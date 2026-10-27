using server.Models.PublicAPI;
using server.Services.Admin;

namespace server.Services.WallPaper;

// Scoped
public sealed class CuratedWallpaper(SystemConfigService systemConfig, ILogger<CuratedWallpaper> logger)
{
    /// <summary>
    /// 后台人工上架的今日壁纸；未配置或地址不是 HTTPS 绝对地址时返回 null，由调用方回退到 Bing。
    /// </summary>
    public WallPaperResponse? Get()
    {
        string url = systemConfig.GetOrDefault(SystemConfigService.Keys.WallpaperTodayUrl, string.Empty).Trim();
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            logger.LogWarning("Wallpaper.Today.Url is not an absolute HTTPS URL, falling back to Bing wallpaper");
            return null;
        }

        return new(
            url,
            systemConfig.GetOrDefault(SystemConfigService.Keys.WallpaperTodaySourceUrl, url).Trim(),
            systemConfig.GetOrDefault(SystemConfigService.Keys.WallpaperTodayAuthor, "Snap Hutao").Trim(),
            systemConfig.GetOrDefault(SystemConfigService.Keys.WallpaperTodayUploader, "Snap Hutao").Trim());
    }
}
