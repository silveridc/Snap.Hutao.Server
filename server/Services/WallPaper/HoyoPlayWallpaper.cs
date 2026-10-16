using server.Extension;
using server.Models.PublicAPI;

namespace server.Services.WallPaper;

public class HoyoPlayWallpaper(IHttpClientFactory httpClientFactory, IDistributedCache cache, ILogger<HoyoPlayWallpaper> logger)
{
    private const string WallpaperType = "hyp";

    public async Task<WallPaperResponse> GetTodayWallpaperAsync()
    {
        string todayKey = $"{WallpaperType}Wallpaper_{DateTime.UtcNow:yyyyMMdd}";
        const string apiPath = "https://hyp-api.mihoyo.com/hyp/hyp-connect/api/getAllGameBasicInfo?launcher_id=jGHBHlcOq1&language=zh-cn";

        if (cache.TryGetValue(todayKey, out WallPaperResponse? cresult))
        {
            return cresult!;
        }

        try
        {
            HttpClient client = httpClientFactory.CreateClient();
            HypApiResponse? response = await client.GetFromJsonAsync<HypApiResponse>(apiPath);
            HypGameItem? targetGame = response?.Data?.GameInfoList?.FirstOrDefault(g => string.Equals(g.Game?.Biz?.Trim(), "hk4e_cn", StringComparison.OrdinalIgnoreCase));
            HypBackground? bgItem = targetGame?.Backgrounds?.FirstOrDefault();
            string? imgUrl = bgItem?.Background?.Url;

            if (string.IsNullOrEmpty(imgUrl))
            {
                logger.LogWarning("解析图片失败");
                return null!;
            }
            WallPaperResponse result = new(
                Url: imgUrl,
                SourceUrl: "https://ys.mihoyo.com",
                Author: "HoYoPlay",
                Uploader: "启动器官方壁纸"
            );

            cache.Set(todayKey, result, new DateTimeOffset(DateTime.Today.AddDays(1)));
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "拉取壁纸时发生错误");
            return null!;
        }
    }
}

file record HypApiResponse(
    [property: JsonPropertyName("data")] HypData? Data
);
file record HypData(
    [property: JsonPropertyName("game_info_list")] List<HypGameItem>? GameInfoList
);
file record HypGameItem(
    [property: JsonPropertyName("game")] HypGame? Game,
    [property: JsonPropertyName("backgrounds")] List<HypBackground>? Backgrounds
);
file record HypGame(
    [property: JsonPropertyName("biz")] string? Biz
);
file record HypBackground(
    [property: JsonPropertyName("background")] HypUrl? Background,
    [property: JsonPropertyName("video")] HypUrl? Video
);
file record HypUrl(
    [property: JsonPropertyName("url")] string? Url
);