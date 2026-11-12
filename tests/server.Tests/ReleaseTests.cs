using server.Models.Admin;
using server.Models.Patch;
using server.Tests.Infrastructure;
using System.Net;
using System.Security.Cryptography;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class ReleaseTests(HutaoServerFactory factory)
{
    private const string BucketHost = "patch.test.s3.local";

    private readonly HttpClient client = factory.CreateApiClient();
    private readonly TestUsers users = factory.Users;

    [Fact]
    public async Task ReleasePublish_ThenActivate_MakesTheVersionCurrent()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string version = $"1.99.{Random.Shared.Next(1000, 9999)}.0";

        TestResponse published = await client.SendEnvelopeAsync("POST", "/api/admin/releases", TestData.ReleasePublishJson(version), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, published.Retcode);

        HutaoReleaseItem? release = published.DeserializeData<HutaoReleaseItem>();
        Assert.NotNull(release);
        Assert.Equal(version, release!.Version);
        Assert.True(release.IsActive);

        // 新发布的版本应立即成为 /patch/hutao 返回的当前版本
        using HttpResponseMessage patch = await client.GetWithTokenAsync("/patch/hutao", token: null);
        TestResponse patchParsed = await patch.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, patchParsed.Retcode);
        Assert.Equal(version, patchParsed.Data.GetProperty("version").GetString());

        // 同一个版本号不能重复发布
        TestResponse duplicate = await client.SendEnvelopeAsync("POST", "/api/admin/releases", TestData.ReleasePublishJson(version), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.InvalidUploadData, duplicate.Retcode);

        // 回滚到历史版本
        TestResponse activated = await client.SendEnvelopeAsync("PUT", $"/api/admin/releases/{release.Id}/activation", JsonSerializer.Serialize(new HutaoReleaseActivateRequest { Id = release.Id }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, activated.Retcode);

        // 下架当前版本：/patch/hutao 不再返回它；再激活恢复现场
        TestResponse deactivated = await client.SendEnvelopeAsync("DELETE", $"/api/admin/releases/{release.Id}/activation", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, deactivated.Retcode);

        using HttpResponseMessage after = await client.GetWithTokenAsync("/patch/hutao", token: null);
        TestResponse afterParsed = await after.ReadTestResponseAsync();

        // 干净库里没有其它激活版本时，下架后 /patch/hutao 合法地返回 NotFound
        Assert.True(afterParsed.Retcode is ReturnCodes.Success or ReturnCodes.NotFound, $"/patch/hutao 返回了 {afterParsed.Retcode}");
        if (afterParsed.Retcode is ReturnCodes.Success)
        {
            Assert.True(
                afterParsed.Data.ValueKind is JsonValueKind.Null || !afterParsed.Data.TryGetProperty("version", out JsonElement afterVersion) || afterVersion.GetString() != version,
                $"下架后 /patch/hutao 仍返回 {version}");
        }

        TestResponse reactivated = await client.SendEnvelopeAsync("PUT", $"/api/admin/releases/{release.Id}/activation", JsonSerializer.Serialize(new HutaoReleaseActivateRequest { Id = release.Id }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, reactivated.Retcode);
    }

    /// <summary>
    /// 最低强制版本链路：发布时携带 minimumVersion → /patch/hutao 原样下发（客户端据此强制更新）；
    /// 不携带时字段省略（null 走 WhenWritingNull），大于 Version 的值被拒绝.
    /// </summary>
    [Fact]
    public async Task ReleasePublish_WithMinimumVersion_ServesItOnPatch()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string version = $"1.98.{Random.Shared.Next(1000, 9999)}.0";
        string minimum = $"1.90.{Random.Shared.Next(1000, 9999)}.0";

        TestResponse published = await client.SendEnvelopeAsync("POST", "/api/admin/releases", TestData.ReleasePublishJson(version, minimum), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, published.Retcode);
        Assert.Equal(minimum, published.DeserializeData<HutaoReleaseItem>()!.MinimumVersion);

        using HttpResponseMessage patch = await client.GetWithTokenAsync("/patch/hutao", token: null);
        TestResponse patchParsed = await patch.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, patchParsed.Retcode);
        Assert.Equal(minimum, patchParsed.Data.GetProperty("minimumVersion").GetString());

        // 最低版本不得超过发布版本本身
        string later = $"1.97.{Random.Shared.Next(1000, 9999)}.0";
        TestResponse rejected = await client.SendEnvelopeAsync("POST", "/api/admin/releases", TestData.ReleasePublishJson(later, "1.99.0.0"), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.InvalidUploadData, rejected.Retcode);

        // 不带 minimumVersion 的发布（换版本号避开唯一索引），下发里字段应缺席
        TestResponse plain = await client.SendEnvelopeAsync("POST", "/api/admin/releases", TestData.ReleasePublishJson(later), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, plain.Retcode);
        using HttpResponseMessage patchAgain = await client.GetWithTokenAsync("/patch/hutao", token: null);
        TestResponse parsed = await patchAgain.ReadTestResponseAsync();
        Assert.Equal(later, parsed.Data.GetProperty("version").GetString());
        Assert.False(parsed.Data.TryGetProperty("minimumVersion", out _), "未配置强制版本时字段应省略");
    }

    [Theory]
    [InlineData("1.17.2", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]  // 版本号不足四段
    [InlineData("1.17.2.0", "not-a-sha256")]                                                 // 校验和格式错误
    public async Task ReleasePublish_WithInvalidPayload_IsRejected(string version, string validation)
    {
        TestUser maintainer = await users.MaintainerAsync();

        string body = JsonSerializer.Serialize(new
        {
            Version = version,
            Validation = validation,
            Mirrors = new[] { new { Url = "https://example.com/hutao.zip", MirrorName = "test", MirrorType = "Direct" } },
        });

        TestResponse parsed = await client.SendEnvelopeAsync("POST", "/api/admin/releases", body, maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    [Fact]
    public async Task ReleasePublish_WithInsecureMirrorUrl_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();

        string body = JsonSerializer.Serialize(new
        {
            Version = $"1.98.{Random.Shared.Next(1000, 9999)}.0",
            Validation = new string('a', 64),
            Mirrors = new[] { new { Url = "http://example.com/hutao.zip", MirrorName = "test", MirrorType = "Direct" } },
        });

        TestResponse parsed = await client.SendEnvelopeAsync("POST", "/api/admin/releases", body, maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    private async Task SetBucketConfigAsync(TestUser maintainer, bool enabled)
    {
        Dictionary<string, string?> settings = new()
        {
            ["Patch.S3.Host"] = enabled ? BucketHost : string.Empty,
            ["Patch.S3.Region"] = enabled ? "cn-test-1" : string.Empty,
            ["Patch.S3.AccessKeyId"] = enabled ? "test-access-key" : string.Empty,
            ["Patch.S3.SecretAccessKey"] = enabled ? "test-secret-key" : string.Empty,
        };

        TestResponse updated = await client.SendEnvelopeAsync("PATCH", "/api/admin/settings", JsonSerializer.Serialize(settings), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, updated.Retcode);
    }

    /// <summary>
    /// 从存储桶发布的完整链路：列对象 → 服务端流式算 SHA-256 → 版本号取自对象键 → 组装 Direct 镜像 →
    /// /patch/hutao 原样下发校验值与更新说明.桶本身由 StubHttpMessageHandler 冒充，不依赖外网.
    /// </summary>
    [Fact]
    public async Task ReleasePublishFromBucket_HashesObjectAndServesItOnPatch()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string version = $"1.96.{Random.Shared.Next(1000, 9999)}.0";
        string key = $"patch/Snap.Hutao.{version}.msix";
        byte[] payload = Encoding.UTF8.GetBytes($"msix payload for {version}");
        string sha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

        factory.StubHttp.Map(
            request => request.RequestUri?.Host == BucketHost && request.RequestUri.Query.Contains("list-type=2", StringComparison.Ordinal),
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/">
                      <Name>patch</Name>
                      <Prefix>patch/</Prefix>
                      <IsTruncated>false</IsTruncated>
                      <Contents>
                        <Key>{key}</Key>
                        <LastModified>2026-10-28T00:00:00.000Z</LastModified>
                        <Size>{payload.Length}</Size>
                      </Contents>
                    </ListBucketResult>
                    """, Encoding.UTF8, "application/xml"),
            });
        factory.StubHttp.Map(
            request => request.RequestUri?.Host == BucketHost && request.RequestUri.AbsolutePath.EndsWith($"/{key}", StringComparison.Ordinal),
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            });

        await SetBucketConfigAsync(maintainer, enabled: true);
        try
        {
            TestResponse listed = await client.SendEnvelopeAsync("GET", "/api/admin/releases/bucket-objects?prefix=patch/", null, maintainer.AccessToken);
            Assert.Equal(ReturnCodes.Success, listed.Retcode);

            ReleaseBucketListResult? list = listed.DeserializeData<ReleaseBucketListResult>();
            Assert.NotNull(list);
            Assert.Equal(BucketHost, list!.Host);
            ReleaseBucketObjectItem item = Assert.Single(list.Objects);
            Assert.Equal(key, item.Key);
            Assert.Equal(payload.Length, item.Size);

            TestResponse published = await client.SendEnvelopeAsync(
                "POST",
                "/api/admin/releases/from-bucket",
                JsonSerializer.Serialize(new HutaoReleasePublishFromBucketRequest
                {
                    Key = key,
                    ReleaseNotes = "从存储桶发布",
                }),
                maintainer.AccessToken);
            Assert.Equal(ReturnCodes.Success, published.Retcode);

            HutaoReleaseItem? release = published.DeserializeData<HutaoReleaseItem>();
            Assert.NotNull(release);
            Assert.Equal(version, release!.Version);
            Assert.Equal(sha256, release.Validation);

            HutaoPackageMirror mirror = Assert.Single(release.Mirrors);
            Assert.Equal($"https://{BucketHost}/{key}", mirror.Url);
            Assert.Equal(BucketHost, mirror.MirrorName);
            Assert.Equal(HutaoPackageMirrorType.Direct, mirror.MirrorType);

            using HttpResponseMessage patch = await client.GetWithTokenAsync("/patch/hutao", token: null);
            TestResponse patchParsed = await patch.ReadTestResponseAsync();
            Assert.Equal(ReturnCodes.Success, patchParsed.Retcode);
            Assert.Equal(sha256, patchParsed.Data.GetProperty("validation").GetString());
            Assert.Equal("从存储桶发布", patchParsed.Data.GetProperty("releaseNotes").GetString());
        }
        finally
        {
            await SetBucketConfigAsync(maintainer, enabled: false);
        }
    }

    [Fact]
    public async Task ReleasePublishFromBucket_WithMissingObject_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string key = $"patch/Snap.Hutao.1.95.{Random.Shared.Next(1000, 9999)}.0.msix";

        factory.StubHttp.Map(
            request => request.RequestUri?.Host == BucketHost && request.RequestUri.AbsolutePath.EndsWith($"/{key}", StringComparison.Ordinal),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await SetBucketConfigAsync(maintainer, enabled: true);
        try
        {
            TestResponse published = await client.SendEnvelopeAsync(
                "POST",
                "/api/admin/releases/from-bucket",
                JsonSerializer.Serialize(new HutaoReleasePublishFromBucketRequest { Key = key }),
                maintainer.AccessToken);

            Assert.Equal(ReturnCodes.InvalidUploadData, published.Retcode);
            Assert.Equal(ServerKeys.ServerAdminReleaseBucketObjectNotFound, published.L10nKey);
        }
        finally
        {
            await SetBucketConfigAsync(maintainer, enabled: false);
        }
    }

    [Fact]
    public async Task ReleasePublishFromBucket_WithoutConfiguration_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();
        await SetBucketConfigAsync(maintainer, enabled: false);

        TestResponse published = await client.SendEnvelopeAsync(
            "POST",
            "/api/admin/releases/from-bucket",
            JsonSerializer.Serialize(new HutaoReleasePublishFromBucketRequest { Key = "patch/Snap.Hutao.1.94.0.0.msix" }),
            maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, published.Retcode);
        Assert.Equal(ServerKeys.ServerAdminReleaseBucketNotConfigured, published.L10nKey);
    }
}
