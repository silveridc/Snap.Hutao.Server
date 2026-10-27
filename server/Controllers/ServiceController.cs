using System.Globalization;
using server.Models.Admin;
using server.Models.Cache;
using server.Models.Entity.Announcement;
using server.Models.Redeem;
using server.Models.Upload;
using server.Services.Admin;
using server.Services.Cache;
using server.Services.Yae;

namespace server.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[ServiceFilter(typeof(ValidateMaintainPermission), Order = 1)]
[ServiceFilter(typeof(ValidateAdminWritePermission), Order = 2)]
[ServiceFilter(typeof(AuditLogActionFilter), Order = 3)]
[ApiExplorerSettings(GroupName = "Services")]
public class ServiceController(IServiceProvider serviceProvider) : ControllerBase
{
    private readonly GachaLogExpireService _gachaLogExpireService = serviceProvider.GetRequiredService<GachaLogExpireService>();
    private readonly CdnExpireService _cdnExpireService = serviceProvider.GetRequiredService<CdnExpireService>();
    private readonly RedeemService _redeemService = serviceProvider.GetRequiredService<RedeemService>();
    private readonly AnnouncementService _announcementService = serviceProvider.GetRequiredService<AnnouncementService>();
    private readonly CacheService _cacheService = serviceProvider.GetRequiredService<CacheService>();
    private readonly UserAdminService _userAdminService = serviceProvider.GetRequiredService<UserAdminService>();
    private readonly BanService _banService = serviceProvider.GetRequiredService<BanService>();
    private readonly AdminQueryService _adminQueryService = serviceProvider.GetRequiredService<AdminQueryService>();
    private readonly ReleaseService _releaseService = serviceProvider.GetRequiredService<ReleaseService>();
    private readonly SystemConfigService _systemConfigService = serviceProvider.GetRequiredService<SystemConfigService>();
    private readonly AccessionService _accessionService = serviceProvider.GetRequiredService<AccessionService>();
    private readonly IRankService _rankService = serviceProvider.GetRequiredService<IRankService>();
    private readonly YaeMetadataService _yaeMetadataService = serviceProvider.GetRequiredService<YaeMetadataService>();
    private readonly ClientVersionService _clientVersionService = serviceProvider.GetRequiredService<ClientVersionService>();
    private readonly SystemOverviewService _systemOverviewService = serviceProvider.GetRequiredService<SystemOverviewService>();
    private readonly AnalyticsService _analyticsService = serviceProvider.GetRequiredService<AnalyticsService>();
    private readonly AppDbContext _appDbContext = serviceProvider.GetRequiredService<AppDbContext>();
    private readonly AppOptions _appOptions = serviceProvider.GetRequiredService<AppOptions>();

    /// <summary>
    /// 手动触发深渊统计数据刷新。
    /// </summary>
    /// <returns>操作结果。</returns>
    [HttpGet("Statistics/Run")]
    public async Task<IActionResult> RunStatisticsAsync()
    {
        LegacyStatisticsRefreshJob job = serviceProvider.GetRequiredService<LegacyStatisticsRefreshJob>();
        await job.Execute(default!).ConfigureAwait(false);
        return Success("Operation succeeded", ServerKeys.ServerAdminOperationSucceed);
    }

