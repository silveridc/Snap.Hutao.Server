using server.Models.PublicAPI;
using server.Services.PublicAPI;

namespace server.Controllers;

[ApiController]
[Route("api/[controller]")]
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

        // 查不到时仍是四段 null，接口输出形状与以前一致
        string?[] segments = region ?? new string?[4];
        IpResponse data = new(clientIp ?? "unknown", segments[0]!, segments[1]!, segments[2]!, segments[3]!);
        return Success<IpResponse>("Success", data);
    }
}
