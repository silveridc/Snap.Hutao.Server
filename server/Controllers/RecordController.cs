using server.Models.Legacy;
using server.Models.Upload;

namespace server.Controllers;

[ApiController]
[Route("api/records")]
[ApiExplorerSettings(GroupName = "SpiralAbyss")]
public class RecordController(RecordService recordService, IRankService rankService) : ControllerBase
{
    /// <summary>
    /// Uploads a Spiral Abyss record.
    /// </summary>
    /// <param name="record">The Spiral Abyss record data.</param>
    /// <param name="returningRank">Whether to return the leaderboard data of the UID instead of the upload result.</param>
    /// <returns>The upload result or the leaderboard data.</returns>
    [HttpPost("abyss")]
    public async Task<IActionResult> ProcessUploadRecordAsync([FromBody] SimpleRecord record, [FromQuery] bool returningRank = false)
    {
        RecordUploadResult result = await recordService.ProcessUploadAsync(record).ConfigureAwait(false);

        if (returningRank && result < RecordUploadResult.None)
        {
            Rank rank = await rankService.RetriveRankAsync(record.Uid!).ConfigureAwait(false);
            return Success<Rank>("Rank data acquired successfully", rank, ServerKeys.ServerRecordRankSucceed);
        }
        else
        {
            return result switch
            {
                RecordUploadResult.OkWithNotFirstAttempt => Success("Data uploaded successfully, but this is not the first upload of the current schedule; no gacha log service time granted", ServerKeys.ServerRecordUploadSuccessButNotFirstTimeAtCurrentSchedule),
                RecordUploadResult.OkWithNoUserNamePresented => Success("Data uploaded successfully, but no Snap Hutao account is bound; no gacha log service time granted", ServerKeys.ServerRecordUploadSuccessButNoPassport),
                RecordUploadResult.OkWithGachaLogExtended => Success("Data uploaded successfully, gacha log service time granted", ServerKeys.ServerRecordUploadSuccessAndGachaLogServiceTimeExtended),
                RecordUploadResult.OkWithGachaLogNoSuchUser => Success("Data uploaded successfully, but the Snap Hutao account does not exist", ServerKeys.ServerRecordUploadSuccessButNoSuchUser),

                RecordUploadResult.ComputingStatistics => Fail("Computing statistics", ReturnCode.ComputingStatistics, ServerKeys.ServerRecordComputingStatistics),
                RecordUploadResult.UidBanned => Fail("Uid has been banned", ReturnCode.BannedUid, ServerKeys.ServerRecordBannedUid),
                RecordUploadResult.NotCurrentSchedule => Fail("Not current Spiral Abyss schedule data", ReturnCode.NotCurrentSchedule, ServerKeys.ServerRecordNotCurrentSchedule),
                RecordUploadResult.InvalidData => Fail("Invalid upload data", ReturnCode.InvalidUploadData, ServerKeys.ServerRecordInvalidData),
                RecordUploadResult.ConcurrencyNotSupported => Fail("A previous request for this Uid is still being processed", ReturnCode.PreviousRequestNotCompleted, ServerKeys.ServerRecordPreviousRequestNotCompleted),
                _ => Success("Data uploaded successfully", ServerKeys.ServerRecordUploadSucceed),
            };
        }
    }

    /// <summary>
    /// Checks whether the specified UID has uploaded a record.
    /// </summary>
    /// <param name="uid">The game UID.</param>
    /// <returns>Whether the UID has uploaded a record.</returns>
    [HttpGet("abyss/{uid}/status")]
    public async Task<IActionResult> CheckUidUploadedAsync([FromRoute] string uid)
    {
        if (recordService.IsStatisticsServiceWorking())
        {
            return Fail("Computing statistics", ReturnCode.ComputingStatistics, ServerKeys.ServerRecordComputingStatistics2);
        }

        if (!int.TryParse(uid, out _) || uid.Length < 9)
        {
            return Fail($"{uid} is not a valid Uid", ReturnCode.InvalidQueryString);
        }

        bool uploaded = await recordService.HaveUidUploadedAsync(uid).ConfigureAwait(false);
        return Success<bool>("Query succeeded", uploaded, ServerKeys.ServerRecordCheckSucceed);
    }

    /// <summary>
    /// Gets the leaderboard data of the specified UID.
    /// </summary>
    /// <param name="uid">The game UID.</param>
    /// <returns>The leaderboard data.</returns>
    [HttpGet("abyss/{uid}/rank")]
    public async Task<IActionResult> RankAsync([FromRoute] string uid)
    {
        if (recordService.IsStatisticsServiceWorking())
        {
            return Fail("Computing statistics", ReturnCode.ComputingStatistics, ServerKeys.ServerRecordComputingStatistics2);
        }

        if (!int.TryParse(uid, out _) || uid.Length < 9)
        {
            return Fail($"{uid} is not a valid Uid", ReturnCode.InvalidQueryString);
        }

        Rank rank = await rankService.RetriveRankAsync(uid).ConfigureAwait(false);
        return Success<Rank>("Rank data acquired successfully", rank, ServerKeys.ServerRecordRankSucceed);
    }
}
