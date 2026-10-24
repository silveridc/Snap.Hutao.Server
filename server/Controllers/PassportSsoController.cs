using Microsoft.AspNetCore.WebUtilities;
using server.Models.Entity;
using server.Models.Passport;
using server.Services.Admin;

namespace server.Controllers;

/// <summary>
/// 启动器（客户端）的单点登录接口。四个地址都不需要登录态，也不返回任何密钥信息：
/// <list type="bullet">
/// <item><c>GET /Passport/Sso/Status</c>：启动器询问服务器是否支持单点登录，据此决定显示哪个登录入口；</item>
/// <item><c>GET /Passport/Sso/Challenge</c>：302 跳到身份源（带 PKCE 与 state）；</item>
/// <item><c>GET /Passport/Sso/Callback</c>：身份源回跳到这里，处理后用 302 把结果带回启动器拦截的哨兵地址；</item>
/// <item><c>POST /Passport/Sso/Exchange</c>：用一次性票据换取主站令牌。</item>
/// </list>
/// 与后台登录共用同一套协议实现，但落地的身份不同：这里换到的是<b>主站账号令牌</b>（含刷新令牌，
/// 与邮箱密码登录完全同构），启动器的其它代码不需要知道登录是怎么来的。
/// 能不能登录由后台专属账号表决定，且该账号必须已绑定一个主站账号。
/// </summary>
[ApiController]
[Route("Passport/Sso")]
[ApiExplorerSettings(GroupName = "Passport")]
public sealed class PassportSsoController(
    AdminSsoService adminSsoService,
    PassportService passportService,
    ILogger<PassportSsoController> logger) : ControllerBase
{
    /// <summary>
    /// 启动器在内置浏览器里拦截的回跳哨兵地址。域名用保留的 <c>.invalid</c>，
    /// 启动器在导航开始前就取消请求，这个地址永远不会真的被访问。
    /// </summary>
    public const string LauncherCallbackUrl = "https://sso.snaphutao.invalid/callback";

    /// <summary>
    /// 查询服务器是否支持启动器单点登录。
    /// </summary>
    /// <returns>支持状态与身份源显示名。</returns>
    [HttpGet("Status")]
    public IActionResult GetStatus()
    {
        return Success<PassportSsoStatus>("Single sign-on status acquired successfully", new PassportSsoStatus
        {
            Enabled = adminSsoService.IsClientLoginEnabled,
            DisplayName = adminSsoService.DisplayName,
        });
    }

    /// <summary>
    /// 跳转到身份源的授权页面。启动器让内置浏览器直接访问本地址并跟随重定向，不要在 XHR 里读响应。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>302 到身份源。</returns>
    [HttpGet("Challenge")]
    public async Task<IActionResult> ChallengeAsync(CancellationToken cancellationToken)
    {
        if (!adminSsoService.IsClientLoginEnabled)
        {
            return Fail("Single sign-on is not enabled for the launcher", ReturnCode.SsoNotEnabled, ServerKeys.ServerPassportSsoNotEnabled);
        }

        (string? Url, string? Error) = await adminSsoService.CreateClientAuthorizeUrlAsync(cancellationToken).ConfigureAwait(false);
        if (Url is null)
        {
            logger.LogWarning("Client SSO challenge was not issued: {Error}", Error);
            return Fail(Error ?? "The identity provider is unavailable", ReturnCode.SsoUpstreamFailed, ServerKeys.ServerAdminSsoUpstreamFailed);
        }

        Response.Headers.Location = Url;
        return StatusCode(StatusCodes.Status302Found);
    }

    /// <summary>
    /// 身份源的回调入口。无论成功与否都只回跳到启动器的哨兵地址，票据与错误原因都在查询串里，
    /// 启动器取消这次导航后自行读取。
    /// </summary>
    /// <param name="code">身份源返回的授权码。</param>
    /// <param name="state">身份源原样带回的 state。</param>
    /// <param name="error">身份源直接返回的错误名。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>302 回跳到启动器拦截的地址。</returns>
    [HttpGet("Callback")]
    public async Task<IActionResult> CallbackAsync([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        PassportSsoCallbackResult result = await adminSsoService.HandleClientCallbackAsync(code, state, error, cancellationToken).ConfigureAwait(false);
        Dictionary<string, string?> query = [];

        if (result.Status is PassportSsoCallbackStatus.Success)
        {
            query["ticket"] = result.Ticket!;
        }
        else
        {
            logger.LogWarning("Client SSO callback ended with {Status}: {Detail}", result.Status, result.Detail);
            query["error"] = ToErrorSlug(result.Status);
            if (!string.IsNullOrEmpty(result.Subject))
            {
                query["subject"] = result.Subject;
            }
        }

        Response.Headers.Location = QueryHelpers.AddQueryString(LauncherCallbackUrl, query);
        return StatusCode(StatusCodes.Status302Found);
    }

    /// <summary>
    /// 用一次性票据换取主站令牌。票据用过即废，换来的令牌与普通登录一样带刷新令牌。
    /// </summary>
    /// <param name="request">包含票据的请求体。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>主站令牌与登录邮箱。</returns>
    [HttpPost("Exchange")]
    public async Task<IActionResult> ExchangeAsync([FromBody] PassportSsoExchangeRequest request, CancellationToken cancellationToken)
    {
        (HutaoUser? user, AdminAccount? account) = await adminSsoService
            .TakeClientTicketAsync(request.Ticket ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || account is null)
        {
            return Fail("The login ticket is invalid or has expired", ReturnCode.SsoTicketInvalid, ServerKeys.ServerPassportSsoTicketInvalid);
        }

        TokenResponse token = await passportService.CreateTokenResponseAsync(user, this.GetDeviceInfo()).ConfigureAwait(false);

        return Success<PassportSsoLoginResult>("Login succeeded", new PassportSsoLoginResult
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresIn = token.ExpiresIn,
            UserName = user.UserName ?? account.UserName,
        }, ServerKeys.ServerPassportLoginSucceed);
    }

    private static string ToErrorSlug(PassportSsoCallbackStatus status)
    {
        return status switch
        {
            PassportSsoCallbackStatus.InvalidState => "expired",
            PassportSsoCallbackStatus.UpstreamRejected => "upstream",
            PassportSsoCallbackStatus.TokenInvalid => "invalid_token",
            PassportSsoCallbackStatus.NotProvisioned => "not_provisioned",
            PassportSsoCallbackStatus.Disabled => "disabled",
            PassportSsoCallbackStatus.NotBound => "not_bound",
            _ => "unknown",
        };
    }
}
