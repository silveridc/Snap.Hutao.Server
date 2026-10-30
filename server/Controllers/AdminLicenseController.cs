using server.Filter;
using server.Models.Admin;
using server.Services.Admin;
using server.Services.Licensing;

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
    /// 分页获取开源许可证申请列表。
    /// </summary>
    [HttpGet("license-applications")]
    public async Task<IActionResult> LicenseApplicationsAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool onlyPending = false)
    {
        PagedResult<LicenseApplicationItem> result = await adminQueryService.GetLicenseApplicationsAsync(page, pageSize, onlyPending).ConfigureAwait(false);
        return Success<PagedResult<LicenseApplicationItem>>("License application list acquired successfully", result, ServerKeys.ServerAdminLicenseListSucceed);
    }

    /// <summary>
    /// 批准开源许可证申请（维护者鉴权，供后台使用）。
    /// </summary>
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
