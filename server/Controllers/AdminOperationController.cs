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
    YaeMetadataService yaeMetadataService) : ControllerBase
{
    /// <summary>
    /// 手动触发深渊统计数据刷新.
    /// </summary>
    /// <returns>操作结果.</returns>
    [HttpPost("statistics/refresh")]
    public async Task<IActionResult> RunStatisticsAsync()
    {
        await statisticsRefreshJob.Execute(default!).ConfigureAwait(false);
        return Success("Operation succeeded", ServerKeys.ServerAdminOperationSucceed);
    }

    /// <summary>
    /// 为所有用户补偿祈愿记录服务时长.
    /// </summary>
    /// <param name="days">补偿天数.</param>
    /// <returns>操作结果.</returns>
    [HttpPost("gacha-logs/compensations")]
    public async Task<IActionResult> GachaLogCompensationAsync([FromQuery] int days)
    {
        DateTimeOffset target = await gachaLogExpireService.ExtendTermForAllUsersAsync(days).ConfigureAwait(false);
        return Success($"Operation succeeded, extended {days} days, expires at: {target}");
    }

    /// <summary>
    /// 为指定用户补偿祈愿记录服务时长.
    /// </summary>
    /// <param name="userName">用户名.</param>
    /// <param name="days">补偿天数.</param>
    /// <returns>操作结果.</returns>
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
    /// 为所有用户补偿 CDN 服务时长.
    /// </summary>
    /// <param name="days">补偿天数.</param>
    /// <returns>操作结果.</returns>
    [HttpPost("cdn/compensations")]
    public async Task<IActionResult> DistributionCompensationAsync([FromQuery] int days)
    {
        DateTimeOffset target = await cdnExpireService.ExtendTermForAllUsersAsync(days).ConfigureAwait(false);
        return Success($"Operation succeeded, extended {days} days, expires at: {target}");
    }

    /// <summary>
    /// 为指定用户补偿 CDN 服务时长.
    /// </summary>
    /// <param name="userName">用户名.</param>
    /// <param name="days">补偿天数.</param>
    /// <returns>操作结果.</returns>
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
    /// 批量将缓存标记为过期（按精确键与/或 glob 模式移除）.
    /// </summary>
    /// <param name="request">需要移除的缓存键与模式.</param>
    /// <returns>移除结果.</returns>
    [HttpDelete("cache")]
    public async Task<IActionResult> InvalidateCacheAsync([FromBody] CacheInvalidationRequest request)
    {
        CacheInvalidationResult result = new()
        {
            RemovedKeys = await cacheService.RemoveKeysAsync(request.Keys).ConfigureAwait(false),
            RemovedByPattern = await cacheService.RemoveByPatternsAsync(request.Patterns).ConfigureAwait(false),
        };

        return Success<CacheInvalidationResult>("Cache invalidation succeeded", result, ServerKeys.ServerAdminCacheInvalidationSucceed);
    }

    /// <summary>
    /// 列出匹配指定 glob 模式的缓存键，便于管理界面查看.
    /// </summary>
    /// <param name="pattern">glob 模式，默认 <c>*</c>.</param>
    /// <param name="limit">返回上限，默认 500.</param>
    /// <returns>逻辑缓存键列表.</returns>
    [HttpGet("cache/keys")]
    public IActionResult ListCacheKeys([FromQuery] string pattern = "*", [FromQuery] int limit = 500)
    {
        List<string> keys = cacheService.ListKeys(pattern, limit);
        return Success<List<string>>("Cache keys acquired successfully", keys, ServerKeys.ServerAdminCacheKeysSucceed);
    }

    /// <summary>
    /// 从 Yae 的 metadata bucket 拉取 protobuf 并转换成就注入配置，落库后由 <c>/api/client/features/AchievementFieldId_{version}</c> 对外提供.
    /// </summary>
    /// <param name="chineseHash">本次同步强制指定的国服 exe 指纹（十进制或 0x 十六进制）；省略时用 App:Yae:ChineseGameHashes 配置.新大版本第一次同步通常需要传.</param>
    /// <param name="token">请求取消令牌.</param>
    /// <returns>同步结果，含识别出的两个指纹与条目数.</returns>
    [HttpPost("yae/metadata")]
    public async Task<IActionResult> YaeSyncMetadataAsync([FromQuery] string? chineseHash = null, CancellationToken token = default)
    {
        if (!TryParseHash(chineseHash, out uint? forced))
        {
            return Fail($"chineseHash is not a valid hash: {chineseHash}", ReturnCode.InvalidQueryString);
        }

        YaeBucketDecoder.Result result = await yaeMetadataService.SyncAsync(forced, token).ConfigureAwait(false);
        return result.Succeeded
            ? Success<YaeBucketDecoder.Result>($"Yae metadata synced for game version {result.Version}", result)
            : Fail(result.Error!, ReturnCode.InternalStateException);
    }

    /// <summary>
    /// 列出已同步的 Yae 成就注入配置版本.
    /// </summary>
    /// <param name="token">请求取消令牌.</param>
    /// <returns>版本与指纹列表.</returns>
    [HttpGet("yae/metadata")]
    public async Task<IActionResult> YaeMetadataListAsync(CancellationToken token)
    {
        List<YaeMetadataService.Summary> items = await yaeMetadataService.ListAsync(token).ConfigureAwait(false);
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