    /// <summary>
    /// 为所有用户补偿祈愿记录服务时长。
    /// </summary>
    /// <param name="days">补偿天数。</param>
    /// <returns>操作结果。</returns>
    [HttpGet("GachaLog/Compensation")]
    public async Task<IActionResult> GachaLogCompensationAsync([FromQuery] int days)
    {
        DateTimeOffset target = await _gachaLogExpireService.ExtendTermForAllUsersAsync(days).ConfigureAwait(false);
        return Success($"Operation succeeded, extended {days} days, expires at: {target}");
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
        TermExtendResult result = await _gachaLogExpireService.ExtendTermForUserNameAsync(userName, days).ConfigureAwait(false);
        return result.Kind switch
        {
            TermExtendResultKind.Ok => Success($"Operation succeeded, extended {days} days, expires at: {result.ExpiredAt}"),
            TermExtendResultKind.NoSuchUser => Fail("User name does not exist", ReturnCode.UserNameNotExists, ServerKeys.ServerAdminUserNameNotFound),
            _ => Fail("Database error", ReturnCode.GachaLogExtendDbException, ServerKeys.ServerDatabaseError),
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
        DateTimeOffset target = await _cdnExpireService.ExtendTermForAllUsersAsync(days).ConfigureAwait(false);
        return Success($"Operation succeeded, extended {days} days, expires at: {target}");
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
        TermExtendResult result = await _cdnExpireService.ExtendTermForUserNameAsync(userName, days).ConfigureAwait(false);
        return result.Kind switch
        {
            TermExtendResultKind.Ok => Success($"Operation succeeded, extended {days} days, expires at: {result.ExpiredAt}"),
            TermExtendResultKind.NoSuchUser => Fail("User name does not exist", ReturnCode.UserNameNotExists, ServerKeys.ServerAdminUserNameNotFound),
            _ => Fail("Database error", ReturnCode.CdnExtendDbException, ServerKeys.ServerDatabaseError),
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
        await _announcementService.ProcessUploadAnnouncementAsync(announcement).ConfigureAwait(false);
        return Success("Announcement uploaded successfully", ServerKeys.ServerAnnouncementUploadSucceed);
    }

    /// <summary>
    /// 生成兑换码。
    /// </summary>
    /// <param name="req">兑换码生成请求。</param>
    /// <returns>生成结果。</returns>
    [HttpPost("Redeem/Generate")]
    public async Task<IActionResult> GenerateRedeemCodesAsync([FromBody] RedeemGenerateRequest req)
    {
        HutaoUser? hutaoUser = await this.GetUserAsync(_appDbContext.Users).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(hutaoUser?.UserName);
        req.Creator = hutaoUser.UserName;

        RedeemGenerateResponse response = await _redeemService.GenerateRedeemCodesAsync(req).ConfigureAwait(false);
        return Success<RedeemGenerateResponse>("Redeem codes generated successfully", response, ServerKeys.ServerAdminRedeemGenerateSucceed);
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
            RemovedKeys = await _cacheService.RemoveKeysAsync(request.Keys).ConfigureAwait(false),
            RemovedByPattern = await _cacheService.RemoveByPatternsAsync(request.Patterns).ConfigureAwait(false),
        };

        return Success<CacheInvalidationResult>("Cache invalidation succeeded", result, ServerKeys.ServerAdminCacheInvalidationSucceed);
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
        List<string> keys = _cacheService.ListKeys(pattern, limit);
        return Success<List<string>>("Cache keys acquired successfully", keys, ServerKeys.ServerAdminCacheKeysSucceed);
    }

    /// <summary>
    /// 获取服务端运行状态概览（进程与机器、资源占用、数据库连接、Redis、Quartz 调度）。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>系统概览数据。</returns>
    [HttpGet("System/Overview")]
    public async Task<IActionResult> SystemOverviewAsync(CancellationToken cancellationToken)
    {
        SystemOverview overview = await _systemOverviewService.GetOverviewAsync(cancellationToken).ConfigureAwait(false);
        return Success<SystemOverview>("System overview acquired successfully", overview, ServerKeys.ServerAdminSystemOverviewSucceed);
    }

    /// <summary>
    /// 卡池抽卡分布：按卡池类型汇总抽数与五星，并给出出得最多的五星物品。
    /// </summary>
    /// <param name="days">统计最近多少天，0 表示全部历史。</param>
    /// <returns>抽卡分布。</returns>
    [HttpGet("Analytics/Gacha")]
    public async Task<IActionResult> GachaAnalyticsAsync([FromQuery] int days = 30)
    {
        GachaAnalytics analytics = await _analyticsService.GetGachaAsync(days).ConfigureAwait(false);
        return Success<GachaAnalytics>("Gacha analytics acquired successfully", analytics, ServerKeys.ServerAdminAnalyticsGachaSucceed);
    }

    /// <summary>
    /// 深渊玩法数据：深境螺旋与幻想真境剧诗的上传量、满星率与角色出场。
    /// </summary>
    /// <returns>深渊数据。</returns>
    [HttpGet("Analytics/Abyss")]
    public async Task<IActionResult> AbyssAnalyticsAsync()
    {
        AbyssAnalytics analytics = await _analyticsService.GetAbyssAsync().ConfigureAwait(false);
        return Success<AbyssAnalytics>("Abyss analytics acquired successfully", analytics, ServerKeys.ServerAdminAnalyticsAbyssSucceed);
    }

    /// <summary>
    /// 伤害分布：直接聚合 damage_ranks 明细，取伤害最高的若干角色。
    /// </summary>
    /// <param name="top">返回前多少名，1-50。</param>
    /// <returns>伤害分布。</returns>
    [HttpGet("Analytics/Damage")]
    public async Task<IActionResult> DamageAnalyticsAsync([FromQuery] int top = 10)
    {
        DamageAnalytics analytics = await _analyticsService.GetDamageAsync(top).ConfigureAwait(false);
        return Success<DamageAnalytics>("Damage analytics acquired successfully", analytics, ServerKeys.ServerAdminAnalyticsDamageSucceed);
    }

    /// <summary>
    /// 获取用户数据统计概览（总数、维护者、各服务在期人数、数据量等）。
    /// </summary>
    /// <returns>用户统计数据。</returns>
    [HttpGet("User/Statistics")]
    public async Task<IActionResult> UserStatisticsAsync()
    {
        UserStatistics statistics = await _userAdminService.GetStatisticsAsync().ConfigureAwait(false);
        return Success<UserStatistics>("User statistics acquired successfully", statistics, ServerKeys.ServerAdminUserStatisticsSucceed);
    }

    /// <summary>
    /// 获取胡桃客户端版本分布（每个版本在册的设备数、首次与最近上报时间）。
    /// </summary>
    /// <returns>客户端版本统计数据。</returns>
    [HttpGet("ClientVersion/List")]
    public async Task<IActionResult> ClientVersionListAsync()
    {
        ClientVersionStatistics statistics = await _clientVersionService.GetStatisticsAsync().ConfigureAwait(false);
        return Success<ClientVersionStatistics>("Client version statistics acquired successfully", statistics, ServerKeys.ServerAdminClientVersionListSucceed);
    }

    /// <summary>
    /// 分页获取用户列表。
    /// </summary>
    /// <param name="page">页码，从 1 开始。</param>
    /// <param name="pageSize">每页数量，1-100。</param>
    /// <param name="keyword">按用户名或邮箱模糊搜索。</param>
    /// <param name="maintainer">只取维护者（<see langword="true"/>）或只取非维护者（<see langword="false"/>），null 不筛。</param>
    /// <param name="developer">只取开发者（<see langword="true"/>）或只取非开发者（<see langword="false"/>），null 不筛。</param>
    /// <param name="expiringInDays">只取这么多天内到期的授权，null 不筛。</param>
    /// <returns>分页用户列表。</returns>
    [HttpGet("User/List")]
    public async Task<IActionResult> UserListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null, [FromQuery] bool? maintainer = null, [FromQuery] bool? developer = null, [FromQuery] int? expiringInDays = null)
    {
        PagedResult<UserListItem> result = await _userAdminService.GetUsersAsync(page, pageSize, keyword, maintainer, developer, expiringInDays).ConfigureAwait(false);
        return Success<PagedResult<UserListItem>>("User list acquired successfully", result, ServerKeys.ServerAdminUserListSucceed);
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
        if (this.TryGetUserId(out int currentUserId) && request.UserId == currentUserId && request.IsMaintainer is false)
        {
            return Fail("You cannot revoke your own maintainer privilege", ReturnCode.ServiceKeyInvalid, ServerKeys.ServerAdminCannotRevokeOwnMaintainer);
        }

        // appsettings.json 里配置的默认管理员由服务端兜底保护，任何维护者都不能降级它，
        // 否则后台权限可能被一次性摘干净、再也无法恢复
        if (_appOptions.DefaultAdminUserId > 0 && request.UserId == _appOptions.DefaultAdminUserId && request.IsMaintainer is false)
        {
            return Fail("The default administrator cannot be demoted", ReturnCode.ServiceKeyInvalid, ServerKeys.ServerAdminCannotDemoteDefaultAdmin);
        }

        bool found = await _userAdminService.UpdatePrivilegeAsync(request.UserId, request.IsMaintainer, request.IsLicensedDeveloper).ConfigureAwait(false);
        return found
            ? Success("User privileges updated successfully", ServerKeys.ServerAdminUpdatePrivilegeSucceed)
            : Fail("User does not exist", ReturnCode.UserNameNotExists, ServerKeys.ServerAdminUserNotFound);
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
            return Fail("New password must be at least 8 characters", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminPasswordTooShort);
        }

