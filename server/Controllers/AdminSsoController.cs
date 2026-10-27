using server.Models.Admin;
using server.Services.Admin;

namespace server.Controllers;

[ApiController]
[Route("Admin/Sso")]
[ApiExplorerSettings(GroupName = "Services")]
public sealed class AdminSsoController(AdminSsoService adminSsoService, ILogger<AdminSsoController> logger) : ControllerBase
{
    private const string _callbackPage = "/admin/#/sso";

    /// <summary>
    /// 查询后台是否启用了单点登录。
    /// </summary>
    /// <returns>启用状态与身份源显示名。</returns>
    [HttpGet("Status")]
    public IActionResult GetStatus()
    {
        return Success<AdminSsoStatus>("Admin SSO status acquired successfully", new AdminSsoStatus
        {
            Enabled = adminSsoService.IsEnabled,
            DisplayName = adminSsoService.DisplayName,
        });
    }

    /// <summary>
    /// 生成跳转到身份源的授权地址。前端拿到地址后整页跳转，不要在 XHR 里跟随。
    /// </summary>
    /// <param name="returnPath">登录完成后要回到的后台内路径，必须是站内绝对路径。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>授权地址。</returns>
    [HttpGet("Challenge")]
    public async Task<IActionResult> ChallengeAsync([FromQuery] string? returnPath, CancellationToken cancellationToken)
    {
        if (!adminSsoService.IsEnabled)
        {
            return Fail("Admin SSO is not enabled", ReturnCode.SsoNotEnabled, ServerKeys.ServerAdminSsoNotEnabled);
        }

        (string? url, string? error) = await adminSsoService.CreateAuthorizeUrlAsync(returnPath, cancellationToken).ConfigureAwait(false);

        if (url is null)
        {
            logger.LogWarning("Admin SSO challenge was not issued: {Error}", error);
            return Fail(error ?? "The identity provider is unavailable", ReturnCode.SsoUpstreamFailed, ServerKeys.ServerAdminSsoUpstreamFailed);
        }

        return Success<string>("Redirect to this url to start the admin SSO login", url);
    }

    /// <summary>
    /// 身份源的回调入口。无论成功与否都只回跳后台页面，凭证只出现在 hash 片段里。
    /// </summary>
    /// <param name="code">身份源返回的授权码。</param>
    /// <param name="state">身份源原样带回的 state。</param>
    /// <param name="error">身份源直接返回的错误名。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>302 回跳后台。</returns>
    [HttpGet("Callback")]
    public async Task<IActionResult> CallbackAsync([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        AdminSsoCallbackResult result = await adminSsoService.HandleCallbackAsync(code, state, error, cancellationToken).ConfigureAwait(false);

        if (result.Status is AdminSsoCallbackStatus.Success)
        {
            Dictionary<string, string?> success = new() { ["ticket"] = result.Ticket! };
            if (!string.IsNullOrEmpty(result.ReturnPath))
            {
                success["return"] = result.ReturnPath;
            }

            return RedirectToCallback(success);
        }

        logger.LogWarning("Admin SSO callback ended with {Status}: {Detail}", result.Status, result.Detail);

        Dictionary<string, string?> failure = new() { ["error"] = ToErrorSlug(result.Status) };
        if (!string.IsNullOrEmpty(result.Subject))
        {
            failure["subject"] = result.Subject;
        }

        return RedirectToCallback(failure);
    }

    /// <summary>
    /// 用一次性票据换取后台令牌。票据用过即废，后台令牌不带刷新令牌，到期后重新走一次登录。
    /// </summary>
    /// <param name="request">包含票据的请求体。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>后台令牌与账号信息。</returns>
    [HttpPost("Exchange")]
    public async Task<IActionResult> ExchangeAsync([FromBody] AdminSsoExchangeRequest request, CancellationToken cancellationToken)
    {
        AdminSsoTokenResponse? token = await adminSsoService
            .RedeemTicketAsync(request.Ticket ?? string.Empty, HttpContext.GetClientIp(), cancellationToken)
            .ConfigureAwait(false);

        if (token is null)
        {
            return Fail("The login ticket is invalid or has expired", ReturnCode.SsoTicketInvalid, ServerKeys.ServerAdminSsoTicketInvalid);
        }

        return Success<AdminSsoTokenResponse>("Admin login succeeded", token, ServerKeys.ServerAdminSsoLoginSucceed);
    }

    private IActionResult RedirectToCallback(IDictionary<string, string?> query)
    {
        // 直接写 Location 而不是 Redirect()，避免框架对整串做二次编码后前端解析不到参数
        Response.Headers.Location = CallbackUrl(query);
        return StatusCode(StatusCodes.Status302Found);
    }

    /// <summary>
    /// 拼出带回跳参数的地址。后台是 hash 路由，参数必须写在 <c>#</c> 之后前端才读得到，
    /// 而 QueryHelpers 会把查询串插到 <c>#</c> 之前，所以只取它编码好的那一段自己接上去。
    /// </summary>
    /// <param name="query">要带给回跳页的参数。</param>
    /// <returns>形如 <c>/admin/#/sso?ticket=...</c> 的地址。</returns>
    public static string CallbackUrl(IDictionary<string, string?> query)
    {
        return $"{_callbackPage}{QueryString.Create(query).ToUriComponent()}";
    }

    private static string ToErrorSlug(AdminSsoCallbackStatus status)
    {
        return status switch
        {
            AdminSsoCallbackStatus.InvalidState => "expired",
            AdminSsoCallbackStatus.UpstreamRejected => "upstream",
            AdminSsoCallbackStatus.TokenInvalid => "invalid_token",
            AdminSsoCallbackStatus.NotProvisioned => "not_provisioned",
            AdminSsoCallbackStatus.Disabled => "disabled",
            _ => "unknown",
        };
    }
}
