namespace server.Controllers;

[ApiController]
[Route("[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class EnkaController(IHttpClientFactory httpClientFactory, IDistributedCache cache) : ControllerBase
{
    private const string EnkaApiPrefix = "https://enka.network/api/uid/";
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60),
    };

    /// <summary>
    /// 中转获取 Enka 完整展柜数据（客户端优先直连 enka.network，直连失败时回退到此处）。
    /// </summary>
    /// <param name="uid">游戏 Uid。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>Enka 原始响应。</returns>
    [HttpGet("{uid}")]
    public Task<IActionResult> GetDataAsync([FromRoute] string uid, CancellationToken token)
    {
        return ForwardAsync($"{EnkaApiPrefix}{uid}", token);
    }

    /// <summary>
    /// 中转获取 Enka 玩家基础信息（?info）。
    /// </summary>
    /// <param name="uid">游戏 Uid。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>Enka 原始响应。</returns>
    [HttpGet("{uid}/info")]
    public Task<IActionResult> GetPlayerInfoAsync([FromRoute] string uid, CancellationToken token)
    {
        return ForwardAsync($"{EnkaApiPrefix}{uid}?info", token);
    }

    private async Task<IActionResult> ForwardAsync(string url, CancellationToken token)
    {
        string cacheKey = $"Enka:{url}";
        string? cachedBody = await cache.GetStringAsync(cacheKey, token).ConfigureAwait(false);
        if (cachedBody is not null)
        {
            return new ContentResult
            {
                StatusCode = (int)HttpStatusCode.OK,
                ContentType = "application/json",
                Content = cachedBody,
            };
        }

        HttpClient httpClient = httpClientFactory.CreateClient(nameof(EnkaController));
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", "SnapHutaoSelfHost/1.0");

        using HttpResponseMessage response = await httpClient.SendAsync(request, token).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

        // 仅缓存成功响应，避免把错误（限流、404 等）写入缓存
        if (response.IsSuccessStatusCode && !string.IsNullOrEmpty(body))
        {
            await cache.SetStringAsync(cacheKey, body, CacheOptions, token).ConfigureAwait(false);
        }

        // 透传 enka 的状态码与 JSON 内容，让客户端沿用其既有错误处理
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = "application/json",
            Content = body,
        };
    }
}
