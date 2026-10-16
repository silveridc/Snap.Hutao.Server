using Microsoft.EntityFrameworkCore;
using server.Models.GachaLog;
using server.Models.Legacy;
using server.Models.Metadata;
using server.Models.Passport;
using server.Services.GachaLog;
using server.Tests.Infrastructure;
using System.Net;

namespace server.Tests;

/// <summary>
/// 客户端数据链路：深渊/剧诗上传、祈愿记录云同步、CDN 分发、崩溃日志与开源许可证申请。
/// 这些接口在 <see cref="ApiSmokeTests"/> 里只保证「不 500」，本类断言成功分支的真实
/// 返回码与副作用（数据能被读回来）。
/// </summary>
[Collection(HutaoServerCollection.Name)]
public sealed class ClientFlowTests
{
    private readonly HutaoServerFactory factory;
    private readonly HttpClient client;
    private readonly TestUsers users;

    public ClientFlowTests(HutaoServerFactory factory)
    {
        this.factory = factory;
        client = factory.CreateApiClient();
        users = factory.Users;
    }

    private static int uidCounter;

    /// <summary>
    /// 生成一个不与其它用例冲突的游戏 Uid。服务端要求纯数字、不少于 9 位且能放进 Int32
    /// （<c>/Record/Check</c> 会用 int.TryParse 校验），所以在 2 亿到 3 亿之间递增取值。
    /// </summary>
    private static string NewUid() => $"2{Interlocked.Increment(ref uidCounter):D8}";

