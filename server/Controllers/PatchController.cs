using server.Models.Patch;
using server.Services.Admin;

namespace server.Controllers;

[ApiController]
[Route("[controller]")]
[ApiExplorerSettings(GroupName = "Patch")]
public sealed class PatchController(ReleaseService releaseService) : ControllerBase
{
    /// <summary>
    /// Gets the latest Snap Hutao client version and its download mirrors.
    /// </summary>
    /// <returns>
    /// The update information. The client compares its HutaoRuntime.Version with the returned version and only
    /// prompts for an update when the returned version is greater.
    /// </returns>
    [HttpGet("hutao")]
    public async Task<IActionResult> SnapHutaoAsync()
    {
        HutaoPackageInformation? info = await releaseService.GetCurrentPackageAsync().ConfigureAwait(false);
        if (info is null)
        {
            return Fail("Update information is not configured yet", ReturnCode.NotFound, ServerKeys.ServerPatchUpdateInfoNotConfigured);
        }

        return Success<HutaoPackageInformation>("Update information acquired successfully", info, ServerKeys.ServerPatchUpdateInfoSucceed);
    }
}