        (bool ok, string? error, string? localizationKey) = await _userAdminService.ResetPasswordAsync(request.UserId, request.NewPassword).ConfigureAwait(false);
        return ok
            ? Success("Password reset successfully", ServerKeys.ServerAdminResetPasswordSucceed)
            : localizationKey is null
                ? Fail(error ?? "Failed to reset password", ReturnCode.InvalidUploadData)
                : Fail(error!, ReturnCode.InvalidUploadData, localizationKey);
    }

    /// <summary>
    /// 分页获取封禁的 Uid 列表。
    /// </summary>
    [HttpGet("Ban/List")]
    public async Task<IActionResult> BanListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null)
    {
        PagedResult<BanItem> result = await _banService.GetBannedAsync(page, pageSize, keyword).ConfigureAwait(false);
        return Success<PagedResult<BanItem>>("Ban list acquired successfully", result, ServerKeys.ServerAdminBanListSucceed);
    }

    /// <summary>
    /// 封禁指定 Uid。
    /// </summary>
    [HttpPost("Ban/Add")]
    public async Task<IActionResult> BanAddAsync([FromQuery] string uid, [FromQuery] string? reason = null)
    {
        if (!IsValidUid(uid))
        {
            return Fail("Invalid Uid (expected 9-10 digits)", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminInvalidUid);
        }

        bool added = await _banService.BanAsync(uid.Trim(), reason).ConfigureAwait(false);
        return added
            ? Success("Uid banned successfully", ServerKeys.ServerAdminBanSucceed)
            : Success("Uid is already in the ban list", ServerKeys.ServerAdminBanAlreadyBanned);
    }

    /// <summary>
    /// 解封指定 Uid。
    /// </summary>
    [HttpPost("Ban/Remove")]
    public async Task<IActionResult> BanRemoveAsync([FromQuery] string uid)
    {
        bool removed = await _banService.UnbanAsync((uid ?? string.Empty).Trim()).ConfigureAwait(false);
        return removed
            ? Success("Uid unbanned successfully", ServerKeys.ServerAdminUnbanSucceed)
            : Success("Uid is not in the ban list", ServerKeys.ServerAdminUnbanNotInList);
    }

    /// <summary>
    /// 分页获取公告列表（含历史，不做版本过滤）。
    /// </summary>
    [HttpGet("Announcement/List")]
    public async Task<IActionResult> AnnouncementListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        PagedResult<EntityAnnouncement> result = await _announcementService.GetAllAsync(page, pageSize).ConfigureAwait(false);
        return Success<PagedResult<EntityAnnouncement>>("Announcement list acquired successfully", result, ServerKeys.ServerAdminAnnouncementListSucceed);
    }

    /// <summary>
    /// 删除指定公告。
    /// </summary>
    [HttpPost("Announcement/Delete")]
    public async Task<IActionResult> AnnouncementDeleteAsync([FromQuery] long id, [FromQuery] string locale = "ALL")
    {
        bool removed = await _announcementService.DeleteAsync(id, locale).ConfigureAwait(false);
        return removed
            ? Success("Announcement deleted successfully", ServerKeys.ServerAdminAnnouncementDeleteSucceed)
            : Fail("Announcement does not exist", ReturnCode.NotFound, ServerKeys.ServerAdminAnnouncementNotFound);
    }

    /// <summary>
    /// 编辑公告。
    /// </summary>
    [HttpPost("Announcement/Update")]
    public async Task<IActionResult> AnnouncementUpdateAsync([FromBody] AnnouncementUpdateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return Fail("Title and content must not be empty", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminAnnouncementContentRequired);
        }

        bool ok = await _announcementService.UpdateAsync(request).ConfigureAwait(false);
        return ok
            ? Success("Announcement updated successfully", ServerKeys.ServerAdminAnnouncementUpdateSucceed)
            : Fail("Announcement does not exist", ReturnCode.NotFound, ServerKeys.ServerAdminAnnouncementNotFound);
    }

    /// <summary>
    /// 分页获取兑换码列表。
    /// </summary>
    [HttpGet("Redeem/List")]
    public async Task<IActionResult> RedeemListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null)
    {
        PagedResult<RedeemCodeItem> result = await _adminQueryService.GetRedeemCodesAsync(page, pageSize, keyword).ConfigureAwait(false);
        return Success<PagedResult<RedeemCodeItem>>("Redeem code list acquired successfully", result, ServerKeys.ServerAdminRedeemListSucceed);
    }

    /// <summary>
    /// 某张兑换码的使用者清单。
    /// </summary>
    /// <param name="id">兑换码主键。</param>
    /// <returns>使用该兑换码的用户与时间。</returns>
    [HttpGet("Redeem/Uses")]
    public async Task<IActionResult> RedeemUsesAsync([FromQuery] uint id)
    {
        List<RedeemCodeUsageItem> items = await _adminQueryService.GetRedeemCodeUsesAsync(id).ConfigureAwait(false);
        return Success<List<RedeemCodeUsageItem>>("Redeem code usages acquired successfully", items, ServerKeys.ServerAdminRedeemUsesSucceed);
    }

    /// <summary>
    /// 编辑兑换码（服务、时长、备注、过期时间、可用次数）。
    /// </summary>
    [HttpPost("Redeem/Update")]
    public async Task<IActionResult> RedeemUpdateAsync([FromBody] RedeemCodeUpdateRequest request)
    {
        bool ok = await _adminQueryService.UpdateRedeemCodeAsync(request).ConfigureAwait(false);
        return ok
            ? Success("Redeem code updated successfully", ServerKeys.ServerAdminRedeemUpdateSucceed)
            : Fail("Redeem code does not exist", ReturnCode.NotFound, ServerKeys.ServerRedeemCodeNotFound);
    }

    /// <summary>
    /// 删除兑换码（同时删除其使用记录）。
    /// </summary>
    [HttpPost("Redeem/Delete")]
    public async Task<IActionResult> RedeemDeleteAsync([FromQuery] uint id)
    {
        bool removed = await _adminQueryService.DeleteRedeemCodeAsync(id).ConfigureAwait(false);
        return removed
            ? Success("Redeem code deleted successfully", ServerKeys.ServerAdminRedeemDeleteSucceed)
            : Fail("Redeem code does not exist", ReturnCode.NotFound, ServerKeys.ServerRedeemCodeNotFound);
    }

    /// <summary>
    /// 分页获取开源许可证申请列表。
    /// </summary>
    [HttpGet("License/Applications")]
    public async Task<IActionResult> LicenseApplicationsAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool onlyPending = false)
    {
        PagedResult<LicenseApplicationItem> result = await _adminQueryService.GetLicenseApplicationsAsync(page, pageSize, onlyPending).ConfigureAwait(false);
        return Success<PagedResult<LicenseApplicationItem>>("License application list acquired successfully", result, ServerKeys.ServerAdminLicenseListSucceed);
    }

    /// <summary>
    /// 批准开源许可证申请（维护者鉴权，供后台使用）。
    /// </summary>
    [HttpPost("License/Approve")]
    public async Task<IActionResult> LicenseApproveAsync([FromQuery] string userName, [FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(code))
        {
            return Fail("Approval failed: incomplete parameters", ReturnCode.LicenseApprovalFailed, ServerKeys.ServerAdminLicenseApproveInvalidParameters);
        }

        return await _accessionService.ApproveApplicationAsync(userName.Trim(), code.Trim()).ConfigureAwait(false) switch
        {
            ApplicationApproveResult.Ok => Success("Approved successfully", ServerKeys.ServerAdminLicenseApproveSucceed),
            ApplicationApproveResult.UsetNotExists => Fail("Approval failed: account does not exist", ReturnCode.LicenseApprovalFailed, ServerKeys.ServerAdminLicenseApproveUserNotFound),
            ApplicationApproveResult.NoSuchApplication => Fail("Approval failed: application does not exist", ReturnCode.LicenseApprovalFailed, ServerKeys.ServerAdminLicenseApproveNotFound),
            _ => Fail("Approval failed", ReturnCode.LicenseApprovalFailed, ServerKeys.ServerAdminLicenseApproveFailed),
        };
    }

    /// <summary>
    /// 获取放行的客户端版本 Header 列表。
    /// </summary>
    [HttpGet("AllowedVersion/List")]
    public async Task<IActionResult> AllowedVersionListAsync()
    {
        List<string> versions = await _adminQueryService.GetAllowedVersionsAsync().ConfigureAwait(false);
        return Success<List<string>>("Allowed versions acquired successfully", versions, ServerKeys.ServerAdminAllowedVersionListSucceed);
    }

    /// <summary>
    /// 新增放行的客户端版本 Header。
    /// </summary>
    [HttpPost("AllowedVersion/Add")]
    public async Task<IActionResult> AllowedVersionAddAsync([FromQuery] string header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return Fail("Version header must not be empty", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminAllowedVersionHeaderRequired);
        }

        bool added = await _adminQueryService.AddAllowedVersionAsync(header.Trim()).ConfigureAwait(false);
        return added
            ? Success("Allowed version added successfully", ServerKeys.ServerAdminAllowedVersionAddSucceed)
            : Success("Version already exists", ServerKeys.ServerAdminAllowedVersionAlreadyExists);
    }

    /// <summary>
    /// 移除放行的客户端版本 Header。
    /// </summary>
    [HttpPost("AllowedVersion/Remove")]
    public async Task<IActionResult> AllowedVersionRemoveAsync([FromQuery] string header)
    {
        bool removed = await _adminQueryService.RemoveAllowedVersionAsync((header ?? string.Empty).Trim()).ConfigureAwait(false);
        return removed
            ? Success("Allowed version removed successfully", ServerKeys.ServerAdminAllowedVersionRemoveSucceed)
            : Success("Version does not exist", ServerKeys.ServerAdminAllowedVersionNotFound);
    }

    /// <summary>
    /// 分页获取被判定为无效的祈愿 Uid 列表。
    /// </summary>
    [HttpGet("InvalidGachaUid/List")]
    public async Task<IActionResult> InvalidGachaUidListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null)
    {
        PagedResult<string> result = await _adminQueryService.GetInvalidGachaUidsAsync(page, pageSize, keyword).ConfigureAwait(false);
        return Success<PagedResult<string>>("Invalid gacha Uid list acquired successfully", result, ServerKeys.ServerAdminInvalidGachaUidListSucceed);
    }

    /// <summary>
    /// 移除无效祈愿 Uid 记录（移除后该 Uid 可重新纳入统计）。
    /// </summary>
    [HttpPost("InvalidGachaUid/Remove")]
    public async Task<IActionResult> InvalidGachaUidRemoveAsync([FromQuery] string uid)
    {
        bool removed = await _adminQueryService.RemoveInvalidGachaUidAsync((uid ?? string.Empty).Trim()).ConfigureAwait(false);
        return removed
            ? Success("Removed successfully", ServerKeys.ServerAdminInvalidGachaUidRemoveSucceed)
            : Success("Uid is not in the invalid list", ServerKeys.ServerAdminInvalidGachaUidNotFound);
    }

    /// <summary>
    /// 分页获取后台操作审计日志。
    /// </summary>
    [HttpGet("Audit/List")]
    public async Task<IActionResult> AuditListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        PagedResult<AuditLogItem> result = await _adminQueryService.GetAuditLogsAsync(page, pageSize).ConfigureAwait(false);
        return Success<PagedResult<AuditLogItem>>("Audit logs acquired successfully", result, ServerKeys.ServerAdminAuditListSucceed);
    }

    /// <summary>
    /// 上/下架公告。
    /// </summary>
    [HttpPost("Announcement/SetEnabled")]
    public async Task<IActionResult> AnnouncementSetEnabledAsync([FromQuery] long id, [FromQuery] bool enabled, [FromQuery] string locale = "ALL")
    {
        bool ok = await _announcementService.SetEnabledAsync(id, locale, enabled).ConfigureAwait(false);
        return ok
            ? Success(enabled ? "Announcement enabled" : "Announcement disabled", enabled ? ServerKeys.ServerAdminAnnouncementEnableSucceed : ServerKeys.ServerAdminAnnouncementDisableSucceed)
            : Fail("Announcement does not exist", ReturnCode.NotFound, ServerKeys.ServerAdminAnnouncementNotFound);
    }

    /// <summary>
    /// 获取用户详情（含祈愿 Uid 与数量、兑换使用次数）。
    /// </summary>
    [HttpGet("User/Detail")]
    public async Task<IActionResult> UserDetailAsync([FromQuery] int userId)
    {
        UserDetail? detail = await _adminQueryService.GetUserDetailAsync(userId).ConfigureAwait(false);
        return detail is null
            ? Fail("User does not exist", ReturnCode.UserNameNotExists, ServerKeys.ServerAdminUserNotFound)
            : Success<UserDetail>("User details retrieved successfully", detail, ServerKeys.ServerAdminUserDetailSucceed);
    }

    /// <summary>
    /// 批量封禁 Uid。
    /// </summary>
    [HttpPost("Ban/BatchAdd")]
    public async Task<IActionResult> BanBatchAddAsync([FromBody] BatchUidRequest request)
    {
        int added = await _banService.BatchBanAsync(request.Uids, request.Reason).ConfigureAwait(false);
        return Success<int>($"Banned {added} additional Uids", added);
    }

    /// <summary>
    /// 批量删除兑换码。
    /// </summary>
    [HttpPost("Redeem/BatchDelete")]
    public async Task<IActionResult> RedeemBatchDeleteAsync([FromBody] BatchIdRequest request)
    {
        long removed = await _adminQueryService.BatchDeleteRedeemCodesAsync(request.Ids).ConfigureAwait(false);
        return Success<long>($"Deleted {removed} redeem codes", removed);
    }

    /// <summary>
    /// 获取排行榜可用的 Redis 键列表。
    /// </summary>
    [HttpGet("Rank/Keys")]
    public async Task<IActionResult> RankKeysAsync()
    {
        List<string> keys = await _rankService.GetRankKeysAsync().ConfigureAwait(false);
        return Success<List<string>>("Leaderboard keys retrieved successfully", keys, ServerKeys.ServerAdminRankKeysSucceed);
    }

    /// <summary>
    /// 获取指定排行榜的前 N 名。
    /// </summary>
    [HttpGet("Rank/Leaderboard")]
    public async Task<IActionResult> RankLeaderboardAsync([FromQuery] string key, [FromQuery] int count = 50)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Fail("Leaderboard key is required", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminRankKeyRequired);
        }

        List<RankEntry> entries = await _rankService.GetLeaderboardAsync(key, count).ConfigureAwait(false);
        return Success<List<RankEntry>>("Leaderboard retrieved successfully", entries, ServerKeys.ServerAdminRankLeaderboardSucceed);
    }

    /// <summary>
    /// 获取当前激活的 Snap Hutao 客户端版本。
    /// </summary>
    [HttpGet("Release/Current")]
    public async Task<IActionResult> ReleaseCurrentAsync()
    {
        HutaoReleaseItem? release = await _releaseService.GetCurrentAsync().ConfigureAwait(false);
        return Success<HutaoReleaseItem?>("Current release retrieved successfully", release, ServerKeys.ServerAdminReleaseCurrentSucceed);
    }

    /// <summary>
    /// 分页获取 Snap Hutao 客户端发布历史。
    /// </summary>
    [HttpGet("Release/List")]
    public async Task<IActionResult> ReleaseListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        PagedResult<HutaoReleaseItem> result = await _releaseService.GetListAsync(page, pageSize).ConfigureAwait(false);
        return Success<PagedResult<HutaoReleaseItem>>("Release history retrieved successfully", result, ServerKeys.ServerAdminReleaseListSucceed);
    }

    /// <summary>
    /// 发布并激活新的 Snap Hutao 客户端版本。
    /// </summary>
    [HttpPost("Release/Publish")]
    public async Task<IActionResult> ReleasePublishAsync([FromBody] HutaoReleasePublishRequest request)
    {
        (bool success, string message, string? localizationKey, HutaoReleaseItem? release) = await _releaseService
            .PublishAsync(request, this.GetUserIdOrZero())
            .ConfigureAwait(false);

        return success
            ? Success<HutaoReleaseItem>(message, release!, localizationKey!)
            : localizationKey is null
                ? Fail(message, ReturnCode.InvalidUploadData)
                : Fail(message, ReturnCode.InvalidUploadData, localizationKey);
    }

    /// <summary>
    /// 激活历史版本，用于显式回滚。
    /// </summary>
    [HttpPost("Release/Activate")]
    public async Task<IActionResult> ReleaseActivateAsync([FromBody] HutaoReleaseActivateRequest request)
    {
        bool activated = await _releaseService.ActivateAsync(request.Id).ConfigureAwait(false);
        return activated
            ? Success("Release activated", ServerKeys.ServerAdminReleaseActivateSucceed)
            : Fail("Release does not exist", ReturnCode.NotFound, ServerKeys.ServerAdminReleaseNotFound);
    }

    /// <summary>
    /// 获取所有系统配置项（DB 覆盖值）。
    /// </summary>
    [HttpGet("Config/Get")]
    public async Task<IActionResult> ConfigGetAsync()
    {
        Dictionary<string, string?> settings = await _systemConfigService.GetAllAsync().ConfigureAwait(false);
        settings[SystemConfigService.Keys.OidcEnabled] = _systemConfigService.Oidc.Enabled ? "true" : "false";
        settings[SystemConfigService.Keys.OidcRequireHttpsMetadata] = _systemConfigService.Oidc.RequireHttpsMetadata ? "true" : "false";
        return Success<Dictionary<string, string?>>("Configuration retrieved successfully", settings, ServerKeys.ServerAdminConfigGetSucceed);
    }

    /// <summary>
    /// 批量更新系统配置项。
    /// </summary>
    [HttpPost("Config/Update")]
    public async Task<IActionResult> ConfigUpdateAsync([FromBody] Dictionary<string, string?> settings)
    {
        if (settings.Count == 0)
        {
            return Fail("No configuration changes provided", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminConfigUpdateEmpty);
        }

        (bool success, string message) = await _systemConfigService.UpdateAsync(settings, this.GetUserIdOrZero()).ConfigureAwait(false);
        return success
            ? Success(message, ServerKeys.ServerAdminConfigUpdateSucceed)
            : Fail(message, ReturnCode.InvalidUploadData);
    }

    /// <summary>
    /// 删除配置项，恢复为 appsettings.json 默认值。
    /// </summary>
    [HttpPost("Config/Reset")]
    public async Task<IActionResult> ConfigResetAsync([FromQuery] string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Fail("Configuration key must not be empty", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminConfigKeyRequired);
        }

        (bool success, string message) = await _systemConfigService.DeleteAsync(key).ConfigureAwait(false);
        return success
            ? Success(message, ServerKeys.ServerAdminConfigResetSucceed)
            : Fail(message, ReturnCode.NotFound, ServerKeys.ServerAdminConfigNotFound);
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

    /// <summary>
    /// 从 Yae 的 metadata bucket 拉取 protobuf 并转换成就注入配置，落库后由 <c>/client/AchievementFieldId_{version}.json</c> 对外提供。
    /// </summary>
    /// <param name="chineseHash">本次同步强制指定的国服 exe 指纹（十进制或 0x 十六进制）；省略时用 App:Yae:ChineseGameHashes 配置。新大版本第一次同步通常需要传。</param>
    /// <param name="token">请求取消令牌。</param>
    /// <returns>同步结果，含识别出的两个指纹与条目数。</returns>
    [HttpPost("Yae/SyncMetadata")]
    public async Task<IActionResult> YaeSyncMetadataAsync([FromQuery] string? chineseHash = null, CancellationToken token = default)
    {
        if (!TryParseHash(chineseHash, out uint? forced))
        {
            return Fail($"chineseHash is not a valid hash: {chineseHash}", ReturnCode.InvalidQueryString);
        }

        YaeBucketDecoder.Result result = await _yaeMetadataService.SyncAsync(forced, token).ConfigureAwait(false);
        return result.Succeeded
            ? Success<YaeBucketDecoder.Result>($"Yae metadata synced for game version {result.Version}", result)
            : Fail(result.Error!, ReturnCode.InternalStateException);
    }

    /// <summary>
    /// 列出已同步的 Yae 成就注入配置版本。
    /// </summary>
    /// <param name="token">请求取消令牌。</param>
    /// <returns>版本与指纹列表。</returns>
    [HttpGet("Yae/Metadata")]
    public async Task<IActionResult> YaeMetadataListAsync(CancellationToken token)
    {
        List<YaeMetadataService.Summary> items = await _yaeMetadataService.ListAsync(token).ConfigureAwait(false);
        return Success<List<YaeMetadataService.Summary>>("Yae metadata list acquired successfully", items);
    }

    private static bool TryParseHash(string? raw, out uint? hash)
    {
        hash = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        string trimmed = raw.Trim();
        if (uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint decimalValue))
        {
            hash = decimalValue;
            return true;
        }

        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && uint.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hexValue))
        {
            hash = hexValue;
            return true;
        }

        return false;
    }
}
