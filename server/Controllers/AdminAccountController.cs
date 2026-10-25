using server.Models.Admin;
using server.Services.Admin;

namespace server.Controllers;

/// <summary>
/// 后台专属账号（SSO）管理。与 <see cref="ServiceController"/> 共用同一套权限过滤器：
/// 读接口对维护者与后台账号开放，写接口在启用单点登录后只放行后台账号，以及绑定了后台账号的主站账号。
/// <para>
/// 身份源只负责证明「你是谁」，能不能进后台由这张账号表决定；
/// 未配置账号的人登录成功后会停在「未授权」页，并拿到自己的 subject 以便转交管理员添加。
/// </para>
/// </summary>
[Authorize]
[ApiController]
[Route("api/Service/AdminAccount")]
[ServiceFilter(typeof(ValidateMaintainPermission), Order = 1)]
[ServiceFilter(typeof(ValidateAdminWritePermission), Order = 2)]
[ServiceFilter(typeof(AuditLogActionFilter), Order = 3)]
[ApiExplorerSettings(GroupName = "Services")]
public sealed class AdminAccountController(AdminSsoService adminSsoService) : ControllerBase
{
    /// <summary>
    /// 列出后台专属账号。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>账号列表。</returns>
    [HttpGet("List")]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        int? currentAccountId = this.TryGetAdminAccountId(out int accountId) ? accountId : null;
        List<AdminAccountItem> items = await adminSsoService.ListAsync(currentAccountId, cancellationToken).ConfigureAwait(false);

        return Success<List<AdminAccountItem>>("Admin account list acquired successfully", items, ServerKeys.ServerAdminAccountListSucceed);
    }

    /// <summary>
    /// 新增后台专属账号，允许指定的身份源主体登录后台。
    /// </summary>
    /// <param name="request">主体标识与显示信息。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>操作结果。</returns>
    [HttpPost("Add")]
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
    /// 启用或停用后台专属账号。停用只断开后续登录，已签发的后台令牌会立即失效。
    /// </summary>
    /// <param name="accountId">目标账号。</param>
    /// <param name="enabled">目标状态。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>操作结果。</returns>
    [HttpPost("SetEnabled")]
    public async Task<IActionResult> SetEnabledAsync([FromQuery] int accountId, [FromQuery] bool enabled, CancellationToken cancellationToken)
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
    /// 删除后台专属账号。
    /// </summary>
    /// <param name="accountId">目标账号。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>操作结果。</returns>
    [HttpPost("Remove")]
    public async Task<IActionResult> RemoveAsync([FromQuery] int accountId, CancellationToken cancellationToken)
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
    /// 把后台专属账号绑定到一个主站账号。只有绑定过的身份才能用单点登录进启动器，
    /// 启动器登录后拿到的就是这个主站账号的令牌。
    /// </summary>
    /// <param name="accountId">目标后台账号。</param>
    /// <param name="request">主站账号的邮箱。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>操作结果。</returns>
    [HttpPost("Bind")]
    public async Task<IActionResult> BindAsync([FromQuery] int accountId, [FromBody] AdminAccountBindRequest request, CancellationToken cancellationToken)
    {
        (ReturnCode code, string message, string? localizationKey) = await adminSsoService
            .BindAsync(accountId, request.UserName, cancellationToken)
            .ConfigureAwait(false);

        return code is ReturnCode.Success
            ? Success(message, localizationKey!)
            : Fail(message, code, localizationKey!);
    }

    /// <summary>
    /// 解除后台专属账号与主站账号的绑定，该身份随即不能再用于启动器登录。
    /// </summary>
    /// <param name="accountId">目标后台账号。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>操作结果。</returns>
    [HttpPost("Unbind")]
    public async Task<IActionResult> UnbindAsync([FromQuery] int accountId, CancellationToken cancellationToken)
    {
        (ReturnCode code, string message, string? localizationKey) = await adminSsoService
            .UnbindAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        return code is ReturnCode.Success
            ? Success(message, localizationKey!)
            : Fail(message, code, localizationKey!);
    }
}
