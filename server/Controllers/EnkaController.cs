namespace server.Controllers;

[ApiController]
[Route("api/enka")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class EnkaController(IHttpClientFactory httpClientFactory, IDistributedCache cache) : ControllerBase
{
    private const string _enkaApiPrefix = "https://enka.network/api/uid/";
    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60),
    };

    /// <summary>
    /// Proxies the full Enka showcase data. The client prefers a direct connection to enka.network and falls back
    /// to this endpoint when the direct connection fails.
    /// </summary>
    /// <param name="uid">The game UID.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The raw Enka response.</returns>
    [HttpGet("{uid}")]
    public Task<IActionResult> GetDataAsync([FromRoute] string uid, CancellationToken token)
    {
        return ForwardAsync($"{_enkaApiPrefix}{uid}", token);
    }

    /// <summary>
    /// Proxies the basic Enka player information (<c>?info</c>).
    /// </summary>
    /// <param name="uid">The game UID.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The raw Enka response.</returns>
    [HttpGet("{uid}/info")]
    public Task<IActionResult> GetPlayerInfoAsync([FromRoute] string uid, CancellationToken token)
    {
        return ForwardAsync($"{_enkaApiPrefix}{uid}?info", token);
    }

    /// <summary>
    /// Forwards the request to Enka, caching successful responses for a short period and passing through the
    /// upstream status code and JSON body.
    /// </summary>
    /// <param name="url">The upstream URL.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The upstream response as a content result.</returns>
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
