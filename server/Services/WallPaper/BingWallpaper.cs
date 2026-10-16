using server.Extension;
using server.Models.PublicAPI;

namespace server.Services.WallPaper;
public class BingWallpaper(IHttpClientFactory httpClientFactory, IDistributedCache cache, ILogger<BingWallpaper> logger)
{
    const string wallpaperType = "bing";
    public async Task<WallPaperResponse> GetTodayWallpaperAsync()
    {
        string todayKey = $"{wallpaperType}Wallpaper_{DateTime.UtcNow:yyyyMMdd}";
        string apiPath = "https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=zh-CN";
        if (cache.TryGetValue(todayKey, out WallPaperResponse? cresult))
        {
            return cresult!;
        }

        try
        {
            HttpClient client = httpClientFactory.CreateClient("BingClient");
            BingApiResponse? response = await client.GetFromJsonAsync<BingApiResponse>(apiPath);
            BingImageItem? imgItem = response?.Images?.FirstOrDefault();

            if (imgItem is null)
            {
                return null!;
            }
            string resUrl = $"https://www.bing.com{imgItem.UrlBase}_UHD.jpg";
            WallPaperResponse result = new (resUrl,imgItem.CopyrightLink, "Bing", imgItem.Copyright);
            cache.Set(todayKey, result, new DateTimeOffset(DateTime.Today.AddDays(1)));

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "拉取 Bing 壁纸时发生错误");
            return null!;
        }
    }
}

file class BingApiResponse
{
    [JsonPropertyName("images")]
    public List<BingImageItem>? Images { get; set; }
}

file class BingImageItem
{
    [JsonPropertyName("enddate")]
    public string EndDate { get; set; } = string.Empty;

    [JsonPropertyName("urlbase")]
    public string UrlBase { get; set; } = string.Empty;

    [JsonPropertyName("copyrightlink")]
    public string CopyrightLink { get; set; } = string.Empty;
    [JsonPropertyName("copyright")]
    public string Copyright { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}