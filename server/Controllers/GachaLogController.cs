using server.Filter;
using server.Models.Entity.GachaLog;
using server.Models.GachaLog;
using server.Models.Metadata;

namespace server.Controllers;

[ApiController]
[Route("api/gacha-logs")]
[ApiExplorerSettings(GroupName = "GachaLog")]
public class GachaLogController(GachaLogService gachaLogService) : ControllerBase
{
    /// <summary>
    /// Gets the statistics overview of the current gacha event.
    /// </summary>
    /// <returns>The gacha event statistics.</returns>
    [HttpGet("statistics/current-event")]
    public IActionResult CurrentEventStatistics()
    {
        return GetGachaLogStatistics<GachaEventStatistics>(GachaStatistics.GachaEventStatistics);
    }

    /// <summary>
    /// Gets the distribution statistics of the character event wish.
    /// </summary>
    /// <returns>The character event wish distribution data.</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpGet("statistics/distributions/avatar-event")]
    public IActionResult AvatarEventDistribution()
    {
        return GetGachaLogStatistics<GachaDistribution>(GachaStatistics.AvaterEventGachaDistribution);
    }

    /// <summary>
    /// Gets the distribution statistics of the weapon event wish.
    /// </summary>
    /// <returns>The weapon event wish distribution data.</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpGet("statistics/distributions/weapon-event")]
    public IActionResult WeaponEventDistribution()
    {
        return GetGachaLogStatistics<GachaDistribution>(GachaStatistics.WeaponEventGachaDistribution);
    }

    /// <summary>
    /// Gets the distribution statistics of the chronicled wish.
    /// </summary>
    /// <returns>The chronicled wish distribution data.</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpGet("statistics/distributions/chronicled")]
    public IActionResult ChronicledDistribution()
    {
        return GetGachaLogStatistics<GachaDistribution>(GachaStatistics.ChronicledGachaDistribution);
    }

    /// <summary>
    /// Gets the distribution statistics of the standard wish.
    /// </summary>
    /// <returns>The standard wish distribution data.</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpGet("statistics/distributions/standard")]
    public IActionResult StandardDistribution()
    {
        return GetGachaLogStatistics<GachaDistribution>(GachaStatistics.StandardGachaDistribution);
    }

    /// <summary>
    /// Gets the gacha record entries of the current user.
    /// </summary>
    /// <returns>The gacha record entry list.</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpGet("entries")]
    public async Task<IActionResult> GetGachaEntriesAsync()
    {
        List<GachaEntry> entries = await gachaLogService.GetGachaEntriesForUserAsync(this.GetUserId()).ConfigureAwait(false);
        return Success<List<GachaEntry>>("Entries acquired successfully", entries, ServerKeys.ServerGachaLogGetEntriesSucceed);
    }

    /// <summary>
    /// Gets the newest gacha record end IDs of the specified UID.
    /// </summary>
    /// <param name="uid">The game UID.</param>
    /// <returns>The newest end IDs.</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpGet("end-ids")]
    public async Task<IActionResult> GetEndIdsAsync([FromQuery(Name = "Uid")] string uid)
    {
        EndIds endIds = await gachaLogService.GetNewestEndIdsAsync(this.GetUserId(), uid).ConfigureAwait(false);
        return Success<EndIds>("EndIds acquired successfully", endIds, ServerKeys.ServerGachaLogGetEndIdsSucceed);
    }

    /// <summary>
    /// Gets gacha records older than the given end IDs.
    /// </summary>
    /// <param name="uidAndEndIds">The request body containing the UID and the end IDs.</param>
    /// <returns>The gacha record list.</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpPost("retrievals")]
    public async Task<IActionResult> RetrieveAsync([FromBody] UidAndEndIds uidAndEndIds)
    {
        int userId = this.GetUserId();
        List<SimpleGachaItem> gachaItems = await gachaLogService.GetGachaItemsEarlyThanEndIdsAsync(userId, uidAndEndIds).ConfigureAwait(false);
        return Success<List<SimpleGachaItem>>(userId.ToString(), gachaItems);
    }

