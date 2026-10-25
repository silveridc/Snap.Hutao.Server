using server.Models.PublicAPI;
using server.Services.WallPaper;

namespace server.Controllers;

[Route("api/[controller]")]
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]

public class WallpaperController(BingWallpaper bing, HoyoPlayWallpaper hyp) : Controller
{
    /// <summary>
    /// 获取当日必应壁纸。
    /// </summary>
    /// <returns>壁纸地址与版权信息。</returns>
    [HttpGet("bing")]
    [HttpGet("today")]
    public async Task<IActionResult> GetBingWallpaper()
    {
        WallPaperResponse data = await bing.GetTodayWallpaperAsync();
        return Success<WallPaperResponse>("Success", data);
    }

    /// <summary>
    /// 获取当日米哈游启动器壁纸。
    /// </summary>
    /// <returns>壁纸地址与版权信息。</returns>
    [HttpGet("hoyoplay")]
    public async Task<IActionResult> GetHypWallpaper()
    {
        WallPaperResponse data = await hyp.GetTodayWallpaperAsync();
        return Success<WallPaperResponse>("Success", data);
    }
}
