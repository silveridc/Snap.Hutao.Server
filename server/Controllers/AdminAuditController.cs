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
public sealed class AdminAuditController(AdminQueryService adminQueryService) : ControllerBase
{
    /// <summary>
    /// 分页获取后台操作审计日志。
    /// </summary>
    [HttpGet("audit-logs")]
    public async Task<IActionResult> AuditListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        PagedResult<AuditLogItem> result = await adminQueryService.GetAuditLogsAsync(page, pageSize).ConfigureAwait(false);
        return Success<PagedResult<AuditLogItem>>("Audit logs acquired successfully", result, ServerKeys.ServerAdminAuditListSucceed);
    }

    /// <summary>
    /// 分页获取客户端登录日志，供审计使用。
    /// </summary>
    [HttpGet("login-logs")]
    public async Task<IActionResult> LoginLogListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] bool? success = null,
        [FromQuery] long? from = null,
        [FromQuery] long? to = null)
    {
        PagedResult<UserLoginLogItem> result = await adminQueryService
            .GetLoginLogsAsync(page, pageSize, keyword, success, from, to)
            .ConfigureAwait(false);

        return Success<PagedResult<UserLoginLogItem>>("Login log acquired successfully", result, ServerKeys.ServerAdminLoginLogListSucceed);
    }
}