    private async Task<TestResponse> SendAsync(string method, string url, object? body = null, string? token = null)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), url);
        request.WithToken(token);
        request.WithDevice();
        request.WithClientVersion();

        if (body is not null)
        {
            request.Content = body is string json
                ? new StringContent(json, Encoding.UTF8, "application/json")
                : JsonContent.Create(body);
        }

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode is not HttpStatusCode.InternalServerError, $"{method} {url} returned 500: {text}");

        return TestResponse.Parse(text);
    }

    private async Task<UserInfo> UserInfoAsync(TestUser user)
    {
        TestResponse parsed = await SendAsync("GET", "/Passport/v2/UserInfo", token: user.AccessToken);
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);
        return parsed.DeserializeData<UserInfo>()!;
    }

    [Fact]
    public async Task SpiralAbyssUpload_ThenCheckAndRank_ReadTheRecordBack()
    {
        string uid = NewUid();

        // 未绑定账号的首次上传：成功但不发放祈愿服务时长
        TestResponse uploaded = await SendAsync("POST", "/Record/Upload", TestData.SimpleRecord(uid));
        Assert.Equal(ReturnCodes.Success, uploaded.Retcode);
        Assert.Equal(ServerKeys.ServerRecordUploadSuccessButNoPassport, uploaded.L10nKey);

        TestResponse check = await SendAsync("GET", $"/Record/Check?Uid={uid}");
        Assert.Equal(ReturnCodes.Success, check.Retcode);
        Assert.True(check.Data.GetBoolean());

        // 排行表在上传事务里同步写入，伤害数据应能原样读回
        TestResponse rank = await SendAsync("GET", $"/Record/Rank?Uid={uid}");
        Assert.Equal(ReturnCodes.Success, rank.Retcode);
        Rank? rankData = rank.DeserializeData<Rank>();
        Assert.NotNull(rankData);
        Assert.Equal(10000005, rankData!.Damage!.Item);
        Assert.Equal(123456, rankData.Damage.Value);
        Assert.Equal(10000006, rankData.TakeDamage!.Item);
        Assert.Equal(65432, rankData.TakeDamage.Value);

        // 同一期重复上传仍然成功，但换成「本期非首次」的消息
        TestResponse again = await SendAsync("POST", "/Record/Upload", TestData.SimpleRecord(uid));
        Assert.Equal(ReturnCodes.Success, again.Retcode);
        Assert.Equal(ServerKeys.ServerRecordUploadSuccessButNotFirstTimeAtCurrentSchedule, again.L10nKey);
    }

    [Fact]
    public async Task SpiralAbyssUpload_WithBoundAccount_GrantsGachaLogServiceTime()
    {
        TestUser owner = await users.CreateUniqueAsync("abyss-owner");
        string uid = NewUid();

        TestResponse uploaded = await SendAsync("POST", "/Record/Upload", TestData.SimpleRecord(uid, reservedUserName: owner.UserName));
        Assert.Equal(ReturnCodes.Success, uploaded.Retcode);
        Assert.Equal(ServerKeys.ServerRecordUploadSuccessAndGachaLogServiceTimeExtended, uploaded.L10nKey);

        // RecordService.GachaLogExtendDays = 3，账号原本没有有效期，从当前时刻起算
        DateTimeOffset expireAt = (await UserInfoAsync(owner)).GachaLogExpireAt;
        Assert.True(
            (expireAt - DateTimeOffset.UtcNow.AddDays(3)).Duration() <= TimeSpan.FromMinutes(5),
            $"祈愿服务有效期未按 3 天发放：{expireAt:o}");

        // 上传时也能带排名数据，?returningRank=true 直接返回排行
        TestResponse withRank = await SendAsync("POST", "/Record/Upload?returningRank=true", TestData.SimpleRecord(NewUid(), reservedUserName: owner.UserName));
        Assert.Equal(ReturnCodes.Success, withRank.Retcode);
        Assert.NotNull(withRank.DeserializeData<Rank>());
    }

    [Fact]
    public async Task SpiralAbyssUpload_ForAnUnknownBoundAccount_StillSucceeds()
    {
        string uid = NewUid();

        TestResponse uploaded = await SendAsync("POST", "/Record/Upload", TestData.SimpleRecord(uid, reservedUserName: $"ghost-{Guid.NewGuid():N}@example.com"));

        // 账号不存在只影响时长发放，数据本身照常入库
        Assert.Equal(ReturnCodes.Success, uploaded.Retcode);
        Assert.Equal(ServerKeys.ServerRecordUploadSuccessButNoSuchUser, uploaded.L10nKey);

        Assert.True((await SendAsync("GET", $"/Record/Check?Uid={uid}")).Data.GetBoolean());
    }

    [Fact]
    public async Task SpiralAbyssUpload_ForBannedUid_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string uid = NewUid();

        Assert.Equal(ReturnCodes.Success, (await SendAsync("POST", $"/Service/Ban/Add?uid={uid}&reason=clientflow", token: maintainer.AccessToken)).Retcode);

        TestResponse uploaded = await SendAsync("POST", "/Record/Upload", TestData.SimpleRecord(uid));
        Assert.Equal(ReturnCodes.BannedUid, uploaded.Retcode);
        Assert.Equal(ServerKeys.ServerRecordBannedUid, uploaded.L10nKey);

        await SendAsync("POST", $"/Service/Ban/Remove?uid={uid}", token: maintainer.AccessToken);
    }

    [Fact]
    public async Task SpiralAbyssUpload_FromAnotherSchedule_IsRejected()
    {
        string uid = NewUid();

        TestResponse uploaded = await SendAsync("POST", "/Record/Upload", TestData.OutdatedSpiralAbyssRecordJson(uid));

        Assert.Equal(ReturnCodes.NotCurrentSchedule, uploaded.Retcode);
        Assert.False((await SendAsync("GET", $"/Record/Check?Uid={uid}")).Data.GetBoolean());
    }

    [Fact]
    public async Task SpiralAbyssUpload_WithEmptyBody_IsRejected()
    {
        TestResponse uploaded = await SendAsync("POST", "/Record/Upload", new { });

        Assert.Equal(ReturnCodes.InvalidUploadData, uploaded.Retcode);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("10000000a")]
    public async Task RecordCheck_WithMalformedUid_IsRejected(string uid)
    {
        TestResponse parsed = await SendAsync("GET", $"/Record/Check?Uid={uid}");

        Assert.Equal(ReturnCodes.InvalidQueryString, parsed.Retcode);
    }

    [Fact]
    public async Task RoleCombatUpload_SucceedsAndReplacesThePreviousRecord()
    {
        string uid = NewUid();

        Assert.Equal(ReturnCodes.Success, (await SendAsync("POST", "/RoleCombat/Upload", TestData.RoleCombatRecord(uid))).Retcode);

        // 每个 Uid 只保留最新一条，重复上传不会累积
        TestResponse again = await SendAsync("POST", "/RoleCombat/Upload", TestData.RoleCombatRecord(uid));
        Assert.Equal(ReturnCodes.Success, again.Retcode);

        int records = await factory.WithDbContextAsync(context =>
            context.RoleCombatRecords.AsNoTracking().CountAsync(r => r.Uid == uid, TestContext.Current.CancellationToken));
        Assert.Equal(1, records);
    }

    [Fact]
    public async Task RoleCombatUpload_FromAnotherSchedule_IsRejected()
    {
        server.Models.RoleCombat.SimpleRoleCombatRecord record = TestData.RoleCombatRecord(NewUid());
        record.ScheduleId -= 1;

        TestResponse uploaded = await SendAsync("POST", "/RoleCombat/Upload", record);

        Assert.Equal(ReturnCodes.NotCurrentSchedule, uploaded.Retcode);
    }

    [Fact]
    public async Task GachaLogUpload_ThenRetrieveAndDelete_RoundTrips()
    {
        TestUser owner = await users.GachaLogAsync();
        string uid = NewUid();
        UidAndItems items = TestData.GachaItems(uid, count: 4);

        TestResponse uploaded = await SendAsync("POST", "/GachaLog/Upload", items, owner.AccessToken);
        Assert.Equal(ReturnCodes.Success, uploaded.Retcode);
        Assert.Equal((int)GachaLogSaveResultKind.Ok, uploaded.Data.GetProperty("Kind").GetInt32());
        Assert.Equal(items.Items.Count, uploaded.Data.GetProperty("SaveCount").GetInt32());

        TestResponse entries = await SendAsync("GET", "/GachaLog/Entries", token: owner.AccessToken);
        Assert.Equal(ReturnCodes.Success, entries.Retcode);
        Assert.NotNull(entries.DeserializeData<List<GachaEntry>>()?.SingleOrDefault(entry => entry.Uid == uid && entry.ItemCount == items.Items.Count));

        long newestId = items.Items.Max(item => item.Id);
        TestResponse endIds = await SendAsync("GET", $"/GachaLog/EndIds?Uid={uid}", token: owner.AccessToken);
        Assert.Equal(ReturnCodes.Success, endIds.Retcode);
        Assert.Equal(newestId, endIds.DeserializeData<Dictionary<string, long>>()![$"{(int)GachaConfigType.ActivityAvatar:D}"]);

        // Retrieve 取严格早于 EndIds 的记录，用最新 Id 能取回全部四条
        TestResponse retrieved = await SendAsync("POST", "/GachaLog/Retrieve", new { Uid = uid, EndIds = new Dictionary<string, long> { [$"{(int)GachaConfigType.ActivityAvatar}"] = newestId + 1 } }, owner.AccessToken);
        Assert.Equal(ReturnCodes.Success, retrieved.Retcode);
        Assert.Equal(items.Items.Count, retrieved.DeserializeData<List<SimpleGachaItem>>()?.Count);

        TestResponse limited = await SendAsync("GET", $"/GachaLog/LimitedRetrieve?uid={uid}&configType={(int)GachaConfigType.ActivityAvatar}&endId={newestId + 1}&count=2", token: owner.AccessToken);
        Assert.Equal(ReturnCodes.Success, limited.Retcode);
        Assert.Equal(2, limited.DeserializeData<List<SimpleGachaItem>>()?.Count);

        TestResponse deleted = await SendAsync("GET", $"/GachaLog/Delete?Uid={uid}", token: owner.AccessToken);
        Assert.Equal(ReturnCodes.Success, deleted.Retcode);
        Assert.Equal(items.Items.Count, deleted.Data.GetInt32());

        TestResponse afterDelete = await SendAsync("GET", "/GachaLog/Entries", token: owner.AccessToken);
        Assert.DoesNotContain(afterDelete.DeserializeData<List<GachaEntry>>()!, entry => entry.Uid == uid);
    }

    [Fact]
    public async Task GachaLogUpload_BeyondFiveUids_IsRejected()
    {
        TestUser owner = await users.CreateUniqueAsync("gacha-slot", user => user.GachaLogExpireAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds());

        List<string> uids = [.. Enumerable.Range(0, 5).Select(_ => NewUid())];
        foreach (string uid in uids)
        {
            Assert.Equal(ReturnCodes.Success, (await SendAsync("POST", "/GachaLog/Upload", TestData.GachaItems(uid, count: 1), owner.AccessToken)).Retcode);
        }

        TestResponse sixth = await SendAsync("POST", "/GachaLog/Upload", TestData.GachaItems(NewUid(), count: 1), owner.AccessToken);

        Assert.Equal(ReturnCodes.TooManyGachaLogUids, sixth.Retcode);
        Assert.Equal(ServerKeys.ServerGachaLogServiceInsufficientRecordSlot, sixth.L10nKey);
    }

    [Fact]
    public async Task GachaLogUpload_WithZeroIdItem_IsRejected()
    {
        TestUser owner = await users.GachaLogAsync();
        UidAndItems items = TestData.GachaItems(NewUid(), count: 2);
        items.Items[0].Id = 0;

        TestResponse uploaded = await SendAsync("POST", "/GachaLog/Upload", items, owner.AccessToken);

        Assert.Equal(ReturnCodes.InvalidGachaLogItems, uploaded.Retcode);

        // 整批拒绝，一条也不会入库
        TestResponse entries = await SendAsync("GET", "/GachaLog/Entries", token: owner.AccessToken);
        Assert.DoesNotContain(entries.DeserializeData<List<GachaEntry>>()!, entry => entry.Uid == items.Uid);
    }

    [Theory]
    [InlineData("/GachaLog/Entries")]
    [InlineData("/GachaLog/EndIds?Uid=100000999")]
    public async Task GachaLogEndpoints_WithoutActiveService_AreRejected(string url)
    {
        TestUser plain = await users.PlainAsync();

        TestResponse parsed = await SendAsync("GET", url, token: plain.AccessToken);

        Assert.Equal(ReturnCodes.GachaLogServiceNotAllowed, parsed.Retcode);
        Assert.Equal(ServerKeys.ServerGachaLogServiceInsufficientTime, parsed.L10nKey);
    }

    [Fact]
    public async Task GachaLogStatistics_WhenNotComputed_ReportComputing()
    {
        TestUser owner = await users.GachaLogAsync();

        // 分布统计由外部生成器写入 GachaStatistics 表，测试库里没有数据时走「计算中」分支
        foreach (string url in new[]
        {
            "/GachaLog/Statistics/Distribution/AvatarEvent",
            "/GachaLog/Statistics/Distribution/WeaponEvent",
            "/GachaLog/Statistics/Distribution/Chronicled",
            "/GachaLog/Statistics/Distribution/Standard",
        })
        {
            TestResponse parsed = await SendAsync("GET", url, token: owner.AccessToken);

            Assert.Equal(ReturnCodes.ComputingStatistics, parsed.Retcode);
            Assert.Equal(ServerKeys.ServerGachaLogStatisticsUnavailable, parsed.L10nKey);
        }
    }

    [Fact]
    public async Task GachaLogStatistics_AfterSeeding_ReturnTheStoredData()
    {
        await factory.WithDbContextAsync(async context =>
        {
            context.GachaStatistics.Add(new server.Models.Entity.GachaLog.GachaStatistics
            {
                Name = server.Models.Entity.GachaLog.GachaStatistics.GachaEventStatistics,
                Data = """{"Status":1,"AvatarEvent":[],"AvatarEvent2":[],"WeaponEvent":[],"Chronicled":[],"InvalidUids":[],"PullsEnumerated":4321}""",
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        TestResponse parsed = await SendAsync("GET", "/GachaLog/Statistics/CurrentEventStatistics");

        Assert.Equal(ReturnCodes.Success, parsed.Retcode);
        Assert.Equal(ServerKeys.ServerGachaLogStatisticsSucceed, parsed.L10nKey);
        Assert.Equal(4321, parsed.Data.GetProperty("PullsEnumerated").GetInt64());
    }

    [Fact]
    public async Task DistributionEndpoints_ForCdnUser_ReturnTheDispatcherResults()
    {
        TestUser owner = await users.CdnAsync();

        TestResponse token = await SendAsync("GET", "/Distribution/GetDownloadToken", token: owner.AccessToken);
        Assert.Equal(ReturnCodes.Success, token.Retcode);
        Assert.Equal("stub-download-token", token.Data.GetString());

        TestResponse mirror = await SendAsync("GET", "/Distribution/GetAcceleratedMirror?Filename=hutao.zip", token: owner.AccessToken);
        Assert.Equal(ReturnCodes.Success, mirror.Retcode);
        // HutaoPackageMirror 用 snake_case 的 JsonPropertyName，出站也是这个拼写
        Assert.Equal("https://hutao-cdn.test/files/hutao.zip", mirror.Data.GetProperty("url").GetString());
    }

    [Fact]
    public async Task DistributionEndpoints_WithoutCdnService_AreRejected()
    {
        TestUser plain = await users.PlainAsync();

        TestResponse parsed = await SendAsync("GET", "/Distribution/GetDownloadToken", token: plain.AccessToken);

        Assert.Equal(ReturnCodes.GachaLogServiceNotAllowed, parsed.Retcode);
        Assert.Equal(ServerKeys.ServerCdnServiceInsufficientTime, parsed.L10nKey);
    }

    [Fact]
    public async Task HutaoLogUpload_ThenByDeviceId_ReturnTheStoredLog()
    {
        // HutaoLogSingleItem.DeviceId 限长 32
        string deviceId = $"cf-{Guid.NewGuid():N}".Substring(0, 32);
        string info = $"{{\"exception\":\"{Guid.NewGuid():N}\"}}";

        TestResponse first = await SendAsync("POST", "/HutaoLog/Upload", new { Id = deviceId, Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Info = info });
        Assert.Equal(ReturnCodes.Success, first.Retcode);
        Assert.Equal(ServerKeys.ServerHutaoLogUploadSucceed, first.L10nKey);

        // 相同 Info 视为同一条日志，只累加次数
        Assert.Equal(ReturnCodes.Success, (await SendAsync("POST", "/HutaoLog/Upload", new { Id = deviceId, Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Info = info })).Retcode);

        TestResponse query = await SendAsync("GET", $"/HutaoLog/ByDeviceId?id={deviceId}");
        Assert.Equal(ReturnCodes.Success, query.Retcode);

        List<server.Models.Entity.Telemetry.HutaoLog> logs = query.DeserializeData<List<server.Models.Entity.Telemetry.HutaoLog>>()!;
        Assert.Equal(2, logs.Count);
        Assert.All(logs, log => Assert.Equal(2, log.Count));
        Assert.All(logs, log => Assert.Equal("Snap Hutao/1.17.1.0", log.Version));
    }

    [Fact]
    public async Task HutaoLogUpload_WithoutDeviceId_IsRejected()
    {
        TestResponse parsed = await SendAsync("POST", "/HutaoLog/Upload", new { Id = (string?)null, Time = 0, Info = "{}" });

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    [Fact]
    public async Task ApplyOpenSourceLicense_WithRecaptchaPassed_IsRecordedAndNotRepeatable()
    {
        TestUser applicant = await users.CreateUniqueAsync("license-applicant");

        // 只放行这次请求的 token，其余请求仍走工厂里默认的失败桩
        string token = $"test-token-{Guid.NewGuid():N}";
        factory.StubHttp.MapJson(
            $"response={token}",
            """{"success":true,"score":0.9,"action":"ApplyOpenSourceLicense"}""");

        TestResponse applied = await SendAsync("POST", "/Accession/ApplyOpenSourceLicense", new { UserName = applicant.UserName, ProjectUrl = "https://github.com/example/repo", Token = token });
        Assert.Equal(ReturnCodes.Success, applied.Retcode);
        Assert.Equal(ServerKeys.ServerLicenseApplySucceed, applied.L10nKey);

        TestResponse twice = await SendAsync("POST", "/Accession/ApplyOpenSourceLicense", new { UserName = applicant.UserName, ProjectUrl = "https://github.com/example/repo", Token = token });
        Assert.Equal(ReturnCodes.AlreadyAppliedForLicense, twice.Retcode);

        // reCAPTCHA 通过但账号不存在
        string ghostToken = $"test-token-{Guid.NewGuid():N}";
        factory.StubHttp.MapJson($"response={ghostToken}", """{"success":true,"score":0.9,"action":"ApplyOpenSourceLicense"}""");
        TestResponse ghost = await SendAsync("POST", "/Accession/ApplyOpenSourceLicense", new { UserName = $"ghost-{Guid.NewGuid():N}@example.com", ProjectUrl = "https://github.com/example/repo", Token = ghostToken });
        Assert.Equal(ReturnCodes.UserNameNotExists, ghost.Retcode);
    }
}
