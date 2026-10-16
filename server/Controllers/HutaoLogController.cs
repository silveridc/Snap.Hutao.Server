using server.Models.Entity.Telemetry;
using server.Models.Upload;

namespace server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class HutaoLogController(TelemetryService telemetryService) : ControllerBase
    {
        /// <summary>
        /// 上传胡桃日志。
        /// </summary>
        /// <param name="uploadLog">待上传的日志数据。</param>
        /// <returns>日志处理结果。</returns>
        [HttpPost("Upload")]
        public async Task<IActionResult> ProcessUploadLog([FromBody] HutaoUploadLog uploadLog)
        {
            return await telemetryService.ProcessCrashLogAsync(uploadLog, Request.Headers.UserAgent!) switch
            {
                CrashLogProcessResult.Ok => Models.Response.Response.Success("Log uploaded successfully", ServerKeys.ServerHutaoLogUploadSucceed),
                _ => Models.Response.Response.Fail(ReturnCode.InvalidUploadData, "Incomplete data", ServerKeys.ServerHutaoLogIncompleteData),
            };
        }

        /// <summary>
        /// 按设备 ID 获取最新日志记录。
        /// </summary>
        /// <param name="deviceId">设备 ID。</param>
        /// <returns>日志记录列表。</returns>
        [HttpGet("ByDeviceId")]
        public async Task<IActionResult> LatestByDeviceId([FromQuery(Name = "id")] string deviceId)
        {
            return Response<List<HutaoLog>>.Success("Logs acquired successfully", ServerKeys.ServerHutaoLogGetRecordsSucceed, await telemetryService.GetLogsByDeviceId(deviceId).ConfigureAwait(false));
        }
    }
}