using server.Models.Admin;
using server.Services.Admin;

namespace server.Controllers;

[ApiController]
[Route("api/admin/sso")]
[ApiExplorerSettings(GroupName = "Services")]
public sealed class AdminSsoController(AdminSsoService adminSsoService, ILogger<AdminSsoController> logger) : ControllerBase
{
    private const string _callbackPage = "/admin/sso";

    /// <summary>
    /// Builds the callback URL carrying the return parameters. The admin app uses real path routing,
    /// so the query string is appended directly to the path.
    /// </summary>
    /// <param name="query">The parameters to pass to the callback page.</param>
    /// <returns>A URL such as <c>/admin/sso?ticket=...</c>.</returns>
    public static string CallbackUrl(IDictionary<string, string?> query)
    {
        return $"{_callbackPage}{QueryString.Create(query).ToUriComponent()}";
    }

    /// <summary>
    /// Gets whether single sign-on is enabled for the back office.
    /// </summary>
    /// <returns>The enabled state and the identity provider display name.</returns>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Success<AdminSsoStatus>("Admin SSO status acquired successfully", new AdminSsoStatus
        {
            Enabled = adminSsoService.IsEnabled,
            DisplayName = adminSsoService.DisplayName,
        });
    }

    /// <summary>
    /// Creates the authorize URL that redirects to the identity provider. The front end should navigate the whole page
    /// to it instead of following it in XHR.
    /// </summary>
    /// <param name="returnPath">The back-office path to return to after sign-in; must be a site-local absolute path.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The authorize URL.</returns>
    [HttpGet("challenge")]
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
    /// The identity provider callback entry. Regardless of the outcome it only redirects back to the admin page;
    /// the ticket appears only in the one-time query string.
    /// </summary>
    /// <param name="code">The authorization code returned by the identity provider.</param>
    /// <param name="state">The state echoed back by the identity provider.</param>
    /// <param name="error">The error name returned by the identity provider.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>A 302 redirect back to the admin page.</returns>
    [HttpGet("~/Admin/Sso/Callback")]
    public async Task<IActionResult> CallbackAsync([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        AdminSsoCallbackResult result = await adminSsoService.HandleCallbackAsync(code, state, error, cancellationToken).ConfigureAwait(false);

        if (result.Status is AdminSsoCallbackStatus.Success)
        {
            Dictionary<string, string?> success = new()
            {
                ["ticket"] = result.Ticket!,
            };
            if (!string.IsNullOrEmpty(result.ReturnPath))
            {
                success["return"] = result.ReturnPath;
            }

            return RedirectToCallback(success);
        }

        logger.LogWarning("Admin SSO callback ended with {Status}: {Detail}", result.Status, result.Detail);

        Dictionary<string, string?> failure = new()
        {
            ["error"] = ToErrorSlug(result.Status),
        };
        if (!string.IsNullOrEmpty(result.Subject))
        {
            failure["subject"] = result.Subject;
        }

        return RedirectToCallback(failure);
    }

    /// <summary>
    /// Exchanges a one-time ticket for an admin token. The ticket is consumed after use; the admin token has no refresh
    /// token, so the sign-in flow must be repeated after it expires.
    /// </summary>
    /// <param name="request">The request body containing the ticket.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The admin token and account information.</returns>
    [HttpPost("sessions")]
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

    /// <summary>
    /// Maps a callback status to the error slug expected by the admin front end.
    /// </summary>
    /// <param name="status">The callback status.</param>
    /// <returns>The error slug.</returns>
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

    /// <summary>
    /// Writes the Location header directly instead of calling <c>Redirect()</c>, avoiding a second round of encoding
    /// of the whole query string that would prevent the front end from parsing the parameters.
    /// </summary>
    /// <param name="query">The query parameters for the callback page.</param>
    /// <returns>A 302 status result.</returns>
    private StatusCodeResult RedirectToCallback(Dictionary<string, string?> query)
    {
        // 直接写 Location 而不是 Redirect()，避免框架对整串做二次编码后前端解析不到参数
        Response.Headers.Location = CallbackUrl(query);
        return StatusCode(StatusCodes.Status302Found);
    }
}
