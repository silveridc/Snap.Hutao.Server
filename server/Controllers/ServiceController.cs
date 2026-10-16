using server.Models.Admin;
using server.Models.Cache;
using server.Models.Entity.Announcement;
using server.Models.Redeem;
using server.Models.Upload;
using server.Services.Admin;
using server.Services.Cache;

namespace server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    [ServiceFilter(typeof(ValidateMaintainPermission))]
    [ServiceFilter(typeof(AuditLogActionFilter))]
    [ApiExplorerSettings(GroupName = "Services")]
    public class ServiceController(IServiceProvider serviceProvider) : ControllerBase
    {
        private readonly GachaLogExpireService gachaLogExpireService = serviceProvider.GetRequiredService<GachaLogExpireService>();
        private readonly CdnExpireService cdnExpireService = serviceProvider.GetRequiredService<CdnExpireService>();
        private readonly RedeemService redeemService = serviceProvider.GetRequiredService<RedeemService>();
        private readonly AnnouncementService announcementService = serviceProvider.GetRequiredService<AnnouncementService>();
        private readonly CacheService cacheService = serviceProvider.GetRequiredService<CacheService>();
        private readonly UserAdminService userAdminService = serviceProvider.GetRequiredService<UserAdminService>();
        private readonly BanService banService = serviceProvider.GetRequiredService<BanService>();
        private readonly AdminQueryService adminQueryService = serviceProvider.GetRequiredService<AdminQueryService>();
        private readonly ReleaseService releaseService = serviceProvider.GetRequiredService<ReleaseService>();
        private readonly SystemConfigService systemConfigService = serviceProvider.GetRequiredService<SystemConfigService>();
        private readonly AccessionService accessionService = serviceProvider.GetRequiredService<AccessionService>();
        private readonly IRankService rankService = serviceProvider.GetRequiredService<IRankService>();
        private readonly AppDbContext appDbContext = serviceProvider.GetRequiredService<AppDbContext>();

        /// <summary>
        /// 手动触发深渊统计数据刷新。
        /// </summary>
        /// <returns>操作结果。</returns>
        [HttpGet("Statistics/Run")]
        public async Task<IActionResult> RunStatisticsAsync()
        {
            LegacyStatisticsRefreshJob job = serviceProvider.GetRequiredService<LegacyStatisticsRefreshJob>();
            await job.Execute(default!).ConfigureAwait(false);
            return Models.Response.Response.Success("Operation succeeded", ServerKeys.ServerAdminOperationSucceed);
        }

        /// <summary>
        /// 为所有用户补偿祈愿记录服务时长。
        /// </summary>
        /// <param name="days">补偿天数。</param>
        /// <returns>操作结果。</returns>
        [HttpGet("GachaLog/Compensation")]
        public async Task<IActionResult> GachaLogCompensationAsync([FromQuery] int days)
        {
            DateTimeOffset target = await gachaLogExpireService.ExtendTermForAllUsersAsync(days).ConfigureAwait(false);
            return Models.Response.Response.Success($"Operation succeeded, extended {days} days, expires at: {target}");
        }

        /// <summary>
        /// 为指定用户补偿祈愿记录服务时长。
        /// </summary>
        /// <param name="userName">用户名。</param>
        /// <param name="days">补偿天数。</param>
        /// <returns>操作结果。</returns>
        [HttpGet("GachaLog/Designation")]
        public async Task<IActionResult> GachaLogDesignationAsync([FromQuery] string userName, [FromQuery] int days)
        {
            TermExtendResult result = await gachaLogExpireService.ExtendTermForUserNameAsync(userName, days).ConfigureAwait(false);
            return result.Kind switch
            {
                TermExtendResultKind.Ok => Models.Response.Response.Success($"Operation succeeded, extended {days} days, expires at: {result.ExpiredAt}"),
                TermExtendResultKind.NoSuchUser => Models.Response.Response.Fail(ReturnCode.UserNameNotExists, "User name does not exist", ServerKeys.ServerAdminUserNameNotFound),
                _ => Models.Response.Response.Fail(ReturnCode.GachaLogExtendDbException, "Database error", ServerKeys.ServerDatabaseError),
            };
        }

        /// <summary>
        /// 为所有用户补偿 CDN 服务时长。
        /// </summary>
        /// <param name="days">补偿天数。</param>
        /// <returns>操作结果。</returns>
        [HttpGet("Distribution/Compensation")]
        public async Task<IActionResult> DistributionCompensationAsync([FromQuery] int days)
        {
            DateTimeOffset target = await cdnExpireService.ExtendTermForAllUsersAsync(days).ConfigureAwait(false);
            return Models.Response.Response.Success($"Operation succeeded, extended {days} days, expires at: {target}");
        }

        /// <summary>
        /// 为指定用户补偿 CDN 服务时长。
        /// </summary>
        /// <param name="userName">用户名。</param>
        /// <param name="days">补偿天数。</param>
        /// <returns>操作结果。</returns>
        [HttpGet("Distribution/Designation")]
        public async Task<IActionResult> DistributionDesignationAsync([FromQuery] string userName, [FromQuery] int days)
        {
            TermExtendResult result = await cdnExpireService.ExtendTermForUserNameAsync(userName, days).ConfigureAwait(false);
            return result.Kind switch
            {
                TermExtendResultKind.Ok => Models.Response.Response.Success($"Operation succeeded, extended {days} days, expires at: {result.ExpiredAt}"),
                TermExtendResultKind.NoSuchUser => Models.Response.Response.Fail(ReturnCode.UserNameNotExists, "User name does not exist", ServerKeys.ServerAdminUserNameNotFound),
                _ => Models.Response.Response.Fail(ReturnCode.CdnExtendDbException, "Database error", ServerKeys.ServerDatabaseError),
            };
        }

        /// <summary>
        /// 上传公告。
        /// </summary>
        /// <param name="announcement">公告内容。</param>
        /// <returns>操作结果。</returns>
        [HttpPost("Announcement/Upload")]
        public async Task<IActionResult> UploadAnnouncementAsync([FromBody] HutaoUploadAnnouncement announcement)
        {
            await announcementService.ProcessUploadAnnouncementAsync(announcement).ConfigureAwait(false);
            return Models.Response.Response.Success("Announcement uploaded successfully", ServerKeys.ServerAnnouncementUploadSucceed);
        }

        /// <summary>
        /// 生成兑换码。
        /// </summary>
        /// <param name="req">兑换码生成请求。</param>
        /// <returns>生成结果。</returns>
        [HttpPost("Redeem/Generate")]
        public async Task<IActionResult> GenerateRedeemCodesAsync([FromBody] RedeemGenerateRequest req)
        {
            HutaoUser? hutaoUser = await this.GetUserAsync(appDbContext.Users).ConfigureAwait(false);
            ArgumentNullException.ThrowIfNull(hutaoUser?.UserName);
            req.Creator = hutaoUser.UserName;

            RedeemGenerateResponse response = await redeemService.GenerateRedeemCodesAsync(req).ConfigureAwait(false);
            return Response<RedeemGenerateResponse>.Success("Redeem codes generated successfully", ServerKeys.ServerAdminRedeemGenerateSucceed, response);
        }

        /// <summary>
        /// 批量将缓存标记为过期（按精确键与/或 glob 模式移除）。
        /// </summary>
        /// <param name="request">需要移除的缓存键与模式。</param>
        /// <returns>移除结果。</returns>
        [HttpPost("Cache/Invalidate")]
        public async Task<IActionResult> InvalidateCacheAsync([FromBody] CacheInvalidationRequest request)
        {
            CacheInvalidationResult result = new()
            {
                RemovedKeys = await cacheService.RemoveKeysAsync(request.Keys).ConfigureAwait(false),
                RemovedByPattern = await cacheService.RemoveByPatternsAsync(request.Patterns).ConfigureAwait(false),
            };

            return Response<CacheInvalidationResult>.Success("Cache invalidation succeeded", ServerKeys.ServerAdminCacheInvalidationSucceed, result);
        }

        /// <summary>
        /// 列出匹配指定 glob 模式的缓存键，便于管理界面查看。
        /// </summary>
        /// <param name="pattern">glob 模式，默认 <c>*</c>。</param>
        /// <param name="limit">返回上限，默认 500。</param>
        /// <returns>逻辑缓存键列表。</returns>
        [HttpGet("Cache/Keys")]
        public IActionResult ListCacheKeys([FromQuery] string pattern = "*", [FromQuery] int limit = 500)
        {
            List<string> keys = cacheService.ListKeys(pattern, limit);
            return Response<List<string>>.Success("Cache keys acquired successfully", ServerKeys.ServerAdminCacheKeysSucceed, keys);
        }

        /// <summary>
        /// 获取用户数据统计概览（总数、维护者、各服务在期人数、数据量等）。
        /// </summary>
        /// <returns>用户统计数据。</returns>
        [HttpGet("User/Statistics")]
        public async Task<IActionResult> UserStatisticsAsync()
        {
            UserStatistics statistics = await userAdminService.GetStatisticsAsync().ConfigureAwait(false);
            return Response<UserStatistics>.Success("User statistics acquired successfully", ServerKeys.ServerAdminUserStatisticsSucceed, statistics);
        }

        /// <summary>
        /// 分页获取用户列表。
        /// </summary>
        /// <param name="page">页码，从 1 开始。</param>
        /// <param name="pageSize">每页数量，1-100。</param>
        /// <param name="keyword">按用户名或邮箱模糊搜索。</param>
        /// <returns>分页用户列表。</returns>
        [HttpGet("User/List")]
        public async Task<IActionResult> UserListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null, [FromQuery] bool? maintainer = null, [FromQuery] bool? developer = null, [FromQuery] int? expiringInDays = null)
        {
            PagedResult<UserListItem> result = await userAdminService.GetUsersAsync(page, pageSize, keyword, maintainer, developer, expiringInDays).ConfigureAwait(false);
            return Response<PagedResult<UserListItem>>.Success("User list acquired successfully", ServerKeys.ServerAdminUserListSucceed, result);
        }

        /// <summary>
        /// 更新用户的维护者/开发者授权标志。
        /// </summary>
        /// <param name="request">目标用户与要设置的标志（为空的字段保持不变）。</param>
        /// <returns>操作结果。</returns>
        [HttpPost("User/Privilege")]
        public async Task<IActionResult> UpdateUserPrivilegeAsync([FromBody] UserPrivilegeRequest request)
        {
            // 防止维护者误将自己降级导致失去后台权限
            if (request.UserId == this.GetUserId() && request.IsMaintainer is false)
            {
                return Models.Response.Response.Fail(ReturnCode.ServiceKeyInvalid, "You cannot revoke your own maintainer privilege", ServerKeys.ServerAdminCannotRevokeOwnMaintainer);
            }

            bool found = await userAdminService.UpdatePrivilegeAsync(request.UserId, request.IsMaintainer, request.IsLicensedDeveloper).ConfigureAwait(false);
            return found
                ? Models.Response.Response.Success("User privileges updated successfully", ServerKeys.ServerAdminUpdatePrivilegeSucceed)
                : Models.Response.Response.Fail(ReturnCode.UserNameNotExists, "User does not exist", ServerKeys.ServerAdminUserNotFound);
        }

        /// <summary>
        /// 以管理员身份重置用户密码。
        /// </summary>
        /// <param name="request">目标用户与新密码。</param>
        /// <returns>操作结果。</returns>
        [HttpPost("User/ResetPassword")]
        public async Task<IActionResult> ResetUserPasswordAsync([FromBody] UserResetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            {
                return Models.Response.Response.Fail(ReturnCode.InvalidUploadData, "New password must be at least 8 characters", ServerKeys.ServerAdminPasswordTooShort);
            }

            (bool ok, string? error, string? localizationKey) = await userAdminService.ResetPasswordAsync(request.UserId, request.NewPassword).ConfigureAwait(false);
            return ok
                ? Models.Response.Response.Success("Password reset successfully", ServerKeys.ServerAdminResetPasswordSucceed)
                : localizationKey is null
                    ? Models.Response.Response.Fail(ReturnCode.InvalidUploadData, error ?? "Failed to reset password")
                    : Models.Response.Response.Fail(ReturnCode.InvalidUploadData, error!, localizationKey);
        }

        /// <summary>
        /// 分页获取封禁的 Uid 列表。
        /// </summary>
        [HttpGet("Ban/List")]
        public async Task<IActionResult> BanListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null)
        {
            PagedResult<BanItem> result = await banService.GetBannedAsync(page, pageSize, keyword).ConfigureAwait(false);
            return Response<PagedResult<BanItem>>.Success("Ban list acquired successfully", ServerKeys.ServerAdminBanListSucceed, result);
        }

        /// <summary>
        /// 封禁指定 Uid。
        /// </summary>
        [HttpPost("Ban/Add")]
        public async Task<IActionResult> BanAddAsync([FromQuery] string uid, [FromQuery] string? reason = null)
        {
            if (!IsValidUid(uid))
            {
                return Models.Response.Response.Fail(ReturnCode.InvalidUploadData, "Invalid Uid (expected 9-10 digits)", ServerKeys.ServerAdminInvalidUid);
            }

            bool added = await banService.BanAsync(uid.Trim(), reason).ConfigureAwait(false);
            return added
                ? Models.Response.Response.Success("Uid banned successfully", ServerKeys.ServerAdminBanSucceed)
                : Models.Response.Response.Success("Uid is already in the ban list", ServerKeys.ServerAdminBanAlreadyBanned);
        }

        /// <summary>
        /// 解封指定 Uid。
        /// </summary>
        [HttpPost("Ban/Remove")]
        public async Task<IActionResult> BanRemoveAsync([FromQuery] string uid)
        {
            bool removed = await banService.UnbanAsync((uid ?? string.Empty).Trim()).ConfigureAwait(false);
            return removed
                ? Models.Response.Response.Success("Uid unbanned successfully", ServerKeys.ServerAdminUnbanSucceed)
                : Models.Response.Response.Success("Uid is not in the ban list", ServerKeys.ServerAdminUnbanNotInList);
        }

        /// <summary>
        /// 分页获取公告列表（含历史，不做版本过滤）。
        /// </summary>
        [HttpGet("Announcement/List")]
        public async Task<IActionResult> AnnouncementListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            PagedResult<EntityAnnouncement> result = await announcementService.GetAllAsync(page, pageSize).ConfigureAwait(false);
            return Response<PagedResult<EntityAnnouncement>>.Success("Announcement list acquired successfully", ServerKeys.ServerAdminAnnouncementListSucceed, result);
        }

        /// <summary>
        /// 删除指定公告。
        /// </summary>
        [HttpPost("Announcement/Delete")]
        public async Task<IActionResult> AnnouncementDeleteAsync([FromQuery] long id, [FromQuery] string locale = "ALL")
        {
            bool removed = await announcementService.DeleteAsync(id, locale).ConfigureAwait(false);
            return removed
                ? Models.Response.Response.Success("Announcement deleted successfully", ServerKeys.ServerAdminAnnouncementDeleteSucceed)
                : Models.Response.Response.Fail(ReturnCode.NotFound, "Announcement does not exist", ServerKeys.ServerAdminAnnouncementNotFound);
        }

        /// <summary>
        /// 编辑公告。
        /// </summary>
        [HttpPost("Announcement/Update")]
        public async Task<IActionResult> AnnouncementUpdateAsync([FromBody] AnnouncementUpdateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            {
                return Models.Response.Response.Fail(ReturnCode.InvalidUploadData, "Title and content must not be empty", ServerKeys.ServerAdminAnnouncementContentRequired);
            }

            bool ok = await announcementService.UpdateAsync(request).ConfigureAwait(false);
            return ok
                ? Models.Response.Response.Success("Announcement updated successfully", ServerKeys.ServerAdminAnnouncementUpdateSucceed)
                : Models.Response.Response.Fail(ReturnCode.NotFound, "Announcement does not exist", ServerKeys.ServerAdminAnnouncementNotFound);
        }

        /// <summary>
        /// 分页获取兑换码列表。
        /// </summary>
        [HttpGet("Redeem/List")]
        public async Task<IActionResult> RedeemListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null)
        {
            PagedResult<RedeemCodeItem> result = await adminQueryService.GetRedeemCodesAsync(page, pageSize, keyword).ConfigureAwait(false);
            return Response<PagedResult<RedeemCodeItem>>.Success("Redeem code list acquired successfully", ServerKeys.ServerAdminRedeemListSucceed, result);
        }

        /// <summary>
        /// 编辑兑换码（服务、时长、备注、过期时间、可用次数）。
        /// </summary>
        [HttpPost("Redeem/Update")]
        public async Task<IActionResult> RedeemUpdateAsync([FromBody] RedeemCodeUpdateRequest request)
        {
            bool ok = await adminQueryService.UpdateRedeemCodeAsync(request).ConfigureAwait(false);
            return ok
                ? Models.Response.Response.Success("Redeem code updated successfully", ServerKeys.ServerAdminRedeemUpdateSucceed)
                : Models.Response.Response.Fail(ReturnCode.NotFound, "Redeem code does not exist", ServerKeys.ServerRedeemCodeNotFound);
        }

        /// <summary>
        /// 删除兑换码（同时删除其使用记录）。
        /// </summary>
        [HttpPost("Redeem/Delete")]
        public async Task<IActionResult> RedeemDeleteAsync([FromQuery] uint id)
        {
            bool removed = await adminQueryService.DeleteRedeemCodeAsync(id).ConfigureAwait(false);
            return removed
                ? Models.Response.Response.Success("Redeem code deleted successfully", ServerKeys.ServerAdminRedeemDeleteSucceed)
                : Models.Response.Response.Fail(ReturnCode.NotFound, "Redeem code does not exist", ServerKeys.ServerRedeemCodeNotFound);
        }

        /// <summary>
        /// 分页获取开源许可证申请列表。
        /// </summary>
        [HttpGet("License/Applications")]
        public async Task<IActionResult> LicenseApplicationsAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool onlyPending = false)
        {
            PagedResult<LicenseApplicationItem> result = await adminQueryService.GetLicenseApplicationsAsync(page, pageSize, onlyPending).ConfigureAwait(false);
            return Response<PagedResult<LicenseApplicationItem>>.Success("License application list acquired successfully", ServerKeys.ServerAdminLicenseListSucceed, result);
        }

        /// <summary>
        /// 批准开源许可证申请（维护者鉴权，供后台使用）。
        /// </summary>
        [HttpPost("License/Approve")]
        public async Task<IActionResult> LicenseApproveAsync([FromQuery] string userName, [FromQuery] string code)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(code))
            {
                return Models.Response.Response.Fail(ReturnCode.LicenseApprovalFailed, "Approval failed: incomplete parameters", ServerKeys.ServerAdminLicenseApproveInvalidParameters);
            }

            return await accessionService.ApproveApplicationAsync(userName.Trim(), code.Trim()).ConfigureAwait(false) switch
            {
                ApplicationApproveResult.Ok => Models.Response.Response.Success("Approved successfully", ServerKeys.ServerAdminLicenseApproveSucceed),
                ApplicationApproveResult.UsetNotExists => Models.Response.Response.Fail(ReturnCode.LicenseApprovalFailed, "Approval failed: account does not exist", ServerKeys.ServerAdminLicenseApproveUserNotFound),
                ApplicationApproveResult.NoSuchApplication => Models.Response.Response.Fail(ReturnCode.LicenseApprovalFailed, "Approval failed: application does not exist", ServerKeys.ServerAdminLicenseApproveNotFound),
                _ => Models.Response.Response.Fail(ReturnCode.LicenseApprovalFailed, "Approval failed", ServerKeys.ServerAdminLicenseApproveFailed),
            };
        }

        /// <summary>
        /// 获取放行的客户端版本 Header 列表。
        /// </summary>
        [HttpGet("AllowedVersion/List")]
        public async Task<IActionResult> AllowedVersionListAsync()
        {
            List<string> versions = await adminQueryService.GetAllowedVersionsAsync().ConfigureAwait(false);
            return Response<List<string>>.Success("Allowed versions acquired successfully", ServerKeys.ServerAdminAllowedVersionListSucceed, versions);
        }

        /// <summary>
        /// 新增放行的客户端版本 Header。
        /// </summary>
        [HttpPost("AllowedVersion/Add")]
        public async Task<IActionResult> AllowedVersionAddAsync([FromQuery] string header)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                return Models.Response.Response.Fail(ReturnCode.InvalidUploadData, "Version header must not be empty", ServerKeys.ServerAdminAllowedVersionHeaderRequired);
            }

            bool added = await adminQueryService.AddAllowedVersionAsync(header.Trim()).ConfigureAwait(false);
            return added
                ? Models.Response.Response.Success("Allowed version added successfully", ServerKeys.ServerAdminAllowedVersionAddSucceed)
                : Models.Response.Response.Success("Version already exists", ServerKeys.ServerAdminAllowedVersionAlreadyExists);
        }

        /// <summary>
        /// 移除放行的客户端版本 Header。
        /// </summary>
        [HttpPost("AllowedVersion/Remove")]
        public async Task<IActionResult> AllowedVersionRemoveAsync([FromQuery] string header)
        {
            bool removed = await adminQueryService.RemoveAllowedVersionAsync((header ?? string.Empty).Trim()).ConfigureAwait(false);
            return removed
                ? Models.Response.Response.Success("Allowed version removed successfully", ServerKeys.ServerAdminAllowedVersionRemoveSucceed)
                : Models.Response.Response.Success("Version does not exist", ServerKeys.ServerAdminAllowedVersionNotFound);
        }

        /// <summary>
        /// 分页获取被判定为无效的祈愿 Uid 列表。
        /// </summary>
        [HttpGet("InvalidGachaUid/List")]
        public async Task<IActionResult> InvalidGachaUidListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null)
        {
            PagedResult<string> result = await adminQueryService.GetInvalidGachaUidsAsync(page, pageSize, keyword).ConfigureAwait(false);
            return Response<PagedResult<string>>.Success("Invalid gacha Uid list acquired successfully", ServerKeys.ServerAdminInvalidGachaUidListSucceed, result);
        }

        /// <summary>
        /// 移除无效祈愿 Uid 记录（移除后该 Uid 可重新纳入统计）。
        /// </summary>
        [HttpPost("InvalidGachaUid/Remove")]
        public async Task<IActionResult> InvalidGachaUidRemoveAsync([FromQuery] string uid)
        {
            bool removed = await adminQueryService.RemoveInvalidGachaUidAsync((uid ?? string.Empty).Trim()).ConfigureAwait(false);
            return removed
                ? Models.Response.Response.Success("Removed successfully", ServerKeys.ServerAdminInvalidGachaUidRemoveSucceed)
                : Models.Response.Response.Success("Uid is not in the invalid list", ServerKeys.ServerAdminInvalidGachaUidNotFound);
        }

        /// <summary>
        /// 分页获取后台操作审计日志。
        /// </summary>
        [HttpGet("Audit/List")]
        public async Task<IActionResult> AuditListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            PagedResult<AuditLogItem> result = await adminQueryService.GetAuditLogsAsync(page, pageSize).ConfigureAwait(false);
            return Response<PagedResult<AuditLogItem>>.Success("Audit logs acquired successfully", ServerKeys.ServerAdminAuditListSucceed, result);
        }

        /// <summary>
        /// 上/下架公告。
        /// </summary>
        [HttpPost("Announcement/SetEnabled")]
        public async Task<IActionResult> AnnouncementSetEnabledAsync([FromQuery] long id, [FromQuery] bool enabled, [FromQuery] string locale = "ALL")
        {
            bool ok = await announcementService.SetEnabledAsync(id, locale, enabled).ConfigureAwait(false);
            return ok
                ? Models.Response.Response.Success(enabled ? "Announcement enabled" : "Announcement disabled", enabled ? ServerKeys.ServerAdminAnnouncementEnableSucceed : ServerKeys.ServerAdminAnnouncementDisableSucceed)
                : Models.Response.Response.Fail(ReturnCode.NotFound, "Announcement does not exist", ServerKeys.ServerAdminAnnouncementNotFound);
        }

        /// <summary>
        /// 获取用户详情（含祈愿 Uid 与数量、兑换使用次数）。
        /// </summary>
        [HttpGet("User/Detail")]
        public async Task<IActionResult> UserDetailAsync([FromQuery] int userId)
        {
            UserDetail? detail = await adminQueryService.GetUserDetailAsync(userId).ConfigureAwait(false);
            return detail is null
                ? Models.Response.Response.Fail(ReturnCode.UserNameNotExists, "User does not exist", ServerKeys.ServerAdminUserNotFound)
                : Response<UserDetail>.Success("User details retrieved successfully", ServerKeys.ServerAdminUserDetailSucceed, detail);
        }

        /// <summary>
        /// 批量封禁 Uid。
        /// </summary>
        [HttpPost("Ban/BatchAdd")]
        public async Task<IActionResult> BanBatchAddAsync([FromBody] BatchUidRequest request)
        {
            int added = await banService.BatchBanAsync(request.Uids, request.Reason).ConfigureAwait(false);
            return Response<int>.Success($"Banned {added} additional Uids", added);
        }

        /// <summary>
        /// 批量删除兑换码。
        /// </summary>
        [HttpPost("Redeem/BatchDelete")]
        public async Task<IActionResult> RedeemBatchDeleteAsync([FromBody] BatchIdRequest request)
        {
            long removed = await adminQueryService.BatchDeleteRedeemCodesAsync(request.Ids).ConfigureAwait(false);
            return Response<long>.Success($"Deleted {removed} redeem codes", removed);
        }

        /// <summary>
        /// 获取排行榜可用的 Redis 键列表。
        /// </summary>
        [HttpGet("Rank/Keys")]
        public async Task<IActionResult> RankKeysAsync()
        {
            List<string> keys = await rankService.GetRankKeysAsync().ConfigureAwait(false);
            return Response<List<string>>.Success("Leaderboard keys retrieved successfully", ServerKeys.ServerAdminRankKeysSucceed, keys);
        }

        /// <summary>
        /// 获取指定排行榜的前 N 名。
        /// </summary>
        [HttpGet("Rank/Leaderboard")]
        public async Task<IActionResult> RankLeaderboardAsync([FromQuery] string key, [FromQuery] int count = 50)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return Models.Response.Response.Fail(ReturnCode.InvalidUploadData, "Leaderboard key is required", ServerKeys.ServerAdminRankKeyRequired);
            }

            List<RankEntry> entries = await rankService.GetLeaderboardAsync(key, count).ConfigureAwait(false);
            return Response<List<RankEntry>>.Success("Leaderboard retrieved successfully", ServerKeys.ServerAdminRankLeaderboardSucceed, entries);
        }

        /// <summary>
        /// 获取当前激活的 Snap Hutao 客户端版本。
        /// </summary>
        [HttpGet("Release/Current")]
        public async Task<IActionResult> ReleaseCurrentAsync()
        {
            HutaoReleaseItem? release = await releaseService.GetCurrentAsync().ConfigureAwait(false);
            return Response<HutaoReleaseItem?>.Success("Current release retrieved successfully", ServerKeys.ServerAdminReleaseCurrentSucceed, release);
        }

        /// <summary>
        /// 分页获取 Snap Hutao 客户端发布历史。
        /// </summary>
        [HttpGet("Release/List")]
        public async Task<IActionResult> ReleaseListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            PagedResult<HutaoReleaseItem> result = await releaseService.GetListAsync(page, pageSize).ConfigureAwait(false);
            return Response<PagedResult<HutaoReleaseItem>>.Success("Release history retrieved successfully", ServerKeys.ServerAdminReleaseListSucceed, result);
        }

        /// <summary>
        /// 发布并激活新的 Snap Hutao 客户端版本。
        /// </summary>
        [HttpPost("Release/Publish")]
        public async Task<IActionResult> ReleasePublishAsync([FromBody] HutaoReleasePublishRequest request)
        {
            (bool success, string message, string? localizationKey, HutaoReleaseItem? release) = await releaseService
                .PublishAsync(request, this.GetUserId())
                .ConfigureAwait(false);

            return success
                ? Response<HutaoReleaseItem>.Success(message, localizationKey!, release!)
                : localizationKey is null
                    ? Models.Response.Response.Fail(ReturnCode.InvalidUploadData, message)
                    : Models.Response.Response.Fail(ReturnCode.InvalidUploadData, message, localizationKey);
        }

        /// <summary>
        /// 激活历史版本，用于显式回滚。
        /// </summary>
        [HttpPost("Release/Activate")]
        public async Task<IActionResult> ReleaseActivateAsync([FromBody] HutaoReleaseActivateRequest request)
        {
            bool activated = await releaseService.ActivateAsync(request.Id).ConfigureAwait(false);
            return activated
                ? Models.Response.Response.Success("Release activated", ServerKeys.ServerAdminReleaseActivateSucceed)
                : Models.Response.Response.Fail(ReturnCode.NotFound, "Release does not exist", ServerKeys.ServerAdminReleaseNotFound);
        }

        /// <summary>
        /// 获取所有系统配置项（DB 覆盖值）。
        /// </summary>
        [HttpGet("Config/Get")]
        public async Task<IActionResult> ConfigGetAsync()
        {
            Dictionary<string, string?> settings = await systemConfigService.GetAllAsync().ConfigureAwait(false);
            return Response<Dictionary<string, string?>>.Success("Configuration retrieved successfully", ServerKeys.ServerAdminConfigGetSucceed, settings);
        }

        /// <summary>
        /// 批量更新系统配置项。
        /// </summary>
        [HttpPost("Config/Update")]
        public async Task<IActionResult> ConfigUpdateAsync([FromBody] Dictionary<string, string?> settings)
        {
            if (settings.Count == 0)
            {
                return Models.Response.Response.Fail(ReturnCode.InvalidUploadData, "No configuration changes provided", ServerKeys.ServerAdminConfigUpdateEmpty);
            }

            (bool success, string message) = await systemConfigService.UpdateAsync(settings, this.GetUserId()).ConfigureAwait(false);
            return success
                ? Models.Response.Response.Success(message, ServerKeys.ServerAdminConfigUpdateSucceed)
                : Models.Response.Response.Fail(ReturnCode.InvalidUploadData, message);
        }

        /// <summary>
        /// 删除配置项，恢复为 appsettings.json 默认值。
        /// </summary>
        [HttpPost("Config/Reset")]
        public async Task<IActionResult> ConfigResetAsync([FromQuery] string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return Models.Response.Response.Fail(ReturnCode.InvalidUploadData, "Configuration key must not be empty", ServerKeys.ServerAdminConfigKeyRequired);
            }

            (bool success, string message) = await systemConfigService.DeleteAsync(key).ConfigureAwait(false);
            return success
                ? Models.Response.Response.Success(message, ServerKeys.ServerAdminConfigResetSucceed)
                : Models.Response.Response.Fail(ReturnCode.NotFound, message, ServerKeys.ServerAdminConfigNotFound);
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
}