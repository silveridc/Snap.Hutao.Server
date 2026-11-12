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
    /// Gets the user statistics overview: totals, maintainers, active service users, data volumes, and more.
    /// </summary>
    /// <returns>The user statistics.</returns>
    [HttpGet("users/statistics")]
    public async Task<IActionResult> UserStatisticsAsync()
    {
        UserStatistics statistics = await userAdminService.GetStatisticsAsync().ConfigureAwait(false);
        return Success("User statistics acquired successfully", statistics, ServerKeys.ServerAdminUserStatisticsSucceed);
    }

    /// <summary>
    /// Gets a page of users.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The page size, 1-100.</param>
    /// <param name="keyword">The optional user name or email keyword.</param>
    /// <param name="maintainer">Return only maintainers (<see langword="true"/>) or only non-maintainers (<see langword="false"/>); <see langword="null"/> disables the filter.</param>
    /// <param name="developer">Return only developers (<see langword="true"/>) or only non-developers (<see langword="false"/>); <see langword="null"/> disables the filter.</param>
    /// <param name="expiringInDays">Return only privileges expiring within the given number of days; <see langword="null"/> disables the filter.</param>
    /// <returns>The requested page of users.</returns>
    [HttpGet("users")]
    public async Task<IActionResult> UserListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null, [FromQuery] bool? maintainer = null, [FromQuery] bool? developer = null, [FromQuery] int? expiringInDays = null)
    {
        PagedResult<UserListItem> result = await userAdminService.GetUsersAsync(page, pageSize, keyword, maintainer, developer, expiringInDays).ConfigureAwait(false);
        return Success("User list acquired successfully", result, ServerKeys.ServerAdminUserListSucceed);
    }

    /// <summary>
    /// Gets the user detail, including the gacha UIDs with item counts and the redemption usage count.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>The user detail.</returns>
    [HttpGet("users/{userId}")]
    public async Task<IActionResult> UserDetailAsync([FromRoute] int userId)
    {
        UserDetail? detail = await adminQueryService.GetUserDetailAsync(userId).ConfigureAwait(false);
        return detail is null
            ? Fail("User does not exist", ReturnCode.UserNameNotExists, ServerKeys.ServerAdminUserNotFound)
            : Success("User details retrieved successfully", detail, ServerKeys.ServerAdminUserDetailSucceed);
    }

    /// <summary>
    /// Updates the maintainer and licensed developer flags of a user.
    /// </summary>
    /// <param name="userId">The target user.</param>
    /// <param name="request">The flags to set; null fields are left unchanged.</param>
    /// <returns>The operation result.</returns>
    [HttpPatch("users/{userId}/privileges")]
    public async Task<IActionResult> UpdateUserPrivilegeAsync([FromRoute] int userId, [FromBody] UserPrivilegeRequest request)
    {
        request.UserId = userId;

        if (this.TryGetUserId(out int currentUserId) && request.UserId == currentUserId && request.IsMaintainer is false)
        {
            return Fail("You cannot revoke your own maintainer privilege", ReturnCode.ServiceKeyInvalid, ServerKeys.ServerAdminCannotRevokeOwnMaintainer);
        }

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
    /// Resets a user's password on behalf of an administrator.
    /// </summary>
    /// <param name="userId">The target user.</param>
    /// <param name="request">The new password.</param>
    /// <returns>The operation result.</returns>
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
    /// Gets a page of banned UIDs.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="keyword">The optional UID keyword to filter by.</param>
    /// <returns>The requested page of banned UIDs.</returns>
    [HttpGet("bans")]
    public async Task<IActionResult> BanListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null)
    {
        PagedResult<BanItem> result = await banService.GetBannedAsync(page, pageSize, keyword).ConfigureAwait(false);
        return Success("Ban list acquired successfully", result, ServerKeys.ServerAdminBanListSucceed);
    }

    /// <summary>
    /// Bans the specified UID.
    /// </summary>
    /// <param name="uid">The UID to ban.</param>
    /// <param name="reason">The optional ban reason.</param>
    /// <returns>The operation result.</returns>
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
    /// Unbans the specified UID.
    /// </summary>
    /// <param name="uid">The UID to unban.</param>
    /// <returns>The operation result.</returns>
    [HttpDelete("bans/{uid}")]
    public async Task<IActionResult> BanRemoveAsync([FromRoute] string uid)
    {
        bool removed = await banService.UnbanAsync((uid ?? string.Empty).Trim()).ConfigureAwait(false);
        return removed
            ? Success("Uid unbanned successfully", ServerKeys.ServerAdminUnbanSucceed)
            : Success("Uid is not in the ban list", ServerKeys.ServerAdminUnbanNotInList);
    }

    /// <summary>
    /// Bans multiple UIDs at once.
    /// </summary>
    /// <param name="request">The UIDs and the optional ban reason.</param>
    /// <returns>The number of newly banned UIDs.</returns>
    [HttpPost("bans/batch")]
    public async Task<IActionResult> BanBatchAddAsync([FromBody] BatchUidRequest request)
    {
        int added = await banService.BatchBanAsync(request.Uids, request.Reason).ConfigureAwait(false);
        return Success($"Banned {added} additional Uids", added);
    }

    /// <summary>
    /// Gets a page of UIDs flagged as invalid for gacha statistics.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="keyword">The optional UID keyword to filter by.</param>
    /// <returns>The requested page of invalid gacha UIDs.</returns>
    [HttpGet("invalid-gacha-uids")]
    public async Task<IActionResult> InvalidGachaUidListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null)
    {
        PagedResult<string> result = await adminQueryService.GetInvalidGachaUidsAsync(page, pageSize, keyword).ConfigureAwait(false);
        return Success("Invalid gacha Uid list acquired successfully", result, ServerKeys.ServerAdminInvalidGachaUidListSucceed);
    }

    /// <summary>
    /// Removes an invalid gacha UID entry so the UID can be counted again.
    /// </summary>
    /// <param name="uid">The UID to remove.</param>
    /// <returns>The operation result.</returns>
    [HttpDelete("invalid-gacha-uids/{uid}")]
    public async Task<IActionResult> InvalidGachaUidRemoveAsync([FromRoute] string uid)
    {
        bool removed = await adminQueryService.RemoveInvalidGachaUidAsync((uid ?? string.Empty).Trim()).ConfigureAwait(false);
        return removed
            ? Success("Removed successfully", ServerKeys.ServerAdminInvalidGachaUidRemoveSucceed)
            : Success("Uid is not in the invalid list", ServerKeys.ServerAdminInvalidGachaUidNotFound);
    }

    /// <summary>
    /// Determines whether the value looks like a game UID (9-10 digits).
    /// </summary>
    /// <param name="uid">The candidate UID.</param>
    /// <returns><see langword="true"/> if the value is a valid UID; otherwise, <see langword="false"/>.</returns>
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
