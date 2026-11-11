using server.Filter;
using server.Models.Admin;
using server.Services.Admin;

namespace server.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/accounts")]
[ServiceFilter(typeof(ValidateMaintainPermission), Order = 1)]
[ServiceFilter(typeof(ValidateAdminWritePermission), Order = 2)]
[ServiceFilter(typeof(AuditLogActionFilter), Order = 3)]
[ApiExplorerSettings(GroupName = "Services")]
public sealed class AdminAccountController(AdminSsoService adminSsoService) : ControllerBase
{
    /// <summary>
    /// Lists the dedicated back-office admin accounts.
    /// </summary>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The admin account list.</returns>
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        int? currentAccountId = this.TryGetAdminAccountId(out int accountId) ? accountId : null;
        return Success(
            "Admin account list acquired successfully",
            await adminSsoService.ListAsync(currentAccountId, cancellationToken).ConfigureAwait(false),
            ServerKeys.ServerAdminAccountListSucceed);
    }

    /// <summary>
    /// Adds a dedicated back-office admin account, allowing the given identity provider subject to sign in.
    /// </summary>
    /// <param name="request">The subject identifier and display information.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The operation result.</returns>
    [HttpPost]
    public async Task<IActionResult> AddAsync([FromBody] AdminAccountAddRequest request, CancellationToken cancellationToken)
    {
        (ReturnCode code, string message, string? localizationKey) = await adminSsoService
            .AddAsync(request.Subject ?? string.Empty, request.UserName, request.Email, cancellationToken)
            .ConfigureAwait(false);

        return code is ReturnCode.Success
            ? Success(message, localizationKey!)
            : Fail(message, code, localizationKey!);
    }

    /// <summary>
    /// Enables or disables a dedicated back-office admin account. Disabling only blocks future sign-ins;
    /// already issued admin tokens become invalid immediately.
    /// </summary>
    /// <param name="accountId">The target account.</param>
    /// <param name="enabled">The target state.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The operation result.</returns>
    [HttpPatch("{accountId}")]
    public async Task<IActionResult> SetEnabledAsync([FromRoute] int accountId, [FromQuery] bool enabled, CancellationToken cancellationToken)
    {
        int currentAccountId = this.TryGetAdminAccountId(out int id) ? id : 0;
        (ReturnCode code, string message, string? localizationKey) = await adminSsoService
            .SetEnabledAsync(accountId, enabled, currentAccountId, cancellationToken)
            .ConfigureAwait(false);

        return code is ReturnCode.Success
            ? Success(message, localizationKey!)
            : Fail(message, code, localizationKey!);
    }

    /// <summary>
    /// Removes a dedicated back-office admin account.
    /// </summary>
    /// <param name="accountId">The target account.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The operation result.</returns>
    [HttpDelete("{accountId}")]
    public async Task<IActionResult> RemoveAsync([FromRoute] int accountId, CancellationToken cancellationToken)
    {
        int currentAccountId = this.TryGetAdminAccountId(out int id) ? id : 0;
        (ReturnCode code, string message, string? localizationKey) = await adminSsoService
            .RemoveAsync(accountId, currentAccountId, cancellationToken)
            .ConfigureAwait(false);

        return code is ReturnCode.Success
            ? Success(message, localizationKey!)
            : Fail(message, code, localizationKey!);
    }

    /// <summary>
    /// Binds the dedicated back-office admin account to a Hutao account. Only bound identities can sign in to the
    /// launcher via single sign-on; after sign-in the launcher receives the token of this Hutao account.
    /// </summary>
    /// <param name="accountId">The target admin account.</param>
    /// <param name="request">The Hutao account email.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The operation result.</returns>
    [HttpPut("{accountId}/binding")]
    public async Task<IActionResult> BindAsync([FromRoute] int accountId, [FromBody] AdminAccountBindRequest request, CancellationToken cancellationToken)
    {
        (ReturnCode code, string message, string? localizationKey) = await adminSsoService
            .BindAsync(accountId, request.UserName, cancellationToken)
            .ConfigureAwait(false);

        return code is ReturnCode.Success
            ? Success(message, localizationKey!)
            : Fail(message, code, localizationKey!);
    }

    /// <summary>
    /// Removes the binding between the dedicated back-office admin account and its Hutao account;
    /// the identity can no longer be used for launcher sign-in.
    /// </summary>
    /// <param name="accountId">The target admin account.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The operation result.</returns>
    [HttpDelete("{accountId}/binding")]
    public async Task<IActionResult> UnbindAsync([FromRoute] int accountId, CancellationToken cancellationToken)
    {
        (ReturnCode code, string message, string? localizationKey) = await adminSsoService
            .UnbindAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        return code is ReturnCode.Success
            ? Success(message, localizationKey!)
            : Fail(message, code, localizationKey!);
    }
}
