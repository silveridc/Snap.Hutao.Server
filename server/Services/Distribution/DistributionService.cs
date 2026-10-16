namespace server.Services.Distribution
{
    // Scoped
    public sealed class DistributionService(IServiceProvider serviceProvider)
    {
        private readonly ILogger<DistributionService> logger = serviceProvider.GetRequiredService<ILogger<DistributionService>>();
        private readonly HttpClient httpClient = serviceProvider.GetRequiredService<HttpClient>();
        private readonly string cdnEndpoint = serviceProvider.GetRequiredService<AppOptions>().CdnEndpoint;
        private readonly string cdnToken = serviceProvider.GetRequiredService<AppOptions>().CdnToken;

        public async ValueTask<string?> GetDownloadTokenAsync()
        {
            using HttpRequestMessage req = new(HttpMethod.Get, $"{cdnEndpoint}/v2/getDownloadToken");
            req.Headers.UserAgent.ParseAdd("Snap Hutao Server/1.0");
            req.Headers.Authorization = new("Bearer", cdnToken);

            using HttpResponseMessage resp = await httpClient.SendAsync(req);
            Response<string>? tokenResp = await resp.Content.ReadFromJsonAsync<Response<string>>();
            ArgumentNullException.ThrowIfNull(tokenResp);
            if (tokenResp is not { Code: ReturnCode.Success })
            {
                logger.LogWarning("Failed to get download token, raw message: {Message}", tokenResp.Message);
                return default;
            }

            return tokenResp.Data;
        }

        public async ValueTask<HutaoPackageMirror?> GetAcceleratedMirrorAsync(string filename)
        {
            using HttpRequestMessage req = new(HttpMethod.Get, $"{cdnEndpoint}/get?filename={filename}");
            req.Headers.UserAgent.ParseAdd("Snap Hutao Server/1.0");
            req.Headers.Authorization = new("Bearer", cdnToken);

            using HttpResponseMessage resp = await httpClient.SendAsync(req);
            Response<HutaoPackageMirror>? mirrorResp = await resp.Content.ReadFromJsonAsync<Response<HutaoPackageMirror>>();
            ArgumentNullException.ThrowIfNull(mirrorResp);
            if (mirrorResp is not { Code: ReturnCode.Success })
            {
                logger.LogWarning("Failed to get accelerated mirror for {Filename}, raw message: {Message}", filename, mirrorResp.Message);
                return default;
            }

            return mirrorResp.Data;
        }
    }
}