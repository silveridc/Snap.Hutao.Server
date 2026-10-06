using server.Models.Entity.GachaLog;
using server.Models.GachaLog;
using server.Models.Metadata;

namespace server.Controllers;

[ApiController]
[Route("[controller]")]
[ApiExplorerSettings(GroupName = "GachaLog")]
public class GachaLogController(GachaLogService gachaLogService) : ControllerBase
{
    /// <summary>
    /// 获取当前祈愿活动的统计概览。
    /// </summary>
    /// <returns>祈愿活动统计数据。</returns>
    [HttpGet("Statistics/CurrentEventStatistics")]
    public IActionResult CurrentEventStatistics()
    {
        return GetGachaLogStatistics<GachaEventStatistics>(GachaStatistics.GachaEventStatistics);
    }

    /// <summary>
    /// 获取角色活动祈愿的分布统计数据。
    /// </summary>
    /// <returns>角色活动祈愿分布数据。</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpGet("Statistics/Distribution/AvatarEvent")]
    public IActionResult AvatarEventDistribution()
    {
        return GetGachaLogStatistics<GachaDistribution>(GachaStatistics.AvaterEventGachaDistribution);
    }

    /// <summary>
    /// 获取武器活动祈愿的分布统计数据。
    /// </summary>
    /// <returns>武器活动祈愿分布数据。</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpGet("Statistics/Distribution/WeaponEvent")]
    public IActionResult WeaponEventDistribution()
    {
        return GetGachaLogStatistics<GachaDistribution>(GachaStatistics.WeaponEventGachaDistribution);
    }

    /// <summary>
    /// 获取集录祈愿的分布统计数据。
    /// </summary>
    /// <returns>集录祈愿分布数据。</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpGet("Statistics/Distribution/Chronicled")]
    public IActionResult ChronicledDistribution()
    {
        return GetGachaLogStatistics<GachaDistribution>(GachaStatistics.ChronicledGachaDistribution);
    }

    /// <summary>
    /// 获取常驻祈愿的分布统计数据。
    /// </summary>
    /// <returns>常驻祈愿分布数据。</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpGet("Statistics/Distribution/Standard")]
    public IActionResult StandardDistribution()
    {
        return GetGachaLogStatistics<GachaDistribution>(GachaStatistics.StandardGachaDistribution);
    }

    /// <summary>
    /// 获取当前用户的祈愿记录 Entry 列表。
    /// </summary>
    /// <returns>祈愿记录 Entry 列表。</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpGet("Entries")]
    public async Task<IActionResult> GetGachaEntriesAsync()
    {
        List<GachaEntry> entries = await gachaLogService.GetGachaEntriesForUserAsync(this.GetUserId()).ConfigureAwait(false);
        return Response<List<GachaEntry>>.Success("Entries acquired successfully", ServerKeys.ServerGachaLogGetEntriesSucceed, entries);
    }

    /// <summary>
    /// 获取指定 Uid 最新的祈愿记录结束 ID。
    /// </summary>
    /// <param name="uid">游戏 Uid。</param>
    /// <returns>最新的 EndIds。</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpGet("EndIds")]
    public async Task<IActionResult> GetEndIdsAsync([FromQuery(Name = "Uid")] string uid)
    {
        EndIds endIds = await gachaLogService.GetNewestEndIdsAsync(this.GetUserId(), uid).ConfigureAwait(false);
        return Response<EndIds>.Success("EndIds acquired successfully", ServerKeys.ServerGachaLogGetEndIdsSucceed, endIds);
    }

    /// <summary>
    /// 根据 Uid 与 EndIds 获取更早的祈愿记录。
    /// </summary>
    /// <param name="uidAndEndIds">包含 Uid 与 EndIds 的请求体。</param>
    /// <returns>祈愿记录列表。</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpPost("Retrieve")]
    public async Task<IActionResult> RetrieveAsync([FromBody] UidAndEndIds uidAndEndIds)
    {
        int userId = this.GetUserId();
        List<SimpleGachaItem> gachaItems = await gachaLogService.GetGachaItemsEarlyThanEndIdsAsync(userId, uidAndEndIds).ConfigureAwait(false);
        return Response<List<SimpleGachaItem>>.Success(userId.ToString(), gachaItems);
    }

