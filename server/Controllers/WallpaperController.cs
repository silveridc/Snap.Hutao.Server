using server.Models.PublicAPI;
using server.Services.WallPaper;

namespace server.Controllers;

[Route("api/wallpapers")]
[ApiController]
[ApiExplorerSettings(GroupName = "Public")]

public class WallpaperController(BingWallpaper bing, HoyoPlayWallpaper hyp, CuratedWallpaper curated) : Controller
{
    /// <summary>
    /// Gets the Bing wallpaper of the day.
    /// </summary>
    /// <returns>The wallpaper address and copyright information.</returns>
    [HttpGet("bing")]
    public async Task<IActionResult> GetBingWallpaper()
    {
        WallPaperResponse data = await bing.GetTodayWallpaperAsync();
        return Success<WallPaperResponse>("Success", data);
    }

    /// <summary>
    /// Gets today's wallpaper: one is picked per day at random from the admin-maintained pool, falling back to the
    /// Bing daily wallpaper when the pool is empty.
    /// </summary>
    /// <returns>The wallpaper address and copyright information.</returns>
    [HttpGet("today")]
    public async Task<IActionResult> GetTodayWallpaper()
    {
        WallPaperResponse data = curated.Get() ?? await bing.GetTodayWallpaperAsync();
        return Success<WallPaperResponse>("Success", data);
    }

    /// <summary>
    /// Gets the miHoYo launcher (HoYoPlay) wallpaper of the day.
    /// </summary>
    /// <returns>The wallpaper address and copyright information.</returns>
    [HttpGet("hoyoplay")]
    public async Task<IActionResult> GetHypWallpaper()
    {
        WallPaperResponse data = await hyp.GetTodayWallpaperAsync();
        return Success<WallPaperResponse>("Success", data);
    }
}
