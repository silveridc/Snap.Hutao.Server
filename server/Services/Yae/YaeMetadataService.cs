using server.Models.Metadata;

namespace server.Services.Yae;

public sealed class YaeMetadataService(
    AppDbContext appDbContext,
    IHttpClientFactory httpClientFactory,
    AppOptions appOptions,
    IDistributedCache cache,
    ILogger<YaeMetadataService> logger,
    TimeProvider timeProvider)
{
    // 注入配置是整包下发（可能几十 KB），每次启动都会拉；内容只在同步新版本时变
    private const string _payloadCacheKeyPrefix = "Yae:Payload:";
    private static readonly TimeSpan PayloadCacheDuration = TimeSpan.FromDays(1);

    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>
    /// 拉取一次 Yae bucket：解码出成就字段映射与版本号，按版本号新增或覆盖落库.
    /// </summary>
    /// <param name="chineseHash">本次同步强制指定的国服指纹，优先级高于配置；null 表示用配置里的集合.</param>
    /// <param name="token">取消令牌.</param>
    public async Task<YaeBucketDecoder.Result> SyncAsync(uint? chineseHash, CancellationToken token = default)
    {
        (byte[]? data, string? fetchError) = await FetchBucketAsync(token).ConfigureAwait(false);
        if (data is null)
        {
            return new YaeBucketDecoder.Result(null, null, 0U, 0U, 0U, 0U, fetchError);
        }

        IReadOnlyCollection<uint> known = chineseHash is { } forced ? [forced] : appOptions.Yae.ChineseGameHashes;
        YaeBucketDecoder.Result result = YaeBucketDecoder.Decode(data, known);
        if (!result.Succeeded || result.Payload is null)
        {
            return result;
        }

        string json = JsonSerializer.Serialize(result.Payload, PayloadOptions);
        YaeAchievementMetadata? existing = await appDbContext.YaeAchievementMetadata.FindAsync([result.Version], token).ConfigureAwait(false);
        YaeAchievementMetadata entity = existing ?? new YaeAchievementMetadata { Version = result.Version! };
        entity.ChineseHash = result.ChineseHash;
        entity.OverseaHash = result.OverseaHash;
        entity.StoreCmdId = result.Payload.NativeConfig.StoreCmdId;
        entity.AchievementCmdId = result.Payload.NativeConfig.AchievementCmdId;
        entity.ItemCount = result.ItemCount;
        entity.GroupCount = result.GroupCount;
        entity.Payload = json;
        entity.SyncedAt = timeProvider.GetUtcNow().UtcDateTime;

        if (existing is null)
        {
            appDbContext.YaeAchievementMetadata.Add(entity);
        }

        await appDbContext.SaveChangesAsync(token).ConfigureAwait(false);
        cache.Remove(_payloadCacheKeyPrefix + result.Version);
        logger.LogInformation("Yae metadata synced for game version {Version}, chinese hash {Chinese:X8}, oversea hash {Oversea:X8}", result.Version, result.ChineseHash, result.OverseaHash);
        return result;
    }

    /// <summary>读取已同步的版本数据，返回原样吐给客户端的响应体；未同步过返回 null（调用方给 404）.</summary>
    public async Task<string?> GetPayloadAsync(string version, CancellationToken token = default)
    {
        string cacheKey = _payloadCacheKeyPrefix + version;
        if (cache.TryGetValue<string>(cacheKey, out string? cached) && !string.IsNullOrEmpty(cached))
        {
            return cached;
        }

        YaeAchievementMetadata? entity = await appDbContext.YaeAchievementMetadata.AsNoTracking().FirstOrDefaultAsync(item => item.Version == version, token).ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        cache.Set(cacheKey, entity.Payload, PayloadCacheDuration);
        return entity.Payload;
    }

    public async Task<List<Summary>> ListAsync(CancellationToken token = default)
    {
        return
        [
            .. await appDbContext.YaeAchievementMetadata
                .AsNoTracking()
                .OrderByDescending(item => item.SyncedAt)
                .Select(item => new Summary(item.Version, item.ChineseHash, item.OverseaHash, item.StoreCmdId, item.AchievementCmdId, item.ItemCount, item.GroupCount, item.SyncedAt))
                .ToListAsync(token)
                .ConfigureAwait(false),
        ];
    }

    private async Task<(byte[]? Data, string? Error)> FetchBucketAsync(CancellationToken token)
    {
        string[] urls = appOptions.Yae.BucketUrls;
        if (urls.Length is 0)
        {
            return (null, "App:Yae:BucketUrls is empty");
        }

        HttpClient httpClient = httpClientFactory.CreateClient();
        List<string> errors = [];
        foreach (string url in urls)
        {
            try
            {
                using HttpResponseMessage response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    errors.Add($"{url} -> {(int)response.StatusCode}");
                    continue;
                }

                byte[] data = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
                if (data.Length is 0)
                {
                    errors.Add($"{url} -> empty body");
                    continue;
                }

                return (data, null);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                errors.Add($"{url} -> {ex.GetType().Name}");
            }
        }

        return (null, "All bucket mirrors failed: " + string.Join("; ", errors));
    }

    public sealed record Summary(string Version, uint ChineseHash, uint OverseaHash, uint StoreCmdId, uint AchievementCmdId, uint ItemCount, uint GroupCount, DateTime SyncedAt);
}
