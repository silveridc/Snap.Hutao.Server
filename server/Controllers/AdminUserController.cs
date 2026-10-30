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
public sealed class AdminUserController(
    UserAdminService userAdminService,
    BanService banService,
    AdminQueryService adminQueryService,
    AppOptions appOptions) : ControllerBase
{
    /// <summary>
    /// 获取用户数据统计概览（总数、维护者、各服务在期人数、数据量等）.
    /// </summary>
    /// <returns>用户统计数据.</returns>
    [HttpGet("users/statistics")]
    public async Task<IActionResult> UserStatisticsAsync()
    {
        UserStatistics statistics = await userAdminService.GetStatisticsAsync().ConfigureAwait(false);
        return Success<UserStatistics>("User statistics acquired successfully", statistics, ServerKeys.ServerAdminUserStatisticsSucceed);
    }

    /// <summary>
    /// 分页获取用户列表.
    /// </summary>
    /// <param name="page">页码，从 1 开始.</param>
    /// <param name="pageSize">每页数量，1-100.</param>
    /// <param name="keyword">按用户名或邮箱模糊搜索.</param>
    /// <param name="maintainer">只取维护者（<see langword="true"/>）或只取非维护者（<see langword="false"/>），null 不筛.</param>
    /// <param name="developer">只取开发者（<see langword="true"/>）或只取非开发者（<see langword="false"/>），null 不筛.</param>
    /// <param name="expiringInDays">只取这么多天内到期的授权，null 不筛.</param>
    /// <returns>分页用户列表.</returns>
    [HttpGet("users")]
    public async Task<IActionResult> UserListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null, [FromQuery] bool? maintainer = null, [FromQuery] bool? developer = null, [FromQuery] int? expiringInDays = null)
    {
        PagedResult<UserListItem> result = await userAdminService.GetUsersAsync(page, pageSize, keyword, maintainer, developer, expiringInDays).ConfigureAwait(false);
        return Success<PagedResult<UserListItem>>("User list acquired successfully", result, ServerKeys.ServerAdminUserListSucceed);
    }

    /// <summary>
    /// 获取用户详情（含祈愿 Uid 与数量、兑换使用次数）.
    /// </summary>
    [HttpGet("users/{userId}")]
    public async Task<IActionResult> UserDetailAsync([FromRoute] int userId)
    {
        UserDetail? detail = await adminQueryService.GetUserDetailAsync(userId).ConfigureAwait(false);
        return detail is null
            ? Fail("User does not exist", ReturnCode.UserNameNotExists, ServerKeys.ServerAdminUserNotFound)
            : Success<UserDetail>("User details retrieved successfully", detail, ServerKeys.ServerAdminUserDetailSucceed);
    }

    /// <summary>
    /// 更新用户的维护者/开发者授权标志.
    /// </summary>
    /// <param name="userId">目标用户.</param>
    /// <param name="request">要设置的标志（为空的字段保持不变）.</param>
    /// <returns>操作结果.</returns>
    [HttpPatch("users/{userId}/privileges")]
    public async Task<IActionResult> UpdateUserPrivilegeAsync([FromRoute] int userId, [FromBody] UserPrivilegeRequest request)
    {
        request.UserId = userId;

        // 防止维护者误将自己降级导致失去后台权限
        if (this.TryGetUserId(out int currentUserId) && request.UserId == currentUserId && request.IsMaintainer is false)
        {
            return Fail("You cannot revoke your own maintainer privilege", ReturnCode.ServiceKeyInvalid, ServerKeys.ServerAdminCannotRevokeOwnMaintainer);
        }

        // appsettings.json 里配置的默认管理员由服务端兜底保护，任何维护者都不能降级它，
        // 否则后台权限可能被一次性摘干净、再也无法恢复
        if (appOptions.DefaultAdminUserId > 0 && request.UserId == appOptions.DefaultAdminUserId && request.IsMaintainer is false)
        {
            return Fail("The default administrator cannot be demoted", ReturnCode.ServiceKeyInvalid, ServerKeys.ServerAdminCannotDemoteDefaultAdmin);
        }

        bool found = await userAdminService.UpdatePrivilegeAsync(request.UserId, request.IsMaintainer, request.IsLicensedDeveloper).ConfigureAwait(false);
        return found
            ? Success("User privileges updated successfully", ServerKeys.ServerAdminUpdatePrivilegeSucceed)
            : Fail("User does not exist", ReturnCode.UserNameNotExists, ServerKeys.ServerAdminUserNotFound);
    }

    /// <summary>
    /// 以管理员身份重置用户密码.
    /// </summary>
    /// <param name="userId">目标用户.</param>
    /// <param name="request">新密码.</param>
    /// <returns>操作结果.</returns>
    [HttpPut("users/{userId}/password")]
    public async Task<IActionResult> ResetUserPasswordAsync([FromRoute] int userId, [FromBody] UserResetPasswordRequest request)
    {
        request.UserId = userId;

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
        {
            return Fail("New password must be at least 8 characters", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminPasswordTooShort);
        }

        (bool ok, string? error, string? localizationKey) = await userAdminService.ResetPasswordAsync(request.UserId, request.NewPassword).ConfigureAwait(false);
        return ok
            ? Success("Password reset successfully", ServerKeys.ServerAdminResetPasswordSucceed)
            : localizationKey is null
                ? Fail(error ?? "Failed to reset password", ReturnCode.InvalidUploadData)
                : Fail(error!, ReturnCode.InvalidUploadData, localizationKey);
    }

    /// <summary>
    /// 分页获取封禁的 Uid 列表.
    /// </summary>
    [HttpGet("bans")]
    public async Task<IActionResult> BanListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null)
    {
        PagedResult<BanItem> result = await banService.GetBannedAsync(page, pageSize, keyword).ConfigureAwait(false);
        return Success<PagedResult<BanItem>>("Ban list acquired successfully", result, ServerKeys.ServerAdminBanListSucceed);
    }

    /// <summary>
    /// 封禁指定 Uid.
    /// </summary>
    [HttpPost("bans")]
    public async Task<IActionResult> BanAddAsync([FromQuery] string uid, [FromQuery] string? reason = null)
    {
        if (!IsValidUid(uid))
        {
            return Fail("Invalid Uid (expected 9-10 digits)", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminInvalidUid);
        }

        bool added = await banService.BanAsync(uid.Trim(), reason).ConfigureAwait(false);
        return added
            ? Success("Uid banned successfully", ServerKeys.ServerAdminBanSucceed)
            : Success("Uid is already in the ban list", ServerKeys.ServerAdminBanAlreadyBanned);
    }

    /// <summary>
    /// 解封指定 Uid.
    /// </summary>
    [HttpDelete("bans/{uid}")]
    public async Task<IActionResult> BanRemoveAsync([FromRoute] string uid)
    {
        bool removed = await banService.UnbanAsync((uid ?? string.Empty).Trim()).ConfigureAwait(false);
        return removed
            ? Success("Uid unbanned successfully", ServerKeys.ServerAdminUnbanSucceed)
            : Success("Uid is not in the ban list", ServerKeys.ServerAdminUnbanNotInList);
    }

    /// <summary>
    /// 批量封禁 Uid.
    /// </summary>
    [HttpPost("bans/batch")]
    public async Task<IActionResult> BanBatchAddAsync([FromBody] BatchUidRequest request)
    {
        int added = await banService.BatchBanAsync(request.Uids, request.Reason).ConfigureAwait(false);
        return Success<int>($"Banned {added} additional Uids", added);
    }

    /// <summary>
    /// 分页获取被判定为无效的祈愿 Uid 列表.
    /// </summary>
    [HttpGet("invalid-gacha-uids")]
    public async Task<IActionResult> InvalidGachaUidListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null)
    {
        PagedResult<string> result = await adminQueryService.GetInvalidGachaUidsAsync(page, pageSize, keyword).ConfigureAwait(false);
        return Success<PagedResult<string>>("Invalid gacha Uid list acquired successfully", result, ServerKeys.ServerAdminInvalidGachaUidListSucceed);
    }

    /// <summary>
    /// 移除无效祈愿 Uid 记录（移除后该 Uid 可重新纳入统计）.
    /// </summary>
    [HttpDelete("invalid-gacha-uids/{uid}")]
    public async Task<IActionResult> InvalidGachaUidRemoveAsync([FromRoute] string uid)
    {
        bool removed = await adminQueryService.RemoveInvalidGachaUidAsync((uid ?? string.Empty).Trim()).ConfigureAwait(false);
        return removed
            ? Success("Removed successfully", ServerKeys.ServerAdminInvalidGachaUidRemoveSucceed)
            : Success("Uid is not in the invalid list", ServerKeys.ServerAdminInvalidGachaUidNotFound);
    }

    private static bool IsValidUid(string? uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            return false;
        }

        string trimmed = uid.Trim();
        return trimmed.Length is >= 9 and <= 10 && trimmed.All(char.IsDigit);
    }
}
