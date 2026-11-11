using server.Models.Entity.Telemetry;
using server.Models.Upload;

namespace server.Controllers;

[ApiController]
[Route("api/telemetry-logs")]
[ApiExplorerSettings(IgnoreApi = true)]
public class HutaoLogController(TelemetryService telemetryService) : ControllerBase
{
    /// <summary>
    /// Uploads a Hutao log.
    /// </summary>
    /// <param name="uploadLog">The log data to upload.</param>
    /// <returns>The log processing result.</returns>
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
    /// Gets the latest log records of the given device ID.
    /// </summary>
    /// <param name="deviceId">The device ID.</param>
    /// <returns>The log record list.</returns>
    [HttpGet("{deviceId}")]
    public async Task<IActionResult> LatestByDeviceId([FromRoute] string deviceId)
    {
        return Success<List<HutaoLog>>("Logs acquired successfully", await telemetryService.GetLogsByDeviceId(deviceId).ConfigureAwait(false), ServerKeys.ServerHutaoLogGetRecordsSucceed);
    }
}
