using Quartz;
using server.Filter;
using server.Models.Cache;
using server.Services.Cache;
using server.Services.Yae;
using System.Globalization;

namespace server.Controllers;

[Authorize]
[ApiController]
[Route("api/admin")]
[ServiceFilter(typeof(ValidateMaintainPermission), Order = 1)]
[ServiceFilter(typeof(ValidateAdminWritePermission), Order = 2)]
[ServiceFilter(typeof(AuditLogActionFilter), Order = 3)]
[ApiExplorerSettings(GroupName = "Services")]
public sealed class AdminOperationController(
    LegacyStatisticsRefreshJob statisticsRefreshJob,
    GachaLogExpireService gachaLogExpireService,
    CdnExpireService cdnExpireService,
    CacheService cacheService,
    YaeMetadataService yaeMetadataService,
    ISchedulerFactory schedulerFactory) : ControllerBase
{
    /// <summary>
    /// Runs a Quartz job immediately instead of waiting for its cron schedule.
    /// </summary>
    /// <param name="name">The job name, which is the class name of the job implementation.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The operation result.</returns>
    [HttpPost("jobs/{name}/run")]
    public async Task<IActionResult> RunJobAsync([FromRoute] string name, CancellationToken cancellationToken)
    {
        IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken).ConfigureAwait(false);
        IReadOnlyCollection<JobKey> jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken).ConfigureAwait(false);

        JobKey? jobKey = jobKeys.FirstOrDefault(key => string.Equals(key.Name, name, StringComparison.Ordinal));
        if (jobKey is null)
        {
            return Fail("Job does not exist", ReturnCode.NotFound, ServerKeys.ServerAdminJobNotFound);
        }

        await scheduler.TriggerJob(jobKey, null, cancellationToken).ConfigureAwait(false);
        return Success($"Job {jobKey.Name} triggered", ServerKeys.ServerAdminJobTriggered);
    }

    /// <summary>
    /// Triggers a manual refresh of the abyss statistics.
    /// </summary>
    /// <returns>The operation result.</returns>
    [HttpPost("statistics/refresh")]
    public async Task<IActionResult> RunStatisticsAsync()
    {
        await statisticsRefreshJob.Execute(default!).ConfigureAwait(false);
        return Success("Operation succeeded", ServerKeys.ServerAdminOperationSucceed);
    }

    /// <summary>
    /// Extends the gacha log service term for every user.
    /// </summary>
    /// <param name="days">The number of days to extend.</param>
    /// <returns>The operation result.</returns>
    [HttpPost("gacha-logs/compensations")]
    public async Task<IActionResult> GachaLogCompensationAsync([FromQuery] int days)
    {
        DateTimeOffset target = await gachaLogExpireService.ExtendTermForAllUsersAsync(days).ConfigureAwait(false);
        return Success($"Operation succeeded, extended {days} days, expires at: {target}");
    }

    /// <summary>
    /// Extends the gacha log service term for a specific user.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <param name="days">The number of days to extend.</param>
    /// <returns>The operation result.</returns>
    [HttpPost("gacha-logs/compensations/designation")]
    public async Task<IActionResult> GachaLogDesignationAsync([FromQuery] string userName, [FromQuery] int days)
    {
        TermExtendResult result = await gachaLogExpireService.ExtendTermForUserNameAsync(userName, days).ConfigureAwait(false);
        return result.Kind switch
        {
            TermExtendResultKind.Ok => Success($"Operation succeeded, extended {days} days, expires at: {result.ExpiredAt}"),
            TermExtendResultKind.NoSuchUser => Fail("User name does not exist", ReturnCode.UserNameNotExists, ServerKeys.ServerAdminUserNameNotFound),
            _ => Fail("Database error", ReturnCode.GachaLogExtendDbException, ServerKeys.ServerDatabaseError),
        };
    }

    /// <summary>
    /// Extends the CDN service term for every user.
    /// </summary>
    /// <param name="days">The number of days to extend.</param>
    /// <returns>The operation result.</returns>
    [HttpPost("cdn/compensations")]
    public async Task<IActionResult> DistributionCompensationAsync([FromQuery] int days)
    {
        DateTimeOffset target = await cdnExpireService.ExtendTermForAllUsersAsync(days).ConfigureAwait(false);
        return Success($"Operation succeeded, extended {days} days, expires at: {target}");
    }

    /// <summary>
    /// Extends the CDN service term for a specific user.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <param name="days">The number of days to extend.</param>
    /// <returns>The operation result.</returns>
    [HttpPost("cdn/compensations/designation")]
    public async Task<IActionResult> DistributionDesignationAsync([FromQuery] string userName, [FromQuery] int days)
    {
        TermExtendResult result = await cdnExpireService.ExtendTermForUserNameAsync(userName, days).ConfigureAwait(false);
        return result.Kind switch
        {
            TermExtendResultKind.Ok => Success($"Operation succeeded, extended {days} days, expires at: {result.ExpiredAt}"),
            TermExtendResultKind.NoSuchUser => Fail("User name does not exist", ReturnCode.UserNameNotExists, ServerKeys.ServerAdminUserNameNotFound),
            _ => Fail("Database error", ReturnCode.CdnExtendDbException, ServerKeys.ServerDatabaseError),
        };
    }

    /// <summary>
    /// Invalidates cached entries by exact keys and/or glob patterns.
    /// </summary>
    /// <param name="request">The cache keys and patterns to remove.</param>
    /// <returns>The removal result.</returns>
    [HttpDelete("cache")]
    public async Task<IActionResult> InvalidateCacheAsync([FromBody] CacheInvalidationRequest request)
    {
        CacheInvalidationResult result = new()
        {
            RemovedKeys = await cacheService.RemoveKeysAsync(request.Keys).ConfigureAwait(false),
            RemovedByPattern = await cacheService.RemoveByPatternsAsync(request.Patterns).ConfigureAwait(false),
        };

        return Success("Cache invalidation succeeded", result, ServerKeys.ServerAdminCacheInvalidationSucceed);
    }

    /// <summary>
    /// Lists cache keys matching the given glob pattern for the admin UI.
    /// </summary>
    /// <param name="pattern">The glob pattern; defaults to <c>*</c>.</param>
    /// <param name="limit">The maximum number of keys to return; defaults to 500.</param>
    /// <returns>The logical cache key list.</returns>
    [HttpGet("cache/keys")]
    public IActionResult ListCacheKeys([FromQuery] string pattern = "*", [FromQuery] int limit = 500)
    {
        List<string> keys = cacheService.ListKeys(pattern, limit);
        return Success("Cache keys acquired successfully", keys, ServerKeys.ServerAdminCacheKeysSucceed);
    }

    /// <summary>
    /// Pulls the protobuf metadata from the Yae metadata bucket, converts it into the achievement injection configuration,
    /// and stores it; the data is then served by <c>/api/client/features/AchievementFieldId_{version}</c>.
    /// </summary>
    /// <param name="chineseHash">
    /// The CN client executable fingerprint to force for this sync (decimal or 0x-prefixed hexadecimal).
    /// When omitted, the fingerprints configured in the admin panel (Yae.ChineseGameHashes) are used; it is usually required for the first sync of a new major version.
    /// </param>
    /// <param name="token">The request cancellation token.</param>
    /// <returns>The sync result, including both detected fingerprints and the item count.</returns>
    [HttpPost("yae/metadata")]
    public async Task<IActionResult> YaeSyncMetadataAsync([FromQuery] string? chineseHash = null, CancellationToken token = default)
    {
        if (!TryParseHash(chineseHash, out uint? forced))
        {
            return Fail($"chineseHash is not a valid hash: {chineseHash}", ReturnCode.InvalidQueryString);
        }

        YaeBucketDecoder.Result result = await yaeMetadataService.SyncAsync(forced, token).ConfigureAwait(false);
        return result.Succeeded
            ? Success($"Yae metadata synced for game version {result.Version}", result)
            : Fail(result.Error!, ReturnCode.InternalStateException);
    }

    /// <summary>
    /// Lists the synced Yae achievement injection configuration versions.
    /// </summary>
    /// <param name="token">The request cancellation token.</param>
    /// <returns>The version and fingerprint pairs.</returns>
    [HttpGet("yae/metadata")]
    public async Task<IActionResult> YaeMetadataListAsync(CancellationToken token)
    {
        List<YaeMetadataService.Summary> items = await yaeMetadataService.ListAsync(token).ConfigureAwait(false);
        return Success("Yae metadata list acquired successfully", items);
    }

    /// <summary>
    /// Parses a fingerprint hash given either as a decimal number or as 0x-prefixed hexadecimal.
    /// </summary>
    /// <param name="raw">The raw value; blank values are accepted as "not specified".</param>
    /// <param name="hash">The parsed hash, or <see langword="null"/> when the value was blank.</param>
    /// <returns><see langword="true"/> if the value is blank or parses successfully; otherwise, <see langword="false"/>.</returns>
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
