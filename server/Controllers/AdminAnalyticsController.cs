using server.Filter;
using server.Services.Admin;

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
    /// Gets the server runtime overview: process and machine information, resource usage, database connectivity, Redis, and Quartz scheduling.
    /// </summary>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The system overview data.</returns>
    [HttpGet("system/overview")]
    public async Task<IActionResult> SystemOverviewAsync(CancellationToken cancellationToken)
    {
        return Success(
            "System overview acquired successfully",
            await systemOverviewService.GetOverviewAsync(cancellationToken).ConfigureAwait(false),
            ServerKeys.ServerAdminSystemOverviewSucceed);
    }

    /// <summary>
    /// Gets the gacha pull distribution per pool type, including the five-star items pulled most often.
    /// </summary>
    /// <param name="days">The lookback window in days; 0 means all history.</param>
    /// <returns>The gacha analytics.</returns>
    [HttpGet("analytics/gacha")]
    public async Task<IActionResult> GachaAnalyticsAsync([FromQuery] int days = 30)
    {
        return Success(
            "Gacha analytics acquired successfully",
            await analyticsService.GetGachaAsync(days).ConfigureAwait(false),
            ServerKeys.ServerAdminAnalyticsGachaSucceed);
    }

    /// <summary>
    /// Gets the abyss analytics: upload counts, full-star rate, and avatar appearances for Spiral Abyss and Imaginarium Theater.
    /// </summary>
    /// <returns>The abyss analytics.</returns>
    [HttpGet("analytics/abyss")]
    public async Task<IActionResult> AbyssAnalyticsAsync()
    {
        return Success(
            "Abyss analytics acquired successfully",
            await analyticsService.GetAbyssAsync().ConfigureAwait(false),
            ServerKeys.ServerAdminAnalyticsAbyssSucceed);
    }

    /// <summary>
    /// Gets the damage distribution aggregated directly from the damage-rank detail rows.
    /// </summary>
    /// <param name="top">The number of avatars to return, 1-50.</param>
    /// <returns>The damage analytics.</returns>
    [HttpGet("analytics/damage")]
    public async Task<IActionResult> DamageAnalyticsAsync([FromQuery] int top = 10)
    {
        return Success(
            "Damage analytics acquired successfully",
            await analyticsService.GetDamageAsync(top).ConfigureAwait(false),
            ServerKeys.ServerAdminAnalyticsDamageSucceed);
    }

    /// <summary>
    /// Gets the Hutao client version distribution: the number of registered devices per version with first and last report times.
    /// </summary>
    /// <returns>The client version statistics.</returns>
    [HttpGet("client-versions")]
    public async Task<IActionResult> ClientVersionListAsync()
    {
        return Success(
            "Client version statistics acquired successfully",
            await clientVersionService.GetStatisticsAsync().ConfigureAwait(false),
            ServerKeys.ServerAdminClientVersionListSucceed);
    }

    /// <summary>
    /// Gets the client version headers that are allowed to pass the version gate.
    /// </summary>
    [HttpGet("allowed-client-versions")]
    public async Task<IActionResult> AllowedVersionListAsync()
    {
        return Success(
            "Allowed versions acquired successfully",
            await adminQueryService.GetAllowedVersionsAsync().ConfigureAwait(false),
            ServerKeys.ServerAdminAllowedVersionListSucceed);
    }

    /// <summary>
    /// Adds a client version header to the allow list.
    /// </summary>
    /// <param name="header">The version header to allow.</param>
    /// <returns>The operation result.</returns>
    [HttpPost("allowed-client-versions")]
    public async Task<IActionResult> AllowedVersionAddAsync([FromQuery] string header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return Fail("Version header must not be empty", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminAllowedVersionHeaderRequired);
        }

        return await adminQueryService.AddAllowedVersionAsync(header.Trim()).ConfigureAwait(false)
            ? Success("Allowed version added successfully", ServerKeys.ServerAdminAllowedVersionAddSucceed)
            : Success("Version already exists", ServerKeys.ServerAdminAllowedVersionAlreadyExists);
    }

    /// <summary>
    /// Removes a client version header from the allow list.
    /// </summary>
    /// <param name="header">The version header to remove.</param>
    /// <returns>The operation result.</returns>
    [HttpDelete("allowed-client-versions")]
    public async Task<IActionResult> AllowedVersionRemoveAsync([FromQuery] string header)
    {
        return await adminQueryService.RemoveAllowedVersionAsync((header ?? string.Empty).Trim()).ConfigureAwait(false)
            ? Success("Allowed version removed successfully", ServerKeys.ServerAdminAllowedVersionRemoveSucceed)
            : Success("Version does not exist", ServerKeys.ServerAdminAllowedVersionNotFound);
    }

    /// <summary>
    /// Gets the Redis keys of the available leaderboards.
    /// </summary>
    [HttpGet("ranks")]
    public async Task<IActionResult> RankKeysAsync()
    {
        return Success(
            "Leaderboard keys retrieved successfully",
            await rankService.GetRankKeysAsync().ConfigureAwait(false),
            ServerKeys.ServerAdminRankKeysSucceed);
    }

    /// <summary>
    /// Gets the top entries of the specified leaderboard.
    /// </summary>
    /// <param name="key">The leaderboard key name.</param>
    /// <param name="count">The number of entries to return.</param>
    /// <returns>The leaderboard entries.</returns>
    [HttpGet("ranks/{key}")]
    public async Task<IActionResult> RankLeaderboardAsync([FromRoute] string key, [FromQuery] int count = 50)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Fail("Leaderboard key is required", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminRankKeyRequired);
        }

        return Success(
            "Leaderboard retrieved successfully",
            await rankService.GetLeaderboardAsync(key, count).ConfigureAwait(false),
            ServerKeys.ServerAdminRankLeaderboardSucceed);
    }
}
