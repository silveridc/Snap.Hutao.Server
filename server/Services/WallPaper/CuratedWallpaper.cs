using server.Models.PublicAPI;
using server.Services.Admin;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace server.Services.WallPaper;

// Scoped
public sealed class CuratedWallpaper(SystemConfigService systemConfig, TimeProvider timeProvider, ILogger<CuratedWallpaper> logger)
{
    private const string DefaultAuthor = "zhaoyj";
    private const int MaxPoolSize = 50;

    public WallPaperResponse? Get()
    {
        IReadOnlyList<WallpaperPoolItem> pool = GetPool();
        if (pool.Count is 0)
        {
            return null;
        }

        WallpaperPoolItem item = pool[GetDailyIndex(pool.Count)];
        return new(
            item.Url,
            string.IsNullOrWhiteSpace(item.SourceUrl) ? item.Url : item.SourceUrl,
            string.IsNullOrWhiteSpace(item.Author) ? DefaultAuthor : item.Author,
            string.IsNullOrWhiteSpace(item.Uploader) ? DefaultAuthor : item.Uploader);
    }

    public IReadOnlyList<WallpaperPoolItem> GetPool()
    {
        string? json = systemConfig.Get(Keys.WallpaperTodayPool);
        if (json is null)
        {
            return GetLegacyPool();
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            List<WallpaperPoolItem>? items = JsonSerializer.Deserialize<List<WallpaperPoolItem>>(json);
            return items is null ? [] : [.. items.Where(IsValid).Select(Normalize)];
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid {Key} value, falling back to the legacy wallpaper settings", Keys.WallpaperTodayPool);
            return GetLegacyPool();
        }
    }

    public async Task<(bool Success, string Message)> SavePoolAsync(IReadOnlyList<WallpaperPoolItem> items, int userId)
    {
        if (items.Count > MaxPoolSize)
        {
            return (false, $"The wallpaper pool cannot contain more than {MaxPoolSize} entries");
        }

        List<WallpaperPoolItem> normalized = [];
        foreach (WallpaperPoolItem item in items)
        {
            string url = item.Url?.Trim() ?? string.Empty;
            if (!IsHttpsUrl(url))
            {
                return (false, "Every wallpaper URL must be an absolute HTTPS URL");
            }

            string? sourceUrl = NormalizeOptional(item.SourceUrl);
            if (sourceUrl is not null && !IsHttpsUrl(sourceUrl))
            {
                return (false, "Every wallpaper source URL must be an absolute HTTPS URL");
            }

            normalized.Add(new WallpaperPoolItem
            {
                Url = url,
                SourceUrl = sourceUrl,
                Author = NormalizeOptional(item.Author),
                Uploader = NormalizeOptional(item.Uploader),
            });
        }

        string json = JsonSerializer.Serialize(normalized);
        return await systemConfig.UpdateAsync(
            new()
            {
                [Keys.WallpaperTodayPool] = json,
            },
            userId).ConfigureAwait(false);
    }

    private static bool IsHttpsUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps;
    }

    private static bool IsValid(WallpaperPoolItem item)
    {
        return IsHttpsUrl(item.Url?.Trim() ?? string.Empty);
    }

    private static WallpaperPoolItem Normalize(WallpaperPoolItem item)
    {
        return new()
        {
            Url = item.Url.Trim(),
            SourceUrl = NormalizeOptional(item.SourceUrl),
            Author = NormalizeOptional(item.Author),
            Uploader = NormalizeOptional(item.Uploader),
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private IReadOnlyList<WallpaperPoolItem> GetLegacyPool()
    {
        string url = systemConfig.GetOrDefault(Keys.WallpaperTodayUrl, string.Empty).Trim();
        if (!IsHttpsUrl(url))
        {
            if (url.Length is not 0)
            {
                logger.LogWarning("Wallpaper.Today.Url is not an absolute HTTPS URL, falling back to Bing wallpaper");
            }

            return [];
        }

        return
        [
            new WallpaperPoolItem
            {
                Url = url,
                SourceUrl = NormalizeOptional(systemConfig.Get(Keys.WallpaperTodaySourceUrl)),
                Author = NormalizeOptional(systemConfig.Get(Keys.WallpaperTodayAuthor)),
                Uploader = NormalizeOptional(systemConfig.Get(Keys.WallpaperTodayUploader)),
            },
        ];
    }

    private int GetDailyIndex(int count)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(today.DayNumber.ToString(CultureInfo.InvariantCulture)));
        return (int)(BinaryPrimitives.ReadUInt32LittleEndian(hash) % (uint)count);
    }
}
