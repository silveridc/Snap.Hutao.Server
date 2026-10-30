using server.Models.Entity.Telemetry;
using server.Models.Upload;

namespace server.Controllers;

[ApiController]
[Route("api/telemetry-logs")]
[ApiExplorerSettings(IgnoreApi = true)]
public class HutaoLogController(TelemetryService telemetryService) : ControllerBase
{
    /// <summary>
    /// 上传胡桃日志。
    /// </summary>
    /// <param name="uploadLog">待上传的日志数据。</param>
    /// <returns>日志处理结果。</returns>
    [HttpPost]
    public async Task<IActionResult> ProcessUploadLog([FromBody] HutaoUploadLog uploadLog)
    {
        return await telemetryService.ProcessCrashLogAsync(uploadLog, Request.Headers.UserAgent!) switch
        {
            CrashLogProcessResult.Ok => Success("Log uploaded successfully", ServerKeys.ServerHutaoLogUploadSucceed),
            _ => Fail("Incomplete data", ReturnCode.InvalidUploadData, ServerKeys.ServerHutaoLogIncompleteData),
        };
    }

    /// <summary>
    /// 按设备 ID 获取最新日志记录。
    /// </summary>
    /// <param name="deviceId">设备 ID。</param>
    /// <returns>日志记录列表。</returns>
    [HttpGet("{deviceId}")]
    public async Task<IActionResult> LatestByDeviceId([FromRoute] string deviceId)
    {
        return Success<List<HutaoLog>>("Logs acquired successfully", await telemetryService.GetLogsByDeviceId(deviceId).ConfigureAwait(false), ServerKeys.ServerHutaoLogGetRecordsSucceed);
    }
}
