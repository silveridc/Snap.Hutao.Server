using System.Net.Http.Headers;

namespace server.Tests.Infrastructure;

/// <summary>构造带鉴权与设备头的请求。</summary>
public static class TestHttpClientExtension
{
    public const string DeviceIdHeader = "x-hutao-device-id";
    public const string DeviceNameHeader = "x-hutao-device-name";
    public const string DeviceOsHeader = "x-hutao-device-os";

    /// <summary>附加 Bearer 令牌。</summary>
    public static HttpRequestMessage WithToken(this HttpRequestMessage request, string? accessToken)
    {
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return request;
    }

    /// <summary>附加胡桃客户端设备头。</summary>
    public static HttpRequestMessage WithDevice(this HttpRequestMessage request, string deviceId = "test-device", string deviceName = "Test Device", string deviceOs = "Windows")
    {
        request.Headers.TryAddWithoutValidation(DeviceIdHeader, deviceId);
        request.Headers.TryAddWithoutValidation(DeviceNameHeader, deviceName);
        request.Headers.TryAddWithoutValidation(DeviceOsHeader, deviceOs);
        return request;
    }

    /// <summary>附加胡桃客户端版本 UserAgent。</summary>
    public static HttpRequestMessage WithClientVersion(this HttpRequestMessage request, string version = "1.17.1.0")
    {
        request.Headers.TryAddWithoutValidation("User-Agent", $"Snap Hutao/{version}");
        return request;
    }

    public static Task<HttpResponseMessage> GetWithTokenAsync(this HttpClient client, string url, string? token, bool device = true)
    {
        HttpRequestMessage request = new(HttpMethod.Get, url);
        request.WithToken(token);
        if (device)
        {
            request.WithDevice();
        }

        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> PostJsonWithTokenAsync<T>(this HttpClient client, string url, T body, string? token, bool device = true)
    {
        HttpRequestMessage request = new(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.WithToken(token);
        if (device)
        {
            request.WithDevice();
        }

        return client.SendAsync(request);
    }
}
