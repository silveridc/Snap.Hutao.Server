using server.Filter;
using server.Models.Admin;
using server.Services.Admin;
using server.Services.Ranking;

namespace server.Controllers;

[Authorize]
[ApiController]
[Route("api/admin")]
[ServiceFilter(typeof(ValidateMaintainPermission), Order = 1)]
[ServiceFilter(typeof(ValidateAdminWritePermission), Order = 2)]
[ServiceFilter(typeof(AuditLogActionFilter), Order = 3)]
[ApiExplorerSettings(GroupName = "Services")]
public sealed class AdminAnalyticsController(
    AnalyticsService analyticsService,
    SystemOverviewService systemOverviewService,
    ClientVersionService clientVersionService,
    AdminQueryService adminQueryService,
    IRankService rankService) : ControllerBase
{
    /// <summary>
    /// 获取服务端运行状态概览（进程与机器、资源占用、数据库连接、Redis、Quartz 调度）。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>系统概览数据。</returns>
    [HttpGet("system/overview")]
    public async Task<IActionResult> SystemOverviewAsync(CancellationToken cancellationToken)
    {
        SystemOverview overview = await systemOverviewService.GetOverviewAsync(cancellationToken).ConfigureAwait(false);
        return Success<SystemOverview>("System overview acquired successfully", overview, ServerKeys.ServerAdminSystemOverviewSucceed);
    }

    /// <summary>
    /// 卡池抽卡分布：按卡池类型汇总抽数与五星，并给出出得最多的五星物品。
    /// </summary>
    /// <param name="days">统计最近多少天，0 表示全部历史。</param>
    /// <returns>抽卡分布。</returns>
    [HttpGet("analytics/gacha")]
    public async Task<IActionResult> GachaAnalyticsAsync([FromQuery] int days = 30)
    {
        GachaAnalytics analytics = await analyticsService.GetGachaAsync(days).ConfigureAwait(false);
        return Success<GachaAnalytics>("Gacha analytics acquired successfully", analytics, ServerKeys.ServerAdminAnalyticsGachaSucceed);
    }

    /// <summary>
    /// 深渊玩法数据：深境螺旋与幻想真境剧诗的上传量、满星率与角色出场。
    /// </summary>
    /// <returns>深渊数据。</returns>
    [HttpGet("analytics/abyss")]
    public async Task<IActionResult> AbyssAnalyticsAsync()
    {
        AbyssAnalytics analytics = await analyticsService.GetAbyssAsync().ConfigureAwait(false);
        return Success<AbyssAnalytics>("Abyss analytics acquired successfully", analytics, ServerKeys.ServerAdminAnalyticsAbyssSucceed);
    }

    /// <summary>
    /// 伤害分布：直接聚合 damage_ranks 明细，取伤害最高的若干角色。
    /// </summary>
    /// <param name="top">返回前多少名，1-50。</param>
    /// <returns>伤害分布。</returns>
    [HttpGet("analytics/damage")]
    public async Task<IActionResult> DamageAnalyticsAsync([FromQuery] int top = 10)
    {
        DamageAnalytics analytics = await analyticsService.GetDamageAsync(top).ConfigureAwait(false);
        return Success<DamageAnalytics>("Damage analytics acquired successfully", analytics, ServerKeys.ServerAdminAnalyticsDamageSucceed);
    }

    /// <summary>
    /// 获取胡桃客户端版本分布（每个版本在册的设备数、首次与最近上报时间）。
    /// </summary>
    /// <returns>客户端版本统计数据。</returns>
    [HttpGet("client-versions")]
    public async Task<IActionResult> ClientVersionListAsync()
    {
        ClientVersionStatistics statistics = await clientVersionService.GetStatisticsAsync().ConfigureAwait(false);
        return Success<ClientVersionStatistics>("Client version statistics acquired successfully", statistics, ServerKeys.ServerAdminClientVersionListSucceed);
    }

    /// <summary>
    /// 获取放行的客户端版本 Header 列表。
    /// </summary>
    [HttpGet("allowed-client-versions")]
    public async Task<IActionResult> AllowedVersionListAsync()
    {
        List<string> versions = await adminQueryService.GetAllowedVersionsAsync().ConfigureAwait(false);
        return Success<List<string>>("Allowed versions acquired successfully", versions, ServerKeys.ServerAdminAllowedVersionListSucceed);
    }

    /// <summary>
    /// 新增放行的客户端版本 Header。
    /// </summary>
    [HttpPost("allowed-client-versions")]
    public async Task<IActionResult> AllowedVersionAddAsync([FromQuery] string header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return Fail("Version header must not be empty", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminAllowedVersionHeaderRequired);
        }

        bool added = await adminQueryService.AddAllowedVersionAsync(header.Trim()).ConfigureAwait(false);
        return added
            ? Success("Allowed version added successfully", ServerKeys.ServerAdminAllowedVersionAddSucceed)
            : Success("Version already exists", ServerKeys.ServerAdminAllowedVersionAlreadyExists);
    }

    /// <summary>
    /// 移除放行的客户端版本 Header。
    /// </summary>
    [HttpDelete("allowed-client-versions")]
    public async Task<IActionResult> AllowedVersionRemoveAsync([FromQuery] string header)
    {
        bool removed = await adminQueryService.RemoveAllowedVersionAsync((header ?? string.Empty).Trim()).ConfigureAwait(false);
        return removed
            ? Success("Allowed version removed successfully", ServerKeys.ServerAdminAllowedVersionRemoveSucceed)
            : Success("Version does not exist", ServerKeys.ServerAdminAllowedVersionNotFound);
    }

    /// <summary>
    /// 获取排行榜可用的 Redis 键列表。
    /// </summary>
    [HttpGet("ranks")]
    public async Task<IActionResult> RankKeysAsync()
    {
        List<string> keys = await rankService.GetRankKeysAsync().ConfigureAwait(false);
        return Success<List<string>>("Leaderboard keys retrieved successfully", keys, ServerKeys.ServerAdminRankKeysSucceed);
    }

    /// <summary>
    /// 获取指定排行榜的前 N 名。
    /// </summary>
    [HttpGet("ranks/{key}")]
    public async Task<IActionResult> RankLeaderboardAsync([FromRoute] string key, [FromQuery] int count = 50)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Fail("Leaderboard key is required", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminRankKeyRequired);
        }

        List<RankEntry> entries = await rankService.GetLeaderboardAsync(key, count).ConfigureAwait(false);
        return Success<List<RankEntry>>("Leaderboard retrieved successfully", entries, ServerKeys.ServerAdminRankLeaderboardSucceed);
    }
}