    /// <summary>
    /// 按条件有限获取更早的祈愿记录。
    /// </summary>
    /// <param name="uid">游戏 Uid。</param>
    /// <param name="configType">祈愿记录配置类型。</param>
    /// <param name="endId">起始结束 ID，默认为最大值。</param>
    /// <param name="count">获取数量，默认 20。</param>
    /// <returns>祈愿记录列表。</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpGet("LimitedRetrieve")]
    public async Task<IActionResult> LimitedRetrieveAsync([FromQuery(Name = "uid")] string uid, [FromQuery(Name = "configType")] int configType, [FromQuery(Name = "endId")] long endId = long.MaxValue, [FromQuery(Name = "count")] int count = 20)
    {
        int userId = this.GetUserId();
        List<SimpleGachaItem> gachaItems = await gachaLogService.GetLimitedGachaItemsEarlyThanEndIdsAsync(userId, uid, (GachaConfigType)configType, endId, count).ConfigureAwait(false);
        return Response<List<SimpleGachaItem>>.Success(userId.ToString(), gachaItems);
    }

    /// <summary>
    /// 上传祈愿记录数据。
    /// </summary>
    /// <param name="uidAndItems">包含 Uid 与祈愿记录数据的请求体。</param>
    /// <returns>上传保存结果。</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpPost("Upload")]
    public async Task<IActionResult> UploadAsync([FromBody] UidAndItems uidAndItems)
    {
        int userId = this.GetUserId();
        GachaLogSaveResult result = await gachaLogService.SaveGachaItemsAsync(userId, uidAndItems).ConfigureAwait(false);

        return result.Kind switch
        {
            GachaLogSaveResultKind.Ok => Response<GachaLogSaveResult>.Success($"Uploaded {uidAndItems.Items.Count} items of Uid {uidAndItems.Uid}, stored {result.SaveCount} items", result),
            GachaLogSaveResultKind.UidPerUserLimitExceeded => Models.Response.Response.Fail(ReturnCode.TooManyGachaLogUids, "A Snap Hutao account can store gacha logs of at most 5 Uids", ServerKeys.ServerGachaLogServiceInsufficientRecordSlot),
            GachaLogSaveResultKind.InvalidGachaItemDetected => Models.Response.Response.Fail(ReturnCode.InvalidGachaLogItems, "Invalid data, unable to save to the cloud", ServerKeys.ServerGachaLogServiceInvalidGachaLogData),
            GachaLogSaveResultKind.DatebaseOperationFailed => Models.Response.Response.Fail(ReturnCode.GachaLogDbException, "Data error, unable to save to the cloud", ServerKeys.ServerGachaLogServiceServerDatabaseError),
            _ => throw new InvalidOperationException(),
        };
    }

    /// <summary>
    /// 删除指定 Uid 的祈愿记录。
    /// </summary>
    /// <param name="uid">游戏 Uid。</param>
    /// <returns>删除的记录条数。</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpGet("Delete")]
    public async Task<IActionResult> DeleteAsync([FromQuery(Name = "Uid")] string uid)
    {
        int count = await gachaLogService.DeleteGachaItemsAsync(this.GetUserId(), uid).ConfigureAwait(false);
        return Response<int>.Success($"Deleted {count} records", count);
    }

    /// <summary>
    /// 根据名称获取指定类型的祈愿统计数据。
    /// </summary>
    /// <typeparam name="T">统计数据类型。</typeparam>
    /// <param name="name">统计数据缓存名称。</param>
    /// <returns>统计数据。</returns>
    private IActionResult GetGachaLogStatistics<T>(string name)
        where T : class
    {
        T? data = gachaLogService.GetGachaLogStatistics<T>(name);

        if (data is null)
        {
            return Models.Response.Response.Fail(
                ReturnCode.ComputingStatistics,
                "Statistics are being computed or do not exist",
                ServerKeys.ServerGachaLogStatisticsUnavailable
            );
        }

        return Response<T>.Success("Gacha log statistics acquired successfully", ServerKeys.ServerGachaLogStatisticsSucceed, data);
    }
}