using System.Net;
using System.Net.Http.Headers;

namespace server.Tests.Infrastructure;

public static class TestHttpClientExtension
{
    public const string DeviceIdHeader = "x-hutao-device-id";
    public const string DeviceNameHeader = "x-hutao-device-name";
    public const string DeviceOsHeader = "x-hutao-device-os";

    /// <summary>与 appsettings.json 的 <c>App:RealIp:Header</c> 一致，被 ForwardedHeaders 还原成 RemoteIpAddress.</summary>
    public const string RealIpHeader = "X-Real-IP";

    /// <summary>附加 Bearer 令牌.</summary>
    public static HttpRequestMessage WithToken(this HttpRequestMessage request, string? accessToken)
    {
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return request;
    }

    /// <summary>附加胡桃客户端设备头.</summary>
    public static HttpRequestMessage WithDevice(this HttpRequestMessage request, string deviceId = "test-device", string deviceName = "Test Device", string deviceOs = "Windows")
    {
        request.Headers.TryAddWithoutValidation(DeviceIdHeader, deviceId);
        request.Headers.TryAddWithoutValidation(DeviceNameHeader, deviceName);
        request.Headers.TryAddWithoutValidation(DeviceOsHeader, deviceOs);
        return request;
    }

    /// <summary>附加胡桃客户端版本 UserAgent.</summary>
    public static HttpRequestMessage WithClientVersion(this HttpRequestMessage request, string version = "1.17.1.0")
    {
        request.Headers.TryAddWithoutValidation("User-Agent", $"Snap Hutao/{version}");
        return request;
    }

    /// <summary>伪装成从公网来的请求，让服务端按真实客户端 IP 记审计、解析属地.</summary>
    public static HttpRequestMessage WithRealIp(this HttpRequestMessage request, string ip)
    {
        request.Headers.TryAddWithoutValidation(RealIpHeader, ip);
        return request;
    }

    public static Task<HttpResponseMessage> DeleteWithTokenAsync(this HttpClient client, string url, string? token, bool device = true)
    {
        HttpRequestMessage request = new(HttpMethod.Delete, url);
        request.WithToken(token);
        if (device)
        {
            request.WithDevice();
        }

        return client.SendAsync(request);
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

    public static Task<HttpResponseMessage> SendJsonWithTokenAsync<T>(this HttpClient client, HttpMethod method, string url, T? body, string? token, bool device = true)
    {
        HttpRequestMessage request = new(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

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

    /// <summary>发送请求并解析统一外壳；500 直接判失败并附上响应体，方便定位.</summary>
    public static async Task<TestResponse> SendEnvelopeAsync(this HttpClient client, string method, string url, string? json, string? token, string? realIp = null)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), url);
        request.WithToken(token);
        request.WithDevice();
        request.WithClientVersion();
        if (realIp is not null)
        {
            request.WithRealIp(realIp);
        }

        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode is not HttpStatusCode.InternalServerError, $"{method} {url} returned 500: {body}");

        return TestResponse.Parse(body);
    }
}