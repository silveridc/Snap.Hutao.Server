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
    /// Gets a page of admin operation audit logs.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>The requested page of audit logs.</returns>
    [HttpGet("audit-logs")]
    public async Task<IActionResult> AuditListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return Success(
            "Audit logs acquired successfully",
            await adminQueryService.GetAuditLogsAsync(page, pageSize).ConfigureAwait(false),
            ServerKeys.ServerAdminAuditListSucceed);
    }

    /// <summary>
    /// Gets a page of client login logs for auditing.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="keyword">The optional email or IP keyword.</param>
    /// <param name="success">The optional login outcome filter.</param>
    /// <param name="from">The optional inclusive lower bound as a Unix timestamp in seconds.</param>
    /// <param name="to">The optional inclusive upper bound as a Unix timestamp in seconds.</param>
    /// <returns>The requested page of login logs.</returns>
    [HttpGet("login-logs")]
    public async Task<IActionResult> LoginLogListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? keyword = null,
        [FromQuery] bool? success = null,
        [FromQuery] long? from = null,
        [FromQuery] long? to = null)
    {
        return Success(
            "Login log acquired successfully",
            await adminQueryService.GetLoginLogsAsync(page, pageSize, keyword, success, from, to).ConfigureAwait(false),
            ServerKeys.ServerAdminLoginLogListSucceed);
    }

    /// <summary>
    /// Gets a page of scheduled job execution records.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="jobType">The job implementation type; leave empty for all.</param>
    /// <param name="success">Whether to return only succeeded or only failed executions; leave empty for all.</param>
    /// <returns>The operation result.</returns>
    [HttpGet("job-logs")]
    public async Task<IActionResult> JobLogListAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? jobType = null,
        [FromQuery] bool? success = null)
    {
        return Success(
            "Job log acquired successfully",
            await adminQueryService.GetJobLogsAsync(page, pageSize, jobType, success).ConfigureAwait(false),
            ServerKeys.ServerAdminJobLogListSucceed);
    }
}
