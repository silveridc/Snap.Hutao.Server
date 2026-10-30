using server.Models.PublicAPI;
using server.Services.PublicAPI;

namespace server.Controllers;

[ApiController]
[Route("api/public")]
[ApiExplorerSettings(GroupName = "Public")]
public class PublicApiController(IpRegionService ipRegionService) : ControllerBase
{
    /// <summary>
    /// Return ip and region data
    /// </summary>
    /// <returns>IP and region data.</returns>
    [HttpGet("ip")]
    [ProducesResponseType(typeof(IpResponse), 200)]
    public async Task<IActionResult> GetIpAsync()
    {
        string? clientIp = HttpContext.GetClientIp();
        string?[]? region = await ipRegionService.GetRegionAsync(clientIp).ConfigureAwait(false);
        string?[] segments = region ?? new string?[4];
        IpResponse data = new(clientIp ?? "unknown", segments[0]!, segments[1]!, segments[2]!, segments[3]!);
        return Success<IpResponse>("Success", data);
    }
}
