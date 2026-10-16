using server.Models.Patch;
using server.Services.Admin;

namespace server.Controllers;

[ApiController]
[Route("[controller]")]
[ApiExplorerSettings(GroupName = "Patch")]
public sealed class PatchController(ReleaseService releaseService) : ControllerBase
{
    /// <summary>
    /// 获取 Snap Hutao 客户端最新版本与下载镜像。
    /// </summary>
    /// <returns>更新信息。客户端以 HutaoRuntime.Version 与返回的 version 比较，较大才提示更新。</returns>
    [HttpGet("hutao")]
    public async Task<IActionResult> SnapHutaoAsync()
    {
        HutaoPackageInformation? info = await releaseService.GetCurrentPackageAsync().ConfigureAwait(false);
        if (info is null)
        {
            return Models.Response.Response.Fail(ReturnCode.NotFound, "Update information is not configured yet", ServerKeys.ServerPatchUpdateInfoNotConfigured);
        }

        return Response<HutaoPackageInformation>.Success("Update information acquired successfully", ServerKeys.ServerPatchUpdateInfoSucceed, info);
    }
}
