using server.Filter;
using server.Models.Admin;
using server.Services.Admin;

namespace server.Controllers;

[Authorize]
[ApiController]
[Route("api/admin")]
[ServiceFilter(typeof(ValidateMaintainPermission), Order = 1)]
[ServiceFilter(typeof(ValidateAdminWritePermission), Order = 2)]
[ServiceFilter(typeof(AuditLogActionFilter), Order = 3)]
[ApiExplorerSettings(GroupName = "Services")]
public sealed class AdminLicenseController(AccessionService accessionService, AdminQueryService adminQueryService) : ControllerBase
{
    /// <summary>
    /// Gets a page of open-source license applications.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="onlyPending">Whether to return only applications that have not been approved.</param>
    /// <returns>The requested page of license applications.</returns>
    [HttpGet("license-applications")]
    public async Task<IActionResult> LicenseApplicationsAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool onlyPending = false) =>
        Success(
            "License application list acquired successfully",
            await adminQueryService.GetLicenseApplicationsAsync(page, pageSize, onlyPending).ConfigureAwait(false),
            ServerKeys.ServerAdminLicenseListSucceed);

    /// <summary>
    /// Approves an open-source license application on behalf of a maintainer.
    /// </summary>
    /// <param name="userName">The applicant user name.</param>
    /// <param name="code">The approval code.</param>
    /// <returns>The approval result.</returns>
    [HttpPost("license-applications/approval")]
    public async Task<IActionResult> LicenseApproveAsync([FromQuery] string userName, [FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(code))
        {
            return Fail("Approval failed: incomplete parameters", ReturnCode.LicenseApprovalFailed, ServerKeys.ServerAdminLicenseApproveInvalidParameters);
        }

        return await accessionService.ApproveApplicationAsync(userName.Trim(), code.Trim()).ConfigureAwait(false) switch
        {
            ApplicationApproveResult.Ok => Success("Approved successfully", ServerKeys.ServerAdminLicenseApproveSucceed),
            ApplicationApproveResult.UsetNotExists => Fail("Approval failed: account does not exist", ReturnCode.LicenseApprovalFailed, ServerKeys.ServerAdminLicenseApproveUserNotFound),
            ApplicationApproveResult.NoSuchApplication => Fail("Approval failed: application does not exist", ReturnCode.LicenseApprovalFailed, ServerKeys.ServerAdminLicenseApproveNotFound),
            _ => Fail("Approval failed", ReturnCode.LicenseApprovalFailed, ServerKeys.ServerAdminLicenseApproveFailed),
        };
    }
}
