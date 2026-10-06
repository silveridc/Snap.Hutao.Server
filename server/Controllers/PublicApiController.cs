using server.Models.PublicAPI;
using server.Services.PublicAPI;
namespace server.Controllers;

[ApiController]
[Route("/api/v1/")]
public class PublicApiController(IpRegionService ipRegionService) : ControllerBase
{
    /// <summary>
    /// Return ip and region data
    /// </summary>
    /// <returns>
    /// <list type="bullet">
    /// <item>
    /// <description><c></c></description>
    /// </item>
    /// </list>
    /// </returns>
    [HttpGet("ip")]
    [ProducesResponseType(typeof(IpResponse), 200)]
    public IActionResult GetIp()
    {
        IPAddress clientIp = HttpContext.Connection.RemoteIpAddress!;
        string?[] region = ipRegionService.GetRegion(clientIp);
        IpResponse? data = new(clientIp.ToString(), region[0]!, region[1]!, region[2]!, region[3]!);
        return Response<IpResponse>.Success("Success", data);
    }
}