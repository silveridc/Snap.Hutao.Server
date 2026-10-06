using server.Models.Legacy;
using server.Models.Response;
using server.Models.Upload;
using server.Services.Legacy;
using server.Services.Ranking;

namespace server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [ApiExplorerSettings(GroupName = "SpiralAbyss")]
    public class RecordController(RecordService recordService, IRankService rankService) : ControllerBase
    {
        /// <summary>
        /// 上传深渊记录。
        /// </summary>
        /// <param name="record">深渊记录数据。</param>
        /// <param name="returningRank">是否返回排行数据。</param>
        /// <returns>上传结果或排行数据。</returns>
        [HttpPost("Upload")]
        public async Task<IActionResult> ProcessUploadRecordAsync([FromBody] SimpleRecord record, [FromQuery] bool returningRank = false)
        {
            RecordUploadResult result = await recordService.ProcessUploadAsync(record).ConfigureAwait(false);

            if (returningRank && result < RecordUploadResult.None)
            {
                Rank rank = await rankService.RetriveRankAsync(record.Uid!).ConfigureAwait(false);
                return Response<Rank>.Success("Rank data acquired successfully", ServerKeys.ServerRecordRankSucceed, rank);
            }
            else
            {
                return result switch
                {
                    RecordUploadResult.OkWithNotFirstAttempt => Models.Response.Response.Success("Data uploaded successfully, but this is not the first upload of the current schedule; no gacha log service time granted", ServerKeys.ServerRecordUploadSuccessButNotFirstTimeAtCurrentSchedule),
                    RecordUploadResult.OkWithNoUserNamePresented => Models.Response.Response.Success("Data uploaded successfully, but no Snap Hutao account is bound; no gacha log service time granted", ServerKeys.ServerRecordUploadSuccessButNoPassport),
                    RecordUploadResult.OkWithGachaLogExtended => Models.Response.Response.Success("Data uploaded successfully, gacha log service time granted", ServerKeys.ServerRecordUploadSuccessAndGachaLogServiceTimeExtended),
                    RecordUploadResult.OkWithGachaLogNoSuchUser => Models.Response.Response.Success("Data uploaded successfully, but the Snap Hutao account does not exist", ServerKeys.ServerRecordUploadSuccessButNoSuchUser),

                    RecordUploadResult.ComputingStatistics => Models.Response.Response.Fail(ReturnCode.ComputingStatistics, "Computing statistics", ServerKeys.ServerRecordComputingStatistics),
                    RecordUploadResult.UidBanned => Models.Response.Response.Fail(ReturnCode.BannedUid, "Uid has been banned", ServerKeys.ServerRecordBannedUid),
                    RecordUploadResult.NotCurrentSchedule => Models.Response.Response.Fail(ReturnCode.NotCurrentSchedule, "Not current Spiral Abyss schedule data", ServerKeys.ServerRecordNotCurrentSchedule),
                    RecordUploadResult.InvalidData => Models.Response.Response.Fail(ReturnCode.InvalidUploadData, "Invalid upload data", ServerKeys.ServerRecordInvalidData),
                    RecordUploadResult.ConcurrencyNotSupported => Models.Response.Response.Fail(ReturnCode.PreviousRequestNotCompleted, "A previous request for this Uid is still being processed", ServerKeys.ServerRecordPreviousRequestNotCompleted),
                    _ => Models.Response.Response.Success("Data uploaded successfully", ServerKeys.ServerRecordUploadSucceed),
                };
            }
        }

        /// <summary>
        /// 查询指定 Uid 是否已上传。
        /// </summary>
        /// <param name="uid">游戏 Uid。</param>
        /// <returns>是否已上传。</returns>
        [HttpGet("Check")]
        public async Task<IActionResult> CheckUidUploadedAsync([FromQuery(Name = "Uid")] string uid)
        {
            if (recordService.IsStatisticsServiceWorking())
            {
                return Models.Response.Response.Fail(ReturnCode.ComputingStatistics, "Computing statistics", ServerKeys.ServerRecordComputingStatistics2);
            }

            if (!int.TryParse(uid, out _) || uid.Length < 9)
            {
                return Models.Response.Response.Fail(ReturnCode.InvalidQueryString, $"{uid} is not a valid Uid");
            }

            bool uploaded = await recordService.HaveUidUploadedAsync(uid).ConfigureAwait(false);
            return Response<bool>.Success("Query succeeded", ServerKeys.ServerRecordCheckSucceed, uploaded);
        }

        /// <summary>
        /// 获取指定 Uid 的排行数据。
        /// </summary>
        /// <param name="uid">游戏 Uid。</param>
        /// <returns>排行数据。</returns>
        [HttpGet("Rank")]
        public async Task<IActionResult> RankAsync([FromQuery(Name = "Uid")] string uid)
        {
            if (recordService.IsStatisticsServiceWorking())
            {
                return Models.Response.Response.Fail(ReturnCode.ComputingStatistics, "Computing statistics", ServerKeys.ServerRecordComputingStatistics2);
            }

            if (!int.TryParse(uid, out _) || uid.Length < 9)
            {
                return Models.Response.Response.Fail(ReturnCode.InvalidQueryString, $"{uid} is not a valid Uid");
            }

            Rank rank = await rankService.RetriveRankAsync(uid).ConfigureAwait(false);
            return Response<Rank>.Success("Rank data acquired successfully", ServerKeys.ServerRecordRankSucceed, rank);
        }
    }
}