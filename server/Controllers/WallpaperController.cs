using server.Models.PublicAPI;
using server.Services.WallPaper;
namespace server.Controllers;

[Route("[controller]")]
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]

public class WallpaperController(Bing bing,HoyoPlay hyp) : Controller
{
    [HttpGet("bing")]
    [HttpGet("today")]
    public async Task<IActionResult> GetBingWallpaper()
    {
        WallPaperResponse data = await bing.GetTodayWallpaperAsync();
        return Response<WallPaperResponse>.Success("Success",data);
    }
    [HttpGet("hoyoplay")]
    public async Task<IActionResult> GetHypWallpaper()
    {
        WallPaperResponse data = await hyp.GetTodayWallpaperAsync();
        return Response<WallPaperResponse>.Success("Success", data);
    }
}
