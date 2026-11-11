using server.Filter;

namespace server.Controllers;

[ApiController]
[Route("api/distribution")]
[ApiExplorerSettings(GroupName = "Distribution")]
public class DistributionController(DistributionService distributionService) : ControllerBase
{
    /// <summary>
    /// Gets a download token.
    /// </summary>
    /// <returns>The download token.</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateCdnPermission))]
    [HttpGet("download-token")]
    public async Task<IActionResult> GetDownloadToken()
    {
        string? token = await distributionService.GetDownloadTokenAsync();
        return token is null ? Fail("Failed to acquire download token", ReturnCode.CdnDispatcherException, ServerKeys.ServerCdnDownloadTokenFailed) : Success<string>("Download token acquired successfully", token, ServerKeys.ServerCdnDownloadTokenSucceed);
    }

    /// <summary>
    /// Gets the accelerated mirror address.
    /// </summary>
    /// <param name="filename">The file name to download.</param>
    /// <returns>The accelerated mirror information.</returns>
    [Authorize]
    [ServiceFilter(typeof(ValidateCdnPermission))]
    [HttpGet("accelerated-mirror")]
    public async Task<IActionResult> GetAcceleratedMirror([FromQuery(Name = "filename")] string filename)
    {
        HutaoPackageMirror? mirror = await distributionService.GetAcceleratedMirrorAsync(filename);
        return mirror is null ? Fail("Failed to acquire accelerated mirror", ReturnCode.CdnDispatcherException, ServerKeys.ServerCdnMirrorFailed) : Success<HutaoPackageMirror?>("Accelerated mirror acquired successfully", mirror, ServerKeys.ServerCdnMirrorSucceed);
    }
}
