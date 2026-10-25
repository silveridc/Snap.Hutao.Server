using server.Services.Admin;
using server.Tests.Infrastructure;
using System.Net.Http;

namespace server.Tests;

/// <summary>
/// S3 预签名：结构正确（SigV4 查询参数齐全、签名 64 位十六进制）、确定性（同输入同签名、
/// 区域变化签名变化）、以及 wire 级行为——配置齐全且主机名匹配时 /patch/hutao 下发的镜像
/// 被现场签名，未配置或主机不匹配时 URL 原样返回。
/// </summary>
[Collection(HutaoServerCollection.Name)]
public sealed class S3PresignTests
{
    private readonly HutaoServerFactory factory;
    private readonly HttpClient client;

    public S3PresignTests(HutaoServerFactory factory)
    {
        this.factory = factory;
        client = factory.CreateApiClient();
    }

    [Fact]
    public void PresignGet_ProducesCanonicalSigV4Query()
    {
        string url = S3Presigner.PresignGet(
            "https://my-bucket.s3.us-east-1.amazonaws.com/hutao/Snap.Hutao_1.17.2.0_x64.msix",
            "us-east-1",
            "AKIAIOSFODNN7EXAMPLE",
            "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
            86400,
            new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero));

        // 不经过 System.Uri 解析（它会把 query 中的 %2F 二次转义），直接对原始串断言
        Assert.StartsWith("https://my-bucket.s3.us-east-1.amazonaws.com/hutao/Snap.Hutao_1.17.2.0_x64.msix?", url, StringComparison.Ordinal);

        Dictionary<string, string> query = url.ParseRawQuery();
        Assert.Equal("AWS4-HMAC-SHA256", query["X-Amz-Algorithm"]);
        Assert.Equal("host", query["X-Amz-SignedHeaders"]);
        Assert.Equal("20261025T000000Z", query["X-Amz-Date"]);
        Assert.Equal("86400", query["X-Amz-Expires"]);
        // SigV4 要求查询参数值按 RFC3986 编码：Credential 中的 '/' 必须是 %2F
        Assert.Equal("AKIAIOSFODNN7EXAMPLE/20261025/us-east-1/s3/aws4_request", query["X-Amz-Credential"]);
        Assert.Matches("^[0-9a-f]{64}$", query["X-Amz-Signature"]);

