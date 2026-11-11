using server.Models.Admin;
using server.Services.Admin;
using server.Tests.Infrastructure;
using System.Globalization;
using System.Net;
using System.Net.Http;

namespace server.Tests;

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
    public void Presign_ProducesCanonicalSigV4Query()
    {
        string url = S3Presigner.Presign(
            "https://my-bucket.s3.us-east-1.amazonaws.com/hutao/Snap.Hutao_1.17.2.0_x64.msix",
            HttpMethod.Get,
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
    public void Presign_IsDeterministic_AndVariesByRegionAndMethod()
    {
        string url = "https://b.example.com/pkg.zip";
        DateTimeOffset now = new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);

        string first = S3Presigner.Presign(url, HttpMethod.Get, "us-east-1", "AK", "SK", 3600, now);
        string second = S3Presigner.Presign(url, HttpMethod.Get, "us-east-1", "AK", "SK", 3600, now);
        string otherRegion = S3Presigner.Presign(url, HttpMethod.Get, "eu-west-1", "AK", "SK", 3600, now);
        string head = S3Presigner.Presign(url, HttpMethod.Head, "us-east-1", "AK", "SK", 3600, now);

        Assert.Equal(first, second);
        Assert.NotEqual(first, otherRegion);

        // SigV4 把 HTTP 方法写进规范请求：GET 的签名不能用于 HEAD，否则 403 SignatureDoesNotMatch
        Dictionary<string, string> headQuery = head.ParseRawQuery();
        Assert.NotEqual(first.ParseRawQuery()["X-Amz-Signature"], headQuery["X-Amz-Signature"]);

        // 除签名外两者一致（X-Amz-Date/Expires/Credential 与编码形态）
        foreach (string key in new[] { "X-Amz-Algorithm", "X-Amz-Credential", "X-Amz-Date", "X-Amz-Expires", "X-Amz-SignedHeaders" })
        {
            Assert.Equal(first.ParseRawQuery()[key], headQuery[key]);
        }
    }

    [Fact]
    public async Task PublishFromBucket_WithPrecomputedHash_SkipsDownloadingTheObject()
    {
        SystemConfigService config = factory.GetService<SystemConfigService>();
        TestUser maintainer = await factory.Users.MaintainerAsync();
        string key = $"patch/precomputed-{Guid.NewGuid():N}.msix";
        string version = $"1.94.{Random.Shared.Next(1000, 9999)}.0";
        string validation = new('b', 64);

        factory.StubHttp.Map(
            request => request.Method == HttpMethod.Head && request.RequestUri!.AbsoluteUri.Contains(key, StringComparison.Ordinal),
            _ =>
            {
                HttpResponseMessage response = new(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([]),
                };
                response.Content.Headers.ContentLength = 123456789;
                return response;
            });

        await config.UpdateAsync(new Dictionary<string, string?>
        {
            [Keys.PatchS3Host] = "bucket.test",
            [Keys.PatchS3Region] = "us-east-1",
            [Keys.PatchS3AccessKeyId] = "AKIAIOSFODNN7EXAMPLE",
            [Keys.PatchS3SecretAccessKey] = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
        }, 0);

        try
        {
            using HttpRequestMessage publish = new(HttpMethod.Post, "/api/admin/releases/from-bucket");
            publish.WithToken(maintainer.AccessToken);
            publish.Content = JsonContent.Create(new HutaoReleasePublishFromBucketRequest
            {
                Key = key,
                Version = version,
                Validation = validation,
            });

            using HttpResponseMessage response = await client.SendAsync(publish, TestContext.Current.CancellationToken);
            TestResponse parsed = await response.ReadTestResponseAsync();
            Assert.Equal(ReturnCodes.Success, parsed.Retcode);

            HutaoReleaseItem? release = parsed.DeserializeData<HutaoReleaseItem>();
            Assert.NotNull(release);
            Assert.Equal(validation, release!.Validation);

            // 指定哈希后只发 HEAD，不下载对象
            Assert.Contains(factory.StubHttp.Requests, r => r.Method == "HEAD" && r.Url.Contains(key, StringComparison.Ordinal));
            Assert.DoesNotContain(factory.StubHttp.Requests, r => r.Method == "GET" && r.Url.Contains(key, StringComparison.Ordinal));

            // 这条 HEAD 必须按 HEAD 签名：桩不校验签名，生产上 GET 签名发 HEAD 会 403 SignatureDoesNotMatch
            string headUrl = Assert.Single(factory.StubHttp.Requests, r => r.Method == "HEAD" && r.Url.Contains(key, StringComparison.Ordinal)).Url;
            Dictionary<string, string> headQuery = headUrl.ParseRawQuery();
            string expectedSignature = S3Presigner.Presign(
                $"https://bucket.test/{key}",
                HttpMethod.Head,
                "us-east-1",
                "AKIAIOSFODNN7EXAMPLE",
                "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                int.Parse(headQuery["X-Amz-Expires"], CultureInfo.InvariantCulture),
                DateTimeOffset.ParseExact(
                    headQuery["X-Amz-Date"],
                    "yyyyMMdd'T'HHmmss'Z'",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)).ParseRawQuery()["X-Amz-Signature"];
            Assert.Equal(expectedSignature, headQuery["X-Amz-Signature"]);

            using HttpRequestMessage deactivate = new(HttpMethod.Delete, $"/api/admin/releases/{release.Id}/activation");
            deactivate.WithToken(maintainer.AccessToken);
            using HttpResponseMessage deactivated = await client.SendAsync(deactivate, TestContext.Current.CancellationToken);
            Assert.True(deactivated.IsSuccessStatusCode);
        }
        finally
        {
            foreach (string configKey in new[]
            {
                Keys.PatchS3Host,
                Keys.PatchS3Region,
                Keys.PatchS3AccessKeyId,
                Keys.PatchS3SecretAccessKey,
            })
            {
                await config.DeleteAsync(configKey);
            }
        }
    }

    [Fact]
    public async Task PatchMirrors_ArePresignedOnlyWhenConfiguredAndHostMatches()
    {
        SystemConfigService config = factory.GetService<SystemConfigService>();
        TestUser maintainer = await factory.Users.MaintainerAsync();
        string version = $"1.96.{Random.Shared.Next(1000, 9999)}.0";

        // 发布两个镜像：一个主机名匹配 S3 配置，一个不匹配
        using HttpRequestMessage publish = new(HttpMethod.Post, "/api/admin/releases");
        publish.WithToken(maintainer.AccessToken);
        publish.Content = new StringContent(
            $$"""
            {"Version":"{{version}}","Validation":"{{new string('a', 64)}}","Mirrors":[
              {"url":"https://my-bucket.s3.us-east-1.amazonaws.com/hutao/pkg.msix","mirror_name":"s3","mirror_type":"Direct"},
              {"url":"https://example.com/pkg.msix","mirror_name":"plain","mirror_type":"Direct"}]}
            """, System.Text.Encoding.UTF8, "application/json");
        using HttpResponseMessage published = await client.SendAsync(publish, TestContext.Current.CancellationToken);
        Assert.True(published.IsSuccessStatusCode);

        try
        {
            await config.UpdateAsync(new Dictionary<string, string?>
            {
                [Keys.PatchS3Host] = "my-bucket.s3.us-east-1.amazonaws.com",
                [Keys.PatchS3Region] = "us-east-1",
                [Keys.PatchS3AccessKeyId] = "AKIAIOSFODNN7EXAMPLE",
                [Keys.PatchS3SecretAccessKey] = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
                [Keys.PatchS3ExpiresHours] = "24",
            }, 0);

            using HttpResponseMessage signed = await client.GetAsync("/patch/hutao", TestContext.Current.CancellationToken);
            string body = await signed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.Contains("X-Amz-Signature=", body, StringComparison.Ordinal);
            Assert.Contains("https://my-bucket.s3.us-east-1.amazonaws.com/hutao/pkg.msix?X-Amz-Algorithm=AWS4-HMAC-SHA256", body, StringComparison.Ordinal);
            // 不匹配的镜像保持原样
            Assert.Contains("\"url\":\"https://example.com/pkg.msix\"", body, StringComparison.Ordinal);
        }
        finally
        {
            foreach (string key in new[]
            {
                Keys.PatchS3Host,
                Keys.PatchS3Region,
                Keys.PatchS3AccessKeyId,
                Keys.PatchS3SecretAccessKey,
                Keys.PatchS3ExpiresHours,
            })
            {
                await config.DeleteAsync(key);
            }
        }

        // 撤销配置后：同一下发不再带签名（下一版本发布前该 release 仍是 active，用 Release/Activate 复位不需要——
        // 直接验证未配置时 SignS3Mirrors 短路即可：发布一个同主机的新版本，URL 原样）
        using HttpRequestMessage republish = new(HttpMethod.Post, "/api/admin/releases");
        string plainVersion = $"1.95.{Random.Shared.Next(1000, 9999)}.0";
        republish.WithToken(maintainer.AccessToken);
        republish.Content = new StringContent(
            $$"""
            {"Version":"{{plainVersion}}","Validation":"{{new string('a', 64)}}","Mirrors":[
              {"url":"https://my-bucket.s3.us-east-1.amazonaws.com/hutao/pkg2.msix","mirror_name":"s3","mirror_type":"Direct"}]}
            """, System.Text.Encoding.UTF8, "application/json");
        using HttpResponseMessage republished = await client.SendAsync(republish, TestContext.Current.CancellationToken);
        Assert.True(republished.IsSuccessStatusCode);

        using HttpResponseMessage plain = await client.GetAsync("/patch/hutao", TestContext.Current.CancellationToken);
        string plainBody = await plain.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("\"url\":\"https://my-bucket.s3.us-east-1.amazonaws.com/hutao/pkg2.msix\"", plainBody, StringComparison.Ordinal);
        Assert.DoesNotContain("X-Amz-Signature", plainBody, StringComparison.Ordinal);
        Assert.False(config.IsPatchS3SigningEnabled);
    }
}

file static class UriExtension
{
    /// <summary>不经 System.Uri（避免其对 %2F 的二次转义），直接切原始 URL 的查询串.</summary>
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