    /// <summary>
    /// Gets a limited number of older gacha records filtered by the given conditions.
    /// </summary>
    /// <param name="uid">The game UID.</param>
    /// <param name="configType">The gacha record configuration type.</param>
    /// <param name="endId">The starting end ID; defaults to the maximum value.</param>
    /// <param name="count">The number of records to return; defaults to 20.</param>
    /// <returns>The gacha record list.</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpGet("limited-retrievals")]
    public async Task<IActionResult> LimitedRetrieveAsync([FromQuery(Name = "uid")] string uid, [FromQuery(Name = "configType")] int configType, [FromQuery(Name = "endId")] long endId = long.MaxValue, [FromQuery(Name = "count")] int count = 20)
    {
        int userId = this.GetUserId();
        List<SimpleGachaItem> gachaItems = await gachaLogService.GetLimitedGachaItemsEarlyThanEndIdsAsync(userId, uid, (GachaConfigType)configType, endId, count).ConfigureAwait(false);
        return Success<List<SimpleGachaItem>>(userId.ToString(), gachaItems);
    }

    /// <summary>
    /// Uploads gacha record data.
    /// </summary>
    /// <param name="uidAndItems">The request body containing the UID and the gacha record data.</param>
    /// <returns>The upload and save result.</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpPost]
    public async Task<IActionResult> UploadAsync([FromBody] UidAndItems uidAndItems)
    {
        int userId = this.GetUserId();
        GachaLogSaveResult result = await gachaLogService.SaveGachaItemsAsync(userId, uidAndItems).ConfigureAwait(false);

        return result.Kind switch
        {
            GachaLogSaveResultKind.Ok => Success<GachaLogSaveResult>($"Uploaded {uidAndItems.Items.Count} items of Uid {uidAndItems.Uid}, stored {result.SaveCount} items", result),
            GachaLogSaveResultKind.UidPerUserLimitExceeded => Fail($"A Snap Hutao account can store gacha logs of at most {gachaLogService.MaxUidPerUser} Uids", ReturnCode.TooManyGachaLogUids, ServerKeys.ServerGachaLogServiceInsufficientRecordSlot),
            GachaLogSaveResultKind.InvalidGachaItemDetected => Fail("Invalid data, unable to save to the cloud", ReturnCode.InvalidGachaLogItems, ServerKeys.ServerGachaLogServiceInvalidGachaLogData),
            GachaLogSaveResultKind.DatebaseOperationFailed => Fail("Data error, unable to save to the cloud", ReturnCode.GachaLogDbException, ServerKeys.ServerGachaLogServiceServerDatabaseError),
            _ => throw new InvalidOperationException(),
        };
    }

    /// <summary>
    /// Deletes the gacha records of the specified UID.
    /// </summary>
    /// <param name="uid">The game UID.</param>
    /// <returns>The number of deleted records.</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateGachaLogPermission))]
    [HttpDelete("{uid}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] string uid)
    {
        int count = await gachaLogService.DeleteGachaItemsAsync(this.GetUserId(), uid).ConfigureAwait(false);
        return Success<int>($"Deleted {count} records", count);
    }

    /// <summary>
    /// Gets the gacha statistics of the given type by statistics cache name.
    /// </summary>
    /// <typeparam name="T">The statistics data type.</typeparam>
    /// <param name="name">The statistics cache name.</param>
    /// <returns>The statistics data.</returns>
    private IActionResult GetGachaLogStatistics<T>(string name)
        where T : class
    {
        T? data = gachaLogService.GetGachaLogStatistics<T>(name);

        if (data is null)
        {
            return Fail("Statistics are being computed or do not exist", ReturnCode.ComputingStatistics, ServerKeys.ServerGachaLogStatisticsUnavailable);
        }

        return Success<T>("Gacha log statistics acquired successfully", data, ServerKeys.ServerGachaLogStatisticsSucceed);
    }
}
