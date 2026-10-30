using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using server.Extension;
using server.Models.Admin;
using server.Models.Context;
using server.Models.Entity;
using server.Models.Patch;
using server.Options;
using server.Services.Admin;
using server.Services.PublicAPI;
using server.Tests.Infrastructure;
using System.Net;
using System.Security.Cryptography;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class ServiceTests
{
    private readonly HutaoServerFactory factory;
    private readonly HttpClient client;
    private readonly TestUsers users;

    public ServiceTests(HutaoServerFactory factory)
    {
        this.factory = factory;
        client = factory.CreateApiClient();
        users = factory.Users;
    }

    private async Task<TestResponse> SendAsync(string method, string url, string? json, string? token, string? realIp = null)
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

    public static TheoryData<string, string, string?> AllEndpoints => ServiceEndpoints.TheoryData;

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task WithoutToken_IsRejectedWithLoginFail(string method, string url, string? json)
    {
        TestResponse parsed = await SendAsync(method, url, json, token: null);

        // [Authorize] 失败时由 ResponseAuthorizationMiddlewareResultHandler 转成统一外壳，HTTP 状态码仍是 200
        Assert.Equal(ReturnCodes.LoginFail, parsed.Retcode);
    }

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task WithNonMaintainer_IsRejectedWithServiceKeyInvalid(string method, string url, string? json)
    {
        TestUser plain = await users.PlainAsync();

        TestResponse parsed = await SendAsync(method, url, json, plain.AccessToken);

        Assert.Equal(ReturnCodes.ServiceKeyInvalid, parsed.Retcode);
        Assert.Equal("Only official staff can perform this action", parsed.Message);
    }

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task WithMaintainer_ReachesTheBusinessLogic(string method, string url, string? json)
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync(method, url, json, maintainer.AccessToken);

        // 维护者身份必须能穿过权限过滤器：返回码应落在业务分支上，而不是鉴权失败
        Assert.NotEqual(ReturnCodes.ServiceKeyInvalid, parsed.Retcode);
        Assert.NotEqual(ReturnCodes.LoginFail, parsed.Retcode);
    }

    [Fact]
    public async Task MaintainerEndpoints_ReturnTheDocumentedRetcode()
    {
        TestUser maintainer = await users.MaintainerAsync();

        foreach (ServiceEndpoint endpoint in ServiceEndpoints.Rows)
        {
            TestResponse parsed = await SendAsync(endpoint.Method, endpoint.Url, endpoint.Json, maintainer.AccessToken);

            // 有状态的接口不带固定期望值，业务分支由各自的用例断言
            if (endpoint.MaintainerRetcode is int expected)
            {
                Assert.True(expected == parsed.Retcode,
                    $"{endpoint.Method} {endpoint.Url} expected retcode {expected} but got {parsed.Retcode} ({parsed.Message})");
            }
        }
    }

    [Fact]
    public async Task SystemOverview_ReturnsLiveProbeResults()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("GET", "/api/admin/system/overview", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);

        SystemOverview? overview = parsed.DeserializeData<SystemOverview>();
        Assert.NotNull(overview);

        Assert.False(string.IsNullOrWhiteSpace(overview!.Runtime.MachineName));
        Assert.True(overview.Runtime.ProcessId > 0);
        Assert.Single(overview.Databases);

        // 测试宿主自带真 MySQL 与进程内 Garnet，探针拿不到连接就说明采集本身是坏的
        Assert.All(overview.Databases, database => Assert.True(database.Connected, $"{database.Name}: {database.Error}"));
        Assert.All(overview.Databases, database => Assert.NotNull(database.RoundtripMs));
        Assert.True(overview.Redis.Connected, overview.Redis.Error);
        Assert.NotNull(overview.Redis.PingMs);

        // 测试宿主跑的是进程内 Garnet，INFO 需要 admin 模式；INFO 被拒时只允许它派生的字段留空
        Assert.True(!string.IsNullOrWhiteSpace(overview.Redis.Version) || !string.IsNullOrWhiteSpace(overview.Redis.Error));
        Assert.False(string.IsNullOrWhiteSpace(overview.Scheduler.State));
    }

    [Fact]
    public async Task BanAdd_ThenRemove_RoundTripsThroughTheBanList()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string uid = "900000101";

        TestResponse added = await SendAsync("POST", $"/api/admin/bans?uid={uid}&reason=integration", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, added.Retcode);

        // 重复封禁同一个 Uid 不报错，只是不再新增
        TestResponse again = await SendAsync("POST", $"/api/admin/bans?uid={uid}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, again.Retcode);

        TestResponse list = await SendAsync("GET", $"/api/admin/bans?page=1&pageSize=100&keyword={uid}", null, maintainer.AccessToken);
        PagedResult<BanItem>? page = list.DeserializeData<PagedResult<BanItem>>();
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.Uid == uid && item.Reason == "integration");

        TestResponse removed = await SendAsync("DELETE", $"/api/admin/bans/{uid}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, removed.Retcode);

        TestResponse afterRemove = await SendAsync("GET", $"/api/admin/bans?page=1&pageSize=100&keyword={uid}", null, maintainer.AccessToken);
        Assert.Empty(afterRemove.DeserializeData<PagedResult<BanItem>>()!.Items);
    }

    [Theory]
    [InlineData("12345")]          // 位数不足
    [InlineData("12345678901")]    // 位数过多
    [InlineData("10000000a")]      // 含非数字
    [InlineData("")]               // 空
    public async Task BanAdd_WithInvalidUid_IsRejected(string uid)
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("POST", $"/api/admin/bans?uid={Uri.EscapeDataString(uid)}", null, maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    [Fact]
    public async Task BanBatchAdd_ReturnsTheNumberOfNewlyBannedUids()
    {
        TestUser maintainer = await users.MaintainerAsync();
        List<string> uids = ["900000201", "900000202", "900000203"];

        string body = JsonSerializer.Serialize(new BatchUidRequest { Uids = uids, Reason = "batch" });
        TestResponse parsed = await SendAsync("POST", "/api/admin/bans/batch", body, maintainer.AccessToken);

        Assert.Equal(ReturnCodes.Success, parsed.Retcode);
        Assert.Equal(uids.Count, parsed.Data.GetInt32());

        // 再批量封禁同样的 Uid 时新增数量为 0
        TestResponse again = await SendAsync("POST", "/api/admin/bans/batch", body, maintainer.AccessToken);
        Assert.Equal(0, again.Data.GetInt32());
    }

    [Fact]
    public async Task AnnouncementLifecycle_UploadUpdateDisableDelete()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string title = $"integration-{Guid.NewGuid():N}";

        TestResponse uploaded = await SendAsync("POST", "/api/admin/announcements", JsonSerializer.Serialize(new
        {
            Title = title,
            Content = "integration content",
            Severity = 0,
            Link = "https://example.com",
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, uploaded.Retcode);

        long id = await FindAnnouncementIdAsync(maintainer, title);
        Assert.True(id > 0, "The uploaded announcement should appear in the admin list");

        TestResponse updated = await SendAsync("PUT", $"/api/admin/announcements/{id}", JsonSerializer.Serialize(new
        {
            Id = id,
            Locale = "ALL",
            Title = title,
            Content = "updated content",
            Severity = 2,
            Link = "https://example.com",
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, updated.Retcode);

        TestResponse disabled = await SendAsync("PATCH", $"/api/admin/announcements/{id}?enabled=false", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, disabled.Retcode);

        TestResponse deleted = await SendAsync("DELETE", $"/api/admin/announcements/{id}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, deleted.Retcode);

        // 删除后同一条公告再删一次应报不存在
        TestResponse again = await SendAsync("DELETE", $"/api/admin/announcements/{id}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.NotFound, again.Retcode);
    }

    [Theory]
    [InlineData("", "content")]
    [InlineData("title", "")]
    [InlineData("   ", "content")]
    public async Task AnnouncementUpdate_WithoutTitleOrContent_IsRejected(string title, string content)
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("PUT", "/api/admin/announcements/1", JsonSerializer.Serialize(new
        {
            Id = 1,
            Locale = "ALL",
            Title = title,
            Content = content,
            Link = "https://example.com",
        }), maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    [Fact]
    public async Task RedeemGenerate_ProducesAn18CharacterCodeThatCanBeRedeemed()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser target = await users.CreateUniqueAsync("service-redeem");

        TestResponse generated = await SendAsync("POST", "/api/admin/redeem-codes/generation", JsonSerializer.Serialize(new
        {
            Count = 2,
            Type = 1,
            ServiceType = 1,
            Value = 7,
            Description = "integration",
            ExpireTime = DateTimeOffset.UtcNow.AddDays(1),
            Times = 1,
        }), maintainer.AccessToken);

        Assert.Equal(ReturnCodes.Success, generated.Retcode);

        List<string>? codes = generated.Data.GetProperty("Codes").Deserialize<List<string>>();
        Assert.NotNull(codes);
        Assert.Equal(2, codes!.Count);
        Assert.All(codes, code => Assert.Equal(18, code.Length));

        // 生成的兑换码可以直接被普通用户使用，且给祈愿记录服务加 7 天
        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/api/redeem-codes/redemptions", new { Code = codes[0], Username = target.UserName }, token: null);

        TestResponse used = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, used.Retcode);
        Assert.Equal(7, used.Data.GetProperty("Value").GetInt32());
    }

    [Fact]
    public async Task RedeemUses_ListsTheUsersWhoRedeemedTheCode()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser target = await users.CreateUniqueAsync("redeem-uses");

        TestResponse generated = await SendAsync("POST", "/api/admin/redeem-codes/generation", JsonSerializer.Serialize(new
        {
            Count = 1,
            Type = 1,
            ServiceType = 1,
            Value = 3,
            Description = "uses-listing",
            ExpireTime = DateTimeOffset.UtcNow.AddDays(1),
            Times = 1,
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, generated.Retcode);

        string code = generated.Data.GetProperty("Codes").Deserialize<List<string>>()![0];
        uint id = await FindRedeemIdAsync(maintainer, code);

        // 兑换前没有使用记录
        TestResponse before = await SendAsync("GET", $"/api/admin/redeem-codes/{id}/usages", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, before.Retcode);
        Assert.Empty(before.DeserializeData<List<RedeemCodeUsageItem>>()!);

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/api/redeem-codes/redemptions", new { Code = code, Username = target.UserName }, token: null);
        Assert.Equal(ReturnCodes.Success, (await response.ReadTestResponseAsync()).Retcode);

        TestResponse after = await SendAsync("GET", $"/api/admin/redeem-codes/{id}/usages", null, maintainer.AccessToken);
        List<RedeemCodeUsageItem>? usages = after.DeserializeData<List<RedeemCodeUsageItem>>();
        Assert.NotNull(usages);
        RedeemCodeUsageItem usage = Assert.Single(usages!);
        Assert.Equal(target.Id, usage.UserId);
        Assert.Equal(target.UserName, usage.UserName);
    }

    [Fact]
    public async Task RedeemUpdate_ThenDelete_RemovesTheCode()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse generated = await SendAsync("POST", "/api/admin/redeem-codes/generation", JsonSerializer.Serialize(new
        {
            Count = 1,
            Type = 1,
            ServiceType = 2,
            Value = 3,
            Description = "before-update",
            ExpireTime = DateTimeOffset.UtcNow.AddDays(1),
            Times = 1,
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, generated.Retcode);

        string code = generated.Data.GetProperty("Codes")[0].GetString()!;
        uint id = await FindRedeemIdAsync(maintainer, code);

        TestResponse updated = await SendAsync("PUT", $"/api/admin/redeem-codes/{id}", JsonSerializer.Serialize(new
        {
            Id = id,
            Type = 1,
            ServiceType = 2,
            Value = 30,
            Description = "after-update",
            ExpireTime = DateTimeOffset.UtcNow.AddDays(2),
            TimesAllowed = 5,
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, updated.Retcode);

        TestResponse deleted = await SendAsync("DELETE", $"/api/admin/redeem-codes/{id}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, deleted.Retcode);

        TestResponse again = await SendAsync("DELETE", $"/api/admin/redeem-codes/{id}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.NotFound, again.Retcode);
    }

    [Fact]
    public async Task AllowedVersionAdd_ThenRemove_RoundTrips()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string header = $"Integration Hutao/{Guid.NewGuid():N}";

        TestResponse added = await SendAsync("POST", $"/api/admin/allowed-client-versions?header={Uri.EscapeDataString(header)}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, added.Retcode);

        TestResponse list = await SendAsync("GET", "/api/admin/allowed-client-versions", null, maintainer.AccessToken);
        Assert.Contains(header, list.DeserializeData<List<string>>()!);

        TestResponse removed = await SendAsync("DELETE", $"/api/admin/allowed-client-versions?header={Uri.EscapeDataString(header)}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, removed.Retcode);

        TestResponse afterRemove = await SendAsync("GET", "/api/admin/allowed-client-versions", null, maintainer.AccessToken);
        Assert.DoesNotContain(header, afterRemove.DeserializeData<List<string>>()!);
    }

    [Fact]
    public async Task ConfigUpdate_ThenReset_RestoresTheDefault()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string key = $"HutaoTest.Config.{Guid.NewGuid():N}";

        TestResponse updated = await SendAsync("PATCH", "/api/admin/settings", JsonSerializer.Serialize(new Dictionary<string, string?> { [key] = "42" }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, updated.Retcode);

        TestResponse fetched = await SendAsync("GET", "/api/admin/settings", null, maintainer.AccessToken);
        Assert.Equal("42", fetched.DeserializeData<Dictionary<string, string?>>()![key]);

        TestResponse reset = await SendAsync("DELETE", $"/api/admin/settings/{key}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, reset.Retcode);

        TestResponse afterReset = await SendAsync("GET", "/api/admin/settings", null, maintainer.AccessToken);
        Assert.DoesNotContain(key, afterReset.DeserializeData<Dictionary<string, string?>>()!.Keys);
    }

    [Fact]
    public async Task ConfigUpdate_WithEmptyBody_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("PATCH", "/api/admin/settings", "{}", maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    [Fact]
    public async Task UserPrivilege_GrantsAndRevokesDeveloperLicense()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser target = await users.CreateUniqueAsync("service-privilege");

        TestResponse granted = await SendAsync("PATCH", $"/api/admin/users/{target.Id}/privileges", JsonSerializer.Serialize(new UserPrivilegeRequest
        {
            UserId = target.Id,
            IsLicensedDeveloper = true,
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, granted.Retcode);

        TestResponse detail = await SendAsync("GET", $"/api/admin/users/{target.Id}", null, maintainer.AccessToken);
        Assert.True(detail.DeserializeData<UserDetail>()!.IsLicensedDeveloper);

        TestResponse revoked = await SendAsync("PATCH", $"/api/admin/users/{target.Id}/privileges", JsonSerializer.Serialize(new UserPrivilegeRequest
        {
            UserId = target.Id,
            IsLicensedDeveloper = false,
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, revoked.Retcode);

        TestResponse afterRevoke = await SendAsync("GET", $"/api/admin/users/{target.Id}", null, maintainer.AccessToken);
        Assert.False(afterRevoke.DeserializeData<UserDetail>()!.IsLicensedDeveloper);
    }

    [Fact]
    public async Task UserPrivilege_RevokingOwnMaintainerFlag_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("PATCH", $"/api/admin/users/{maintainer.Id}/privileges", JsonSerializer.Serialize(new UserPrivilegeRequest
        {
            UserId = maintainer.Id,
            IsMaintainer = false,
        }), maintainer.AccessToken);

        Assert.Equal(ReturnCodes.ServiceKeyInvalid, parsed.Retcode);
    }

    /// <summary>把默认管理员临时配成指定用户，用例结束还原，避免污染同集合的其他用例.</summary>
    private async Task WithDefaultAdminAsync(int userId, Func<Task> body)
    {
        AppOptions appOptions = factory.Services.GetRequiredService<AppOptions>();
        int configured = appOptions.DefaultAdminUserId;
        appOptions.DefaultAdminUserId = userId;
        try
        {
            await body();
        }
        finally
        {
            appOptions.DefaultAdminUserId = configured;
        }
    }

    private Task<TestResponse> PrivilegeAsync(TestUser caller, int userId, bool? isMaintainer = null, bool? isLicensedDeveloper = null)
    {
        return SendAsync("PATCH", $"/api/admin/users/{userId}/privileges", JsonSerializer.Serialize(new UserPrivilegeRequest
        {
            UserId = userId,
            IsMaintainer = isMaintainer,
            IsLicensedDeveloper = isLicensedDeveloper,
        }), caller.AccessToken);
    }

    private async Task<UserDetail> DetailAsync(TestUser caller, int userId)
    {
        TestResponse parsed = await SendAsync("GET", $"/api/admin/users/{userId}", null, caller.AccessToken);
        return parsed.DeserializeData<UserDetail>()!;
    }

    /// <summary>别的维护者降级默认管理员：拒绝，并且库里的权限保持原样.</summary>
    [Fact]
    public async Task UserPrivilege_DemotingDefaultAdmin_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser defaultAdmin = await users.CreateUniqueAsync("default-admin", user => user.IsMaintainer = true);

        await WithDefaultAdminAsync(defaultAdmin.Id, async () =>
        {
            TestResponse demoted = await PrivilegeAsync(maintainer, defaultAdmin.Id, isMaintainer: false);
            Assert.Equal(ReturnCodes.ServiceKeyInvalid, demoted.Retcode);
            Assert.Equal(ServerKeys.ServerAdminCannotDemoteDefaultAdmin, demoted.L10nKey);
            Assert.True((await DetailAsync(maintainer, defaultAdmin.Id)).IsMaintainer, "被拒绝的降级不能改到库里");
        });
    }

    /// <summary>默认管理员降级自己时先命中「不能撤自己」，两条守卫各管各的.</summary>
    [Fact]
    public async Task UserPrivilege_DefaultAdminDemotingSelf_ReportsSelfGuard()
    {
        TestUser selfAdmin = await users.CreateUniqueAsync("self-default-admin", user => user.IsMaintainer = true);

        await WithDefaultAdminAsync(selfAdmin.Id, async () =>
        {
            TestResponse parsed = await PrivilegeAsync(selfAdmin, selfAdmin.Id, isMaintainer: false);
            Assert.Equal(ReturnCodes.ServiceKeyInvalid, parsed.Retcode);
            Assert.Equal(ServerKeys.ServerAdminCannotRevokeOwnMaintainer, parsed.L10nKey);
        });
    }

    /// <summary>守卫只保护配置里的那一个账号，其他维护者照常可被降级.</summary>
    [Fact]
    public async Task UserPrivilege_OtherMaintainer_IsStillDemotableWhileDefaultAdminConfigured()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser defaultAdmin = await users.CreateUniqueAsync("default-admin-scope", user => user.IsMaintainer = true);
        TestUser other = await users.CreateUniqueAsync("other-admin", user => user.IsMaintainer = true);

        await WithDefaultAdminAsync(defaultAdmin.Id, async () =>
        {
            TestResponse demoted = await PrivilegeAsync(maintainer, other.Id, isMaintainer: false);
            Assert.Equal(ReturnCodes.Success, demoted.Retcode);
            Assert.False((await DetailAsync(maintainer, other.Id)).IsMaintainer);
        });
    }

    /// <summary>没配默认管理员（0）时，降级维护者的行为与加守卫之前完全一致.</summary>
    [Fact]
    public async Task UserPrivilege_WithoutDefaultAdminConfigured_DemotionSucceeds()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser other = await users.CreateUniqueAsync("unguarded-admin", user => user.IsMaintainer = true);

        await WithDefaultAdminAsync(0, async () =>
        {
            TestResponse demoted = await PrivilegeAsync(maintainer, other.Id, isMaintainer: false);
            Assert.Equal(ReturnCodes.Success, demoted.Retcode);
            Assert.False((await DetailAsync(maintainer, other.Id)).IsMaintainer);
        });
    }

    /// <summary>只拦 IsMaintainer=false：重新置为 true 或只改开发者授权都不受影响.</summary>
    [Fact]
    public async Task UserPrivilege_DefaultAdmin_KeepsEveryOtherPrivilegeWrite()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser defaultAdmin = await users.CreateUniqueAsync("default-admin-write", user => user.IsMaintainer = true);

        await WithDefaultAdminAsync(defaultAdmin.Id, async () =>
        {
            Assert.Equal(ReturnCodes.Success, (await PrivilegeAsync(maintainer, defaultAdmin.Id, isMaintainer: true)).Retcode);
            Assert.Equal(ReturnCodes.Success, (await PrivilegeAsync(maintainer, defaultAdmin.Id, isLicensedDeveloper: false)).Retcode);

            UserDetail detail = await DetailAsync(maintainer, defaultAdmin.Id);
            Assert.True(detail.IsMaintainer);
            Assert.False(detail.IsLicensedDeveloper);
        });
    }

    /// <summary>配置里的 Uid 查不到人时守卫照样生效，拦截发生在查人之前.</summary>
    [Fact]
    public async Task UserPrivilege_DefaultAdminIdWithoutMatchingUser_BlocksBeforeTheLookup()
    {
        TestUser maintainer = await users.MaintainerAsync();
        int ghostId = 1_000_001;

        await WithDefaultAdminAsync(ghostId, async () =>
        {
            TestResponse demoted = await PrivilegeAsync(maintainer, ghostId, isMaintainer: false);
            Assert.Equal(ReturnCodes.ServiceKeyInvalid, demoted.Retcode);

            TestResponse licensed = await PrivilegeAsync(maintainer, ghostId, isLicensedDeveloper: true);
            Assert.Equal(ReturnCodes.UserNameNotExists, licensed.Retcode);
        });
    }

    /// <summary>appsettings.json 里必须真的有这个键，键名拼错会让整条守卫静默失效.</summary>
    [Fact]
    public void DefaultAdminUserId_IsPresentInTheAppSection()
    {
        IConfiguration configuration = factory.Services.GetRequiredService<IConfiguration>();
        IConfigurationSection app = configuration.GetSection("App");

        Assert.Contains("DefaultAdminUserId", app.GetChildren().Select(section => section.Key), StringComparer.Ordinal);
        Assert.Equal(app.GetValue<int>("DefaultAdminUserId"), factory.Services.GetRequiredService<AppOptions>().DefaultAdminUserId);
    }

    [Fact]
    public async Task UserPrivilege_ForUnknownUser_ReportsMissingUser()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("PATCH", $"/api/admin/users/{int.MaxValue}/privileges", JsonSerializer.Serialize(new UserPrivilegeRequest
        {
            UserId = int.MaxValue,
            IsMaintainer = true,
        }), maintainer.AccessToken);

        Assert.Equal(ReturnCodes.UserNameNotExists, parsed.Retcode);
    }

    [Fact]
    public async Task UserResetPassword_WithShortPassword_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser target = await users.CreateUniqueAsync("service-resetpwd");

        TestResponse parsed = await SendAsync("PUT", $"/api/admin/users/{target.Id}/password", JsonSerializer.Serialize(new UserResetPasswordRequest
        {
            UserId = target.Id,
            NewPassword = "short",
        }), maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    [Fact]
    public async Task UserResetPassword_ChangesThePasswordOfTheTargetAccount()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser target = await users.CreateUniqueAsync("service-resetpwd-ok");
        const string newPassword = "AdminIssuedPassword1";

        TestResponse parsed = await SendAsync("PUT", $"/api/admin/users/{target.Id}/password", JsonSerializer.Serialize(new UserResetPasswordRequest
        {
            UserId = target.Id,
            NewPassword = newPassword,
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/api/passport/v2/sessions",
            new Dictionary<string, object?>
            {
                ["UserName"] = RsaPayload.Encrypt(target.UserName),
                ["Password"] = RsaPayload.Encrypt(newPassword),
            },
            token: null);

        Assert.Equal(ReturnCodes.Success, (await response.ReadTestResponseAsync()).Retcode);
    }

    [Fact]
    public async Task UserList_FiltersByKeywordAndPaging()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser target = await users.CreateUniqueAsync("service-list-keyword");

        TestResponse parsed = await SendAsync(
            "GET", $"/api/admin/users?page=1&pageSize=10&keyword={Uri.EscapeDataString(target.UserName)}", null, maintainer.AccessToken);

        PagedResult<UserListItem>? page = parsed.DeserializeData<PagedResult<UserListItem>>();
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.Id == target.Id);
    }

    [Fact]
    public async Task UserStatistics_CountsAtLeastTheSeededAccounts()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("GET", "/api/admin/users/statistics", null, maintainer.AccessToken);

        UserStatistics? statistics = parsed.DeserializeData<UserStatistics>();
        Assert.NotNull(statistics);
        Assert.True(statistics!.TotalUsers > 0);
        Assert.True(statistics.Maintainers >= 1);
    }

    [Fact]
    public async Task CacheInvalidate_ReportsRemovedKeys()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("DELETE", "/api/admin/cache", JsonSerializer.Serialize(new
        {
            Keys = new[] { "HutaoTest.Missing" },
            Patterns = new[] { "HutaoTest.Missing:*" },
        }), maintainer.AccessToken);

        Assert.Equal(ReturnCodes.Success, parsed.Retcode);
        Assert.Equal(1, parsed.Data.GetProperty("RemovedKeys").GetInt32());
        Assert.Equal(0, parsed.Data.GetProperty("RemovedByPattern").GetInt64());
    }

    [Fact]
    public async Task ReleasePublish_ThenActivate_MakesTheVersionCurrent()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string version = $"1.99.{Random.Shared.Next(1000, 9999)}.0";

        TestResponse published = await SendAsync("POST", "/api/admin/releases", TestData.ReleasePublishJson(version), maintainer.AccessToken);
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
        TestResponse duplicate = await SendAsync("POST", "/api/admin/releases", TestData.ReleasePublishJson(version), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.InvalidUploadData, duplicate.Retcode);

        // 回滚到历史版本
        TestResponse activated = await SendAsync("PUT", $"/api/admin/releases/{release.Id}/activation", JsonSerializer.Serialize(new HutaoReleaseActivateRequest { Id = release.Id }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, activated.Retcode);

        // 下架当前版本：/patch/hutao 不再返回它；再激活恢复现场
        TestResponse deactivated = await SendAsync("DELETE", $"/api/admin/releases/{release.Id}/activation", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, deactivated.Retcode);

        using HttpResponseMessage after = await client.GetWithTokenAsync("/patch/hutao", token: null);
        TestResponse afterParsed = await after.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, afterParsed.Retcode);
        Assert.True(
            afterParsed.Data.ValueKind is JsonValueKind.Null || !afterParsed.Data.TryGetProperty("version", out JsonElement afterVersion) || afterVersion.GetString() != version,
            $"下架后 /patch/hutao 仍返回 {version}");

        TestResponse reactivated = await SendAsync("PUT", $"/api/admin/releases/{release.Id}/activation", JsonSerializer.Serialize(new HutaoReleaseActivateRequest { Id = release.Id }), maintainer.AccessToken);
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

        TestResponse published = await SendAsync("POST", "/api/admin/releases", TestData.ReleasePublishJson(version, minimum), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, published.Retcode);
        Assert.Equal(minimum, published.DeserializeData<HutaoReleaseItem>()!.MinimumVersion);

        using HttpResponseMessage patch = await client.GetWithTokenAsync("/patch/hutao", token: null);
        TestResponse patchParsed = await patch.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, patchParsed.Retcode);
        Assert.Equal(minimum, patchParsed.Data.GetProperty("minimumVersion").GetString());

        // 最低版本不得超过发布版本本身
        string later = $"1.97.{Random.Shared.Next(1000, 9999)}.0";
        TestResponse rejected = await SendAsync("POST", "/api/admin/releases", TestData.ReleasePublishJson(later, "1.99.0.0"), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.InvalidUploadData, rejected.Retcode);

        // 不带 minimumVersion 的发布（换版本号避开唯一索引），下发里字段应缺席
        TestResponse plain = await SendAsync("POST", "/api/admin/releases", TestData.ReleasePublishJson(later), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, plain.Retcode);
        using HttpResponseMessage patchAgain = await client.GetWithTokenAsync("/patch/hutao", token: null);
        TestResponse parsed = await patchAgain.ReadTestResponseAsync();
        Assert.Equal(later, parsed.Data.GetProperty("version").GetString());
        Assert.False(parsed.Data.TryGetProperty("minimumVersion", out _), "未配置强制版本时字段应省略");
    }

    /// <summary>
    /// 功能熔断链路：后台开启 Patch.DisableFpsUnlock / Patch.DisableIslandInjection 后，
    /// /patch/hutao 下发 true；关闭时字段省略（客户端视为 false，恢复用户本地设置）.
    /// </summary>
    [Fact]
    public async Task PatchKillSwitches_AreServedOnPatchEndpoint()
    {
        SystemConfigService config = factory.GetService<SystemConfigService>();
        try
        {
            await config.UpdateAsync(new Dictionary<string, string?>
            {
                [Keys.PatchDisableFpsUnlock] = "true",
                [Keys.PatchDisableIslandInjection] = "true",
                [Keys.PatchDisableFpsUnlockReason] = "游戏版本更新，帧率解锁暂不可用",
                [Keys.PatchDisableIslandInjectionReason] = "Island 注入正在修复",
            }, 0);

            using HttpResponseMessage on = await client.GetAsync("/patch/hutao", TestContext.Current.CancellationToken);
            TestResponse parsedOn = await on.ReadTestResponseAsync();
            Assert.Equal(ReturnCodes.Success, parsedOn.Retcode);
            Assert.True(parsedOn.Data.GetProperty("fpsUnlockDisabled").GetBoolean());
            Assert.True(parsedOn.Data.GetProperty("islandInjectionDisabled").GetBoolean());
            Assert.Equal("游戏版本更新，帧率解锁暂不可用", parsedOn.Data.GetProperty("fpsUnlockDisabledReason").GetString());
            Assert.Equal("Island 注入正在修复", parsedOn.Data.GetProperty("islandInjectionDisabledReason").GetString());
        }
        finally
        {
            await config.DeleteAsync(Keys.PatchDisableFpsUnlock);
            await config.DeleteAsync(Keys.PatchDisableIslandInjection);
            await config.DeleteAsync(Keys.PatchDisableFpsUnlockReason);
            await config.DeleteAsync(Keys.PatchDisableIslandInjectionReason);
        }

        using HttpResponseMessage off = await client.GetAsync("/patch/hutao", TestContext.Current.CancellationToken);
        TestResponse parsedOff = await off.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, parsedOff.Retcode);
        Assert.False(parsedOff.Data.TryGetProperty("fpsUnlockDisabled", out _), "未熔断时字段应省略");
        Assert.False(parsedOff.Data.TryGetProperty("islandInjectionDisabled", out _), "未熔断时字段应省略");
        Assert.False(parsedOff.Data.TryGetProperty("fpsUnlockDisabledReason", out _), "未熔断时原因应省略");
        Assert.False(parsedOff.Data.TryGetProperty("islandInjectionDisabledReason", out _), "未熔断时原因应省略");
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

        TestResponse parsed = await SendAsync("POST", "/api/admin/releases", body, maintainer.AccessToken);

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

        TestResponse parsed = await SendAsync("POST", "/api/admin/releases", body, maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    private const string BucketHost = "patch.test.s3.local";

    private async Task SetBucketConfigAsync(TestUser maintainer, bool enabled)
    {
        Dictionary<string, string?> settings = new()
        {
            ["Patch.S3.Host"] = enabled ? BucketHost : string.Empty,
            ["Patch.S3.Region"] = enabled ? "cn-test-1" : string.Empty,
            ["Patch.S3.AccessKeyId"] = enabled ? "test-access-key" : string.Empty,
            ["Patch.S3.SecretAccessKey"] = enabled ? "test-secret-key" : string.Empty,
        };

        TestResponse updated = await SendAsync("PATCH", "/api/admin/settings", JsonSerializer.Serialize(settings), maintainer.AccessToken);
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
            TestResponse listed = await SendAsync("GET", "/api/admin/releases/bucket-objects?prefix=patch/", null, maintainer.AccessToken);
            Assert.Equal(ReturnCodes.Success, listed.Retcode);

            ReleaseBucketListResult? list = listed.DeserializeData<ReleaseBucketListResult>();
            Assert.NotNull(list);
            Assert.Equal(BucketHost, list!.Host);
            ReleaseBucketObjectItem item = Assert.Single(list.Objects);
            Assert.Equal(key, item.Key);
            Assert.Equal(payload.Length, item.Size);

            TestResponse published = await SendAsync(
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
            TestResponse published = await SendAsync(
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

        TestResponse published = await SendAsync(
            "POST",
            "/api/admin/releases/from-bucket",
            JsonSerializer.Serialize(new HutaoReleasePublishFromBucketRequest { Key = "patch/Snap.Hutao.1.94.0.0.msix" }),
            maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, published.Retcode);
        Assert.Equal(ServerKeys.ServerAdminReleaseBucketNotConfigured, published.L10nKey);
    }

    [Fact]
    public async Task LicenseApprove_WithIncompleteParameters_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("POST", "/api/admin/license-applications/approval?userName=&code=", null, maintainer.AccessToken);

        Assert.Equal(ReturnCodes.LicenseApprovalFailed, parsed.Retcode);
    }

    [Fact]
    public async Task LicenseApprove_ForAnExistingApplication_GrantsDeveloperLicense()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser applicant = await users.CreateUniqueAsync("service-license");
        const string approvalCode = "INTEGRATIONAPPROVALCODE00000001";

        await factory.WithDbContextAsync(async db =>
        {
            db.Licenses.Add(new server.Models.Entity.LicenseApplicationRecord
            {
                UserId = applicant.Id,
                ProjectUrl = "https://example.com/project",
                ApprovalCode = approvalCode,
            });
            await db.SaveChangesAsync();
        });

        TestResponse parsed = await SendAsync(
            "POST", $"/api/admin/license-applications/approval?userName={Uri.EscapeDataString(applicant.UserName)}&code={approvalCode}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);

        TestResponse detail = await SendAsync("GET", $"/api/admin/users/{applicant.Id}", null, maintainer.AccessToken);
        Assert.True(detail.DeserializeData<UserDetail>()!.IsLicensedDeveloper);
    }

    [Fact]
    public async Task AuditLog_RecordsPostOperationsWithRedactedDetails()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string marker = $"audit-{Guid.NewGuid():N}";

        // 用带 Description 的写操作产生一条审计记录
        TestResponse generated = await SendAsync("POST", "/api/admin/redeem-codes/generation", JsonSerializer.Serialize(new
        {
            Count = 1,
            Type = 1,
            ServiceType = 1,
            Value = 1,
            Description = marker,
            ExpireTime = DateTimeOffset.UtcNow.AddDays(1),
            Times = 1,
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, generated.Retcode);

        TestResponse audit = await SendAsync("GET", "/api/admin/audit-logs?page=1&pageSize=100", null, maintainer.AccessToken);
        PagedResult<AuditLogItem>? page = audit.DeserializeData<PagedResult<AuditLogItem>>();
        Assert.NotNull(page);

        AuditLogItem? entry = page!.Items.FirstOrDefault(item => item.Detail?.Contains(marker, StringComparison.Ordinal) is true);
        Assert.NotNull(entry);
        Assert.Equal(maintainer.Id, entry!.AdminUserId);
        Assert.Equal("/api/admin/redeem-codes/generation", entry.Action);
        Assert.True(entry.Success);

        // 测试宿主不经过反向代理，RemoteIpAddress 就是回环地址
        Assert.False(string.IsNullOrWhiteSpace(entry.Ip), "审计日志必须记录来源 IP");
        Assert.True(IPAddress.TryParse(entry.Ip, out _), $"记录的不是合法 IP：{entry.Ip}");
    }

    /// <summary>回环地址在内置库里没有属地，落库时应当留空而不是写半截字符串.</summary>
    [Fact]
    public async Task AuditLog_LoopbackRequest_HasNoRegionButKeepsIp()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string marker = $"audit-region-{Guid.NewGuid():N}";

        await SendAsync("POST", "/api/admin/redeem-codes/generation", JsonSerializer.Serialize(new
        {
            Count = 1,
            Type = 1,
            ServiceType = 1,
            Value = 1,
            Description = marker,
            ExpireTime = DateTimeOffset.UtcNow.AddDays(1),
            Times = 1,
        }), maintainer.AccessToken);

        TestResponse audit = await SendAsync("GET", "/api/admin/audit-logs?page=1&pageSize=100", null, maintainer.AccessToken);
        PagedResult<AuditLogItem>? page = audit.DeserializeData<PagedResult<AuditLogItem>>();
        AuditLogItem? entry = page!.Items.FirstOrDefault(item => item.Detail?.Contains(marker, StringComparison.Ordinal) is true);

        Assert.NotNull(entry);
        Assert.NotNull(entry!.Ip);
        Assert.Null(entry.IpRegion);
    }

    /// <summary>属地不落库：审计只存真实来源 IP，列表读取时按它现查出四段数组.</summary>
    [Fact]
    public async Task AuditLog_ResolvesRegionFromTheStoredIp()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string marker = $"audit-region-{Guid.NewGuid():N}";

        // 带 X-Real-IP 走一次真实写操作，ForwardedHeaders 会把它还原成 RemoteIpAddress
        await SendAsync("POST", "/api/admin/redeem-codes/generation", JsonSerializer.Serialize(new
        {
            Count = 1,
            Type = 1,
            ServiceType = 1,
            Value = 1,
            Description = marker,
            ExpireTime = DateTimeOffset.UtcNow.AddDays(1),
            Times = 1,
        }), maintainer.AccessToken, realIp: "223.5.5.5");

        TestResponse audit = await SendAsync("GET", "/api/admin/audit-logs?page=1&pageSize=100", null, maintainer.AccessToken);
        AuditLogItem? entry = audit.DeserializeData<PagedResult<AuditLogItem>>()!.Items
            .FirstOrDefault(item => item.Detail?.Contains(marker, StringComparison.Ordinal) is true);

        Assert.NotNull(entry);
        Assert.Equal("223.5.5.5", entry!.Ip);
        Assert.Equal("中国/浙江省/杭州市/阿里", string.Join('/', entry.IpRegion!));
    }

    /// <summary>现查的结果按 IP 缓存，审计翻页不重复算；非法地址直接算成没有属地.</summary>
    [Fact]
    public async Task IpRegion_IsCachedByIp()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        string?[]? region = await scope.ServiceProvider.GetRequiredService<IpRegionService>().GetRegionAsync("180.101.50.188");

        Assert.NotNull(region);
        Assert.Equal(4, region!.Length);
        Assert.True(scope.ServiceProvider.GetRequiredService<IDistributedCache>().TryGetValue("IpRegion:180.101.50.188", out string?[]? cached));
        Assert.Equal(region, cached!);

        Assert.Null(await scope.ServiceProvider.GetRequiredService<IpRegionService>().GetRegionAsync("not-an-ip"));
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IpRegionService>().GetRegionAsync(null));
    }

    [Fact]
    public async Task AuditLog_NeverStoresPlaintextPasswords()
    {
        TestUser maintainer = await users.MaintainerAsync();
        const string secret = "SuperSecretPassword1";

        await SendAsync("PUT", $"/api/admin/users/{int.MaxValue}/password", JsonSerializer.Serialize(new UserResetPasswordRequest
        {
            UserId = int.MaxValue,
            NewPassword = secret,
        }), maintainer.AccessToken);

        TestResponse audit = await SendAsync("GET", "/api/admin/audit-logs?page=1&pageSize=100", null, maintainer.AccessToken);
        PagedResult<AuditLogItem>? page = audit.DeserializeData<PagedResult<AuditLogItem>>();
        Assert.NotNull(page);

        Assert.DoesNotContain(page!.Items, item => item.Detail?.Contains(secret, StringComparison.Ordinal) is true);
        Assert.Contains(page.Items, item => item.Detail?.Contains("\"NewPassword\":\"***\"", StringComparison.Ordinal) is true);
    }

    [Fact]
    public async Task StatisticsRun_PopulatesThePublicOverview()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse run = await SendAsync("POST", "/api/admin/statistics/refresh", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, run.Retcode);

        using HttpResponseMessage overview = await client.GetWithTokenAsync("/api/statistics/overview", token: null);
        string body = await overview.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        TestResponse parsed = TestResponse.Parse(body);
        Assert.True(parsed.Retcode == ReturnCodes.Success, $"/api/statistics/overview failed: {body}");
    }

    private async Task<long> FindAnnouncementIdAsync(TestUser maintainer, string title)
    {
        TestResponse list = await SendAsync("GET", "/api/admin/announcements?page=1&pageSize=100", null, maintainer.AccessToken);
        PagedResult<server.Models.Entity.Announcement.EntityAnnouncement>? page =
            list.DeserializeData<PagedResult<server.Models.Entity.Announcement.EntityAnnouncement>>();

        return page?.Items.FirstOrDefault(item => item.Title == title)?.Id ?? 0;
    }

    private async Task<uint> FindRedeemIdAsync(TestUser maintainer, string code)
    {
        TestResponse list = await SendAsync("GET", $"/api/admin/redeem-codes?page=1&pageSize=100&keyword={code}", null, maintainer.AccessToken);
        PagedResult<RedeemCodeItem>? page = list.DeserializeData<PagedResult<RedeemCodeItem>>();

        return page?.Items.FirstOrDefault(item => item.Code == code)?.Id ?? 0;
    }
}