using Microsoft.AspNetCore.WebUtilities;
using server.Models.Entity;
using server.Models.Passport;
using server.Services.Admin;

namespace server.Controllers;

[ApiController]
[Route("api/passport/sso")]
[ApiExplorerSettings(GroupName = "Passport")]
[Obsolete("已弃用sso")]
public sealed class PassportSsoController(
    AdminSsoService adminSsoService,
    PassportService passportService,
    UserLoginLogService userLoginLogService,
    ILogger<PassportSsoController> logger) : ControllerBase
{
    /// <summary>
    /// The sentinel callback address intercepted by the launcher's embedded browser. The host uses the reserved
    /// <c>.invalid</c> domain, and the launcher cancels the request before navigation starts, so this address is never
    /// actually visited.
    /// </summary>
    public const string LauncherCallbackUrl = "https://sso.snaphutao.invalid/callback";

    /// <summary>
    /// Queries whether the server supports launcher single sign-on.
    /// </summary>
    /// <returns>The support state and the identity provider display name.</returns>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Success<PassportSsoStatus>("Single sign-on status acquired successfully", new PassportSsoStatus
        {
            Enabled = adminSsoService.IsClientLoginEnabled,
            DisplayName = adminSsoService.DisplayName,
        });
    }

    /// <summary>
    /// Redirects to the identity provider's authorization page. The launcher lets the embedded browser visit this URL
    /// directly and follow redirects; do not read the response in XHR.
    /// </summary>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>A 302 redirect to the identity provider.</returns>
    [HttpGet("challenge")]
    public async Task<IActionResult> ChallengeAsync(CancellationToken cancellationToken)
    {
        if (!adminSsoService.IsClientLoginEnabled)
        {
            return Fail("Single sign-on is not enabled for the launcher", ReturnCode.SsoNotEnabled, ServerKeys.ServerPassportSsoNotEnabled);
        }

        (string? url, string? error) = await adminSsoService.CreateClientAuthorizeUrlAsync(cancellationToken).ConfigureAwait(false);
        if (url is null)
        {
            logger.LogWarning("Client SSO challenge was not issued: {Error}", error);
            return Fail(error ?? "The identity provider is unavailable", ReturnCode.SsoUpstreamFailed, ServerKeys.ServerAdminSsoUpstreamFailed);
        }

        Response.Headers.Location = url;
        return StatusCode(StatusCodes.Status302Found);
    }

    /// <summary>
    /// The identity provider callback entry. Regardless of the outcome it only redirects to the launcher's sentinel URL;
    /// the ticket and the error reason are carried in the query string, which the launcher reads after cancelling this navigation.
    /// </summary>
    /// <param name="code">The authorization code returned by the identity provider.</param>
    /// <param name="state">The state echoed back by the identity provider.</param>
    /// <param name="error">The error name returned directly by the identity provider.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>A 302 redirect to the address intercepted by the launcher.</returns>
    [HttpGet("~/Passport/Sso/Callback")]
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
    /// Exchanges a one-time ticket for a Hutao site token. The ticket is consumed after use; the returned token carries
    /// a refresh token just like a normal sign-in.
    /// </summary>
    /// <param name="request">The request body containing the ticket.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The Hutao site token and the sign-in email.</returns>
    [HttpPost("sessions")]
    public async Task<IActionResult> ExchangeAsync([FromBody] PassportSsoExchangeRequest request, CancellationToken cancellationToken)
    {
        (HutaoUser? user, AdminAccount? account) = await adminSsoService
            .TakeClientTicketAsync(request.Ticket ?? string.Empty, HttpContext.GetClientIp(), cancellationToken)
            .ConfigureAwait(false);

        if (user is null || account is null)
        {
            await userLoginLogService
                .RecordAsync(HttpContext, null, "sso", null, false, ServerKeys.ServerPassportSsoTicketInvalid)
                .ConfigureAwait(false);

            return Fail("The login ticket is invalid or has expired", ReturnCode.SsoTicketInvalid, ServerKeys.ServerPassportSsoTicketInvalid);
        }

        DeviceInfo deviceInfo = this.GetDeviceInfo();
        TokenResponse token = await passportService.CreateTokenResponseAsync(user, deviceInfo).ConfigureAwait(false);

        await userLoginLogService
            .RecordAsync(HttpContext, deviceInfo, "sso", user.UserName ?? account.UserName, true, null, user.Id)
            .ConfigureAwait(false);

        return Success<PassportSsoLoginResult>(
            "Login succeeded",
            new PassportSsoLoginResult
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                ExpiresIn = token.ExpiresIn,
                UserName = user.UserName ?? account.UserName,
            },
            ServerKeys.ServerPassportLoginSucceed);
    }

    /// <summary>
    /// Maps a callback status to the error slug expected by the launcher.
    /// </summary>
    /// <param name="status">The callback status.</param>
    /// <returns>The error slug.</returns>
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