        // 原始串中的编码形态（不经 Unescape）
        Assert.Contains("X-Amz-Credential=AKIAIOSFODNN7EXAMPLE%2F20261025%2Fus-east-1%2Fs3%2Faws4_request", url, StringComparison.Ordinal);
    }

    [Fact]
    public void PresignGet_IsDeterministic_AndVariesByRegion()
    {
        string url = "https://b.example.com/pkg.zip";
        DateTimeOffset now = new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);

        string first = S3Presigner.PresignGet(url, "us-east-1", "AK", "SK", 3600, now);
        string second = S3Presigner.PresignGet(url, "us-east-1", "AK", "SK", 3600, now);
        string otherRegion = S3Presigner.PresignGet(url, "eu-west-1", "AK", "SK", 3600, now);

        Assert.Equal(first, second);
        Assert.NotEqual(first, otherRegion);
    }

    [Fact]
    public async Task PatchMirrors_ArePresignedOnlyWhenConfiguredAndHostMatches()
    {
        SystemConfigService config = factory.GetService<SystemConfigService>();
        TestUser maintainer = await factory.Users.MaintainerAsync();
        string version = $"1.96.{Random.Shared.Next(1000, 9999)}.0";

        // 发布两个镜像：一个主机名匹配 S3 配置，一个不匹配
        using HttpRequestMessage publish = new(HttpMethod.Post, "/api/Service/Release/Publish");
        publish.WithToken(maintainer.AccessToken);
        publish.Content = new StringContent(
            $$"""
            {"Version":"{{version}}","Validation":"{{new string('a', 64)}}","Mirrors":[
              {"url":"https://my-bucket.s3.us-east-1.amazonaws.com/hutao/pkg.msix","mirror_name":"s3","mirror_type":"Direct"},
              {"url":"https://example.com/pkg.msix","mirror_name":"plain","mirror_type":"Direct"}]}
            """, System.Text.Encoding.UTF8, "application/json");
        using HttpResponseMessage published = await client.SendAsync(publish);
        Assert.True(published.IsSuccessStatusCode);

        try
        {
            await config.UpdateAsync(new Dictionary<string, string?>
            {
                [SystemConfigService.Keys.PatchS3Host] = "my-bucket.s3.us-east-1.amazonaws.com",
                [SystemConfigService.Keys.PatchS3Region] = "us-east-1",
                [SystemConfigService.Keys.PatchS3AccessKeyId] = "AKIAIOSFODNN7EXAMPLE",
                [SystemConfigService.Keys.PatchS3SecretAccessKey] = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                [SystemConfigService.Keys.PatchS3ExpiresHours] = "24",
            }, 0).ConfigureAwait(false);

            using HttpResponseMessage signed = await client.GetAsync("/patch/hutao");
            string body = await signed.Content.ReadAsStringAsync();
            Assert.Contains("X-Amz-Signature=", body, StringComparison.Ordinal);
            Assert.Contains("https://my-bucket.s3.us-east-1.amazonaws.com/hutao/pkg.msix?X-Amz-Algorithm=AWS4-HMAC-SHA256", body, StringComparison.Ordinal);
            // 不匹配的镜像保持原样
            Assert.Contains("\"url\":\"https://example.com/pkg.msix\"", body, StringComparison.Ordinal);
        }
        finally
        {
            foreach (string key in new[]
            {
                SystemConfigService.Keys.PatchS3Host,
                SystemConfigService.Keys.PatchS3Region,
                SystemConfigService.Keys.PatchS3AccessKeyId,
                SystemConfigService.Keys.PatchS3SecretAccessKey,
                SystemConfigService.Keys.PatchS3ExpiresHours,
            })
            {
                await config.DeleteAsync(key).ConfigureAwait(false);
            }
        }

        // 撤销配置后：同一下发不再带签名（下一版本发布前该 release 仍是 active，用 Release/Activate 复位不需要——
        // 直接验证未配置时 SignS3Mirrors 短路即可：发布一个同主机的新版本，URL 原样）
        using HttpRequestMessage republish = new(HttpMethod.Post, "/api/Service/Release/Publish");
        string plainVersion = $"1.95.{Random.Shared.Next(1000, 9999)}.0";
        republish.WithToken(maintainer.AccessToken);
        republish.Content = new StringContent(
            $$"""
            {"Version":"{{plainVersion}}","Validation":"{{new string('a', 64)}}","Mirrors":[
              {"url":"https://my-bucket.s3.us-east-1.amazonaws.com/hutao/pkg2.msix","mirror_name":"s3","mirror_type":"Direct"}]}
            """, System.Text.Encoding.UTF8, "application/json");
        using HttpResponseMessage republished = await client.SendAsync(republish);
        Assert.True(republished.IsSuccessStatusCode);

        using HttpResponseMessage plain = await client.GetAsync("/patch/hutao");
        string plainBody = await plain.Content.ReadAsStringAsync();
        Assert.Contains("\"url\":\"https://my-bucket.s3.us-east-1.amazonaws.com/hutao/pkg2.msix\"", plainBody, StringComparison.Ordinal);
        Assert.DoesNotContain("X-Amz-Signature", plainBody, StringComparison.Ordinal);
        Assert.False(config.IsPatchS3SigningEnabled);
    }
}

file static class UriExtension
{
    /// <summary>不经 System.Uri（避免其对 %2F 的二次转义），直接切原始 URL 的查询串。</summary>
    public static Dictionary<string, string> ParseRawQuery(this string url)
    {
        Dictionary<string, string> result = [];
        string query = url.Split('?', 2)[1];
        foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] kv = pair.Split('=', 2);
            result[Uri.UnescapeDataString(kv[0])] = kv.Length is 2 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
        }

        return result;
    }
}
