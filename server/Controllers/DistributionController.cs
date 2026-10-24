namespace server.Controllers;

[ApiController]
[Route("[controller]")]
[ApiExplorerSettings(GroupName = "Distribution")]
public class DistributionController(DistributionService distributionService) : ControllerBase
{
    [Authorize]
    [ServiceFilter(typeof(ValidateCdnPermission))]
    /// <summary>
    /// 获取下载令牌。
    /// </summary>
    /// <returns>下载令牌。</returns>
    [HttpGet("GetDownloadToken")]
    public async Task<IActionResult> GetDownloadToken()
    {
        string? token = await distributionService.GetDownloadTokenAsync();
        if (token is null)
        {
            return Fail("Failed to acquire download token", ReturnCode.CdnDispatcherException, ServerKeys.ServerCdnDownloadTokenFailed);
        }

        return Success<string>("Download token acquired successfully", token, ServerKeys.ServerCdnDownloadTokenSucceed);
    }

    [Authorize]
    [ServiceFilter(typeof(ValidateCdnPermission))]
    /// <summary>
    /// 获取加速镜像地址。
    /// </summary>
    /// <param name="filename">需要下载的文件名。</param>
    /// <returns>加速镜像信息。</returns>
    [HttpGet("GetAcceleratedMirror")]
    public async Task<IActionResult> GetAcceleratedMirror([FromQuery(Name = "Filename")] string filename)
    {
        HutaoPackageMirror? mirror = await distributionService.GetAcceleratedMirrorAsync(filename);
        if (mirror is null)
        {
            return Fail("Failed to acquire accelerated mirror", ReturnCode.CdnDispatcherException, ServerKeys.ServerCdnMirrorFailed);
        }

        return Success<HutaoPackageMirror?>("Accelerated mirror acquired successfully", mirror, ServerKeys.ServerCdnMirrorSucceed);
    }
}