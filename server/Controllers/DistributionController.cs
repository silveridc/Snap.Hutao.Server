namespace server.Controllers
{
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
                return Models.Response.Response.Fail(ReturnCode.CdnDispatcherException, "Failed to acquire download token", ServerKeys.ServerCdnDownloadTokenFailed);
            }

            return Response<string>.Success("Download token acquired successfully", ServerKeys.ServerCdnDownloadTokenSucceed, token);
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
                return Models.Response.Response.Fail(ReturnCode.CdnDispatcherException, "Failed to acquire accelerated mirror", ServerKeys.ServerCdnMirrorFailed);
            }

            return Response<HutaoPackageMirror?>.Success("Accelerated mirror acquired successfully", ServerKeys.ServerCdnMirrorSucceed, mirror);
        }
    }
}