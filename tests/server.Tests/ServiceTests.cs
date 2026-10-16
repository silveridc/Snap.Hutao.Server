using System.Net;
using server.Models.Admin;
using server.Tests.Infrastructure;

namespace server.Tests;

/// <summary>
/// <c>/Service/*</c> 维护者后台接口：鉴权分支（未登录 / 非维护者 / 维护者）、
/// 参数校验分支，以及若干可验证副作用的写操作。
/// </summary>
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

    private async Task<TestResponse> SendAsync(string method, string url, string? json, string? token)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), url);
        request.WithToken(token);
        request.WithDevice();
        request.WithClientVersion();

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
    public async Task BanAdd_ThenRemove_RoundTripsThroughTheBanList()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string uid = "900000101";

        TestResponse added = await SendAsync("POST", $"/Service/Ban/Add?uid={uid}&reason=integration", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, added.Retcode);

        // 重复封禁同一个 Uid 不报错，只是不再新增
        TestResponse again = await SendAsync("POST", $"/Service/Ban/Add?uid={uid}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, again.Retcode);

        TestResponse list = await SendAsync("GET", $"/Service/Ban/List?page=1&pageSize=100&keyword={uid}", null, maintainer.AccessToken);
        PagedResult<BanItem>? page = list.DeserializeData<PagedResult<BanItem>>();
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.Uid == uid && item.Reason == "integration");

        TestResponse removed = await SendAsync("POST", $"/Service/Ban/Remove?uid={uid}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, removed.Retcode);

        TestResponse afterRemove = await SendAsync("GET", $"/Service/Ban/List?page=1&pageSize=100&keyword={uid}", null, maintainer.AccessToken);
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

        TestResponse parsed = await SendAsync("POST", $"/Service/Ban/Add?uid={Uri.EscapeDataString(uid)}", null, maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    [Fact]
    public async Task BanBatchAdd_ReturnsTheNumberOfNewlyBannedUids()
    {
        TestUser maintainer = await users.MaintainerAsync();
        List<string> uids = ["900000201", "900000202", "900000203"];

        string body = JsonSerializer.Serialize(new BatchUidRequest { Uids = uids, Reason = "batch" });
        TestResponse parsed = await SendAsync("POST", "/Service/Ban/BatchAdd", body, maintainer.AccessToken);

        Assert.Equal(ReturnCodes.Success, parsed.Retcode);
        Assert.Equal(uids.Count, parsed.Data.GetInt32());

        // 再批量封禁同样的 Uid 时新增数量为 0
        TestResponse again = await SendAsync("POST", "/Service/Ban/BatchAdd", body, maintainer.AccessToken);
        Assert.Equal(0, again.Data.GetInt32());
    }

    [Fact]
    public async Task AnnouncementLifecycle_UploadUpdateDisableDelete()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string title = $"integration-{Guid.NewGuid():N}";

        TestResponse uploaded = await SendAsync("POST", "/Service/Announcement/Upload", JsonSerializer.Serialize(new
        {
            Title = title,
            Content = "integration content",
            Severity = 0,
            Link = "https://example.com",
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, uploaded.Retcode);

        long id = await FindAnnouncementIdAsync(maintainer, title);
        Assert.True(id > 0, "The uploaded announcement should appear in the admin list");

        TestResponse updated = await SendAsync("POST", "/Service/Announcement/Update", JsonSerializer.Serialize(new
        {
            Id = id,
            Locale = "ALL",
            Title = title,
            Content = "updated content",
            Severity = 2,
            Link = "https://example.com",
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, updated.Retcode);

        TestResponse disabled = await SendAsync("POST", $"/Service/Announcement/SetEnabled?id={id}&enabled=false", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, disabled.Retcode);

        TestResponse deleted = await SendAsync("POST", $"/Service/Announcement/Delete?id={id}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, deleted.Retcode);

        // 删除后同一条公告再删一次应报不存在
        TestResponse again = await SendAsync("POST", $"/Service/Announcement/Delete?id={id}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.NotFound, again.Retcode);
    }

    [Theory]
    [InlineData("", "content")]
    [InlineData("title", "")]
    [InlineData("   ", "content")]
    public async Task AnnouncementUpdate_WithoutTitleOrContent_IsRejected(string title, string content)
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("POST", "/Service/Announcement/Update", JsonSerializer.Serialize(new
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

        TestResponse generated = await SendAsync("POST", "/Service/Redeem/Generate", JsonSerializer.Serialize(new
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
            "/Redeem/Use", new { Code = codes[0], Username = target.UserName }, token: null);

        TestResponse used = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, used.Retcode);
        Assert.Equal(7, used.Data.GetProperty("Value").GetInt32());
    }

    [Fact]
    public async Task RedeemUpdate_ThenDelete_RemovesTheCode()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse generated = await SendAsync("POST", "/Service/Redeem/Generate", JsonSerializer.Serialize(new
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

        TestResponse updated = await SendAsync("POST", "/Service/Redeem/Update", JsonSerializer.Serialize(new
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

        TestResponse deleted = await SendAsync("POST", $"/Service/Redeem/Delete?id={id}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, deleted.Retcode);

        TestResponse again = await SendAsync("POST", $"/Service/Redeem/Delete?id={id}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.NotFound, again.Retcode);
    }

    [Fact]
    public async Task AllowedVersionAdd_ThenRemove_RoundTrips()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string header = $"Integration Hutao/{Guid.NewGuid():N}";

        TestResponse added = await SendAsync("POST", $"/Service/AllowedVersion/Add?header={Uri.EscapeDataString(header)}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, added.Retcode);

        TestResponse list = await SendAsync("GET", "/Service/AllowedVersion/List", null, maintainer.AccessToken);
        Assert.Contains(header, list.DeserializeData<List<string>>()!);

        TestResponse removed = await SendAsync("POST", $"/Service/AllowedVersion/Remove?header={Uri.EscapeDataString(header)}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, removed.Retcode);

        TestResponse afterRemove = await SendAsync("GET", "/Service/AllowedVersion/List", null, maintainer.AccessToken);
        Assert.DoesNotContain(header, afterRemove.DeserializeData<List<string>>()!);
    }

    [Fact]
    public async Task ConfigUpdate_ThenReset_RestoresTheDefault()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string key = $"HutaoTest.Config.{Guid.NewGuid():N}";

        TestResponse updated = await SendAsync("POST", "/Service/Config/Update", JsonSerializer.Serialize(new Dictionary<string, string?> { [key] = "42" }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, updated.Retcode);

        TestResponse fetched = await SendAsync("GET", "/Service/Config/Get", null, maintainer.AccessToken);
        Assert.Equal("42", fetched.DeserializeData<Dictionary<string, string?>>()![key]);

        TestResponse reset = await SendAsync("POST", $"/Service/Config/Reset?key={key}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, reset.Retcode);

        TestResponse afterReset = await SendAsync("GET", "/Service/Config/Get", null, maintainer.AccessToken);
        Assert.DoesNotContain(key, afterReset.DeserializeData<Dictionary<string, string?>>()!.Keys);
    }

    [Fact]
    public async Task ConfigUpdate_WithEmptyBody_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("POST", "/Service/Config/Update", "{}", maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    [Fact]
    public async Task ConfigReset_WithoutKey_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("POST", "/Service/Config/Reset?key=", null, maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    [Fact]
    public async Task UserPrivilege_GrantsAndRevokesDeveloperLicense()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser target = await users.CreateUniqueAsync("service-privilege");

        TestResponse granted = await SendAsync("POST", "/Service/User/Privilege", JsonSerializer.Serialize(new UserPrivilegeRequest
        {
            UserId = target.Id,
            IsLicensedDeveloper = true,
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, granted.Retcode);

        TestResponse detail = await SendAsync("GET", $"/Service/User/Detail?userId={target.Id}", null, maintainer.AccessToken);
        Assert.True(detail.DeserializeData<UserDetail>()!.IsLicensedDeveloper);

        TestResponse revoked = await SendAsync("POST", "/Service/User/Privilege", JsonSerializer.Serialize(new UserPrivilegeRequest
        {
            UserId = target.Id,
            IsLicensedDeveloper = false,
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, revoked.Retcode);

        TestResponse afterRevoke = await SendAsync("GET", $"/Service/User/Detail?userId={target.Id}", null, maintainer.AccessToken);
        Assert.False(afterRevoke.DeserializeData<UserDetail>()!.IsLicensedDeveloper);
    }

    [Fact]
    public async Task UserPrivilege_RevokingOwnMaintainerFlag_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("POST", "/Service/User/Privilege", JsonSerializer.Serialize(new UserPrivilegeRequest
        {
            UserId = maintainer.Id,
            IsMaintainer = false,
        }), maintainer.AccessToken);

        Assert.Equal(ReturnCodes.ServiceKeyInvalid, parsed.Retcode);
    }

    [Fact]
    public async Task UserPrivilege_ForUnknownUser_ReportsMissingUser()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("POST", "/Service/User/Privilege", JsonSerializer.Serialize(new UserPrivilegeRequest
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

        TestResponse parsed = await SendAsync("POST", "/Service/User/ResetPassword", JsonSerializer.Serialize(new UserResetPasswordRequest
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

        TestResponse parsed = await SendAsync("POST", "/Service/User/ResetPassword", JsonSerializer.Serialize(new UserResetPasswordRequest
        {
            UserId = target.Id,
            NewPassword = newPassword,
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/Login",
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
            "GET", $"/Service/User/List?page=1&pageSize=10&keyword={Uri.EscapeDataString(target.UserName)}", null, maintainer.AccessToken);

        PagedResult<UserListItem>? page = parsed.DeserializeData<PagedResult<UserListItem>>();
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.Id == target.Id);
    }

    [Fact]
    public async Task UserStatistics_CountsAtLeastTheSeededAccounts()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("GET", "/Service/User/Statistics", null, maintainer.AccessToken);

        UserStatistics? statistics = parsed.DeserializeData<UserStatistics>();
        Assert.NotNull(statistics);
        Assert.True(statistics!.TotalUsers > 0);
        Assert.True(statistics.Maintainers >= 1);
    }

    [Fact]
    public async Task CacheInvalidate_ReportsRemovedKeys()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("POST", "/Service/Cache/Invalidate", JsonSerializer.Serialize(new
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

        TestResponse published = await SendAsync("POST", "/Service/Release/Publish", TestData.ReleasePublishJson(version), maintainer.AccessToken);
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
        TestResponse duplicate = await SendAsync("POST", "/Service/Release/Publish", TestData.ReleasePublishJson(version), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.InvalidUploadData, duplicate.Retcode);

        // 回滚到历史版本
        TestResponse activated = await SendAsync("POST", "/Service/Release/Activate", JsonSerializer.Serialize(new HutaoReleaseActivateRequest { Id = release.Id }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, activated.Retcode);
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

        TestResponse parsed = await SendAsync("POST", "/Service/Release/Publish", body, maintainer.AccessToken);

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

        TestResponse parsed = await SendAsync("POST", "/Service/Release/Publish", body, maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    [Fact]
    public async Task RankLeaderboard_WithoutKey_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("GET", "/Service/Rank/Leaderboard?key=", null, maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    [Fact]
    public async Task LicenseApprove_WithIncompleteParameters_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await SendAsync("POST", "/Service/License/Approve?userName=&code=", null, maintainer.AccessToken);

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
            "POST", $"/Service/License/Approve?userName={Uri.EscapeDataString(applicant.UserName)}&code={approvalCode}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);

        TestResponse detail = await SendAsync("GET", $"/Service/User/Detail?userId={applicant.Id}", null, maintainer.AccessToken);
        Assert.True(detail.DeserializeData<UserDetail>()!.IsLicensedDeveloper);
    }

    [Fact]
    public async Task AuditLog_RecordsPostOperationsWithRedactedDetails()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string marker = $"audit-{Guid.NewGuid():N}";

        // 用带 Description 的写操作产生一条审计记录
        TestResponse generated = await SendAsync("POST", "/Service/Redeem/Generate", JsonSerializer.Serialize(new
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

        TestResponse audit = await SendAsync("GET", "/Service/Audit/List?page=1&pageSize=100", null, maintainer.AccessToken);
        PagedResult<AuditLogItem>? page = audit.DeserializeData<PagedResult<AuditLogItem>>();
        Assert.NotNull(page);

        AuditLogItem? entry = page!.Items.FirstOrDefault(item => item.Detail?.Contains(marker, StringComparison.Ordinal) is true);
        Assert.NotNull(entry);
        Assert.Equal(maintainer.Id, entry!.AdminUserId);
        Assert.Equal("/Service/Redeem/Generate", entry.Action);
        Assert.True(entry.Success);
    }

    [Fact]
    public async Task AuditLog_NeverStoresPlaintextPasswords()
    {
        TestUser maintainer = await users.MaintainerAsync();
        const string secret = "SuperSecretPassword1";

        await SendAsync("POST", "/Service/User/ResetPassword", JsonSerializer.Serialize(new UserResetPasswordRequest
        {
            UserId = int.MaxValue,
            NewPassword = secret,
        }), maintainer.AccessToken);

        TestResponse audit = await SendAsync("GET", "/Service/Audit/List?page=1&pageSize=100", null, maintainer.AccessToken);
        PagedResult<AuditLogItem>? page = audit.DeserializeData<PagedResult<AuditLogItem>>();
        Assert.NotNull(page);

        Assert.DoesNotContain(page!.Items, item => item.Detail?.Contains(secret, StringComparison.Ordinal) is true);
        Assert.Contains(page.Items, item => item.Detail?.Contains("\"NewPassword\":\"***\"", StringComparison.Ordinal) is true);
    }

    [Fact]
    public async Task StatisticsRun_PopulatesThePublicOverview()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse run = await SendAsync("GET", "/Service/Statistics/Run", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, run.Retcode);

        using HttpResponseMessage overview = await client.GetWithTokenAsync("/Statistics/Overview", token: null);
        string body = await overview.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        TestResponse parsed = TestResponse.Parse(body);
        Assert.True(parsed.Retcode == ReturnCodes.Success, $"/Statistics/Overview failed: {body}");
    }

    private async Task<long> FindAnnouncementIdAsync(TestUser maintainer, string title)
    {
        TestResponse list = await SendAsync("GET", "/Service/Announcement/List?page=1&pageSize=100", null, maintainer.AccessToken);
        PagedResult<server.Models.Entity.Announcement.EntityAnnouncement>? page =
            list.DeserializeData<PagedResult<server.Models.Entity.Announcement.EntityAnnouncement>>();

        return page?.Items.FirstOrDefault(item => item.Title == title)?.Id ?? 0;
    }

    private async Task<uint> FindRedeemIdAsync(TestUser maintainer, string code)
    {
        TestResponse list = await SendAsync("GET", $"/Service/Redeem/List?page=1&pageSize=100&keyword={code}", null, maintainer.AccessToken);
        PagedResult<RedeemCodeItem>? page = list.DeserializeData<PagedResult<RedeemCodeItem>>();

        return page?.Items.FirstOrDefault(item => item.Code == code)?.Id ?? 0;
    }
}
