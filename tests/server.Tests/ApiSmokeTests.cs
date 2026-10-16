using System.Net;
using System.Text;
using server.Tests.Infrastructure;

namespace server.Tests;

/// <summary>
/// 全量接口冒烟：遍历所有已注册的 HTTP 路由，用合法入参确认没有接口返回 500，
/// 且除第三方透传接口外都返回 Snap Hutao 统一响应外壳。
/// 逐个接口的语义断言见其余测试类。
/// </summary>
[Collection(HutaoServerCollection.Name)]
public sealed class ApiSmokeTests
{
    private readonly HutaoServerFactory factory;
    private readonly HttpClient client;

    public ApiSmokeTests(HutaoServerFactory factory)
    {
        this.factory = factory;
        client = factory.CreateApiClient();
    }

    /// <summary>不返回统一响应外壳的接口前缀（第三方透传、302 重定向、回调协议自有格式）。</summary>
    private static readonly string[] RawEndpointPrefixes =
    [
        "/enka",
        "/OAuth/Auth",
        "/OAuth/Callback",
        "/Webhook",
    ];

    /// <summary>每次运行都使用不同账号，避免与其它用例的验证码缓存互相干扰。</summary>
    private static string UniqueUser(string tag) => $"smoke-{tag}-{Guid.NewGuid():N}@example.com";

    /// <summary>
    /// 构造 PassportRequest 请求体。UserName / Password / VerifyCode 等字段
    /// 在客户端均以 RSA-OAEP-SHA1 加密后提交，这里按同样规则构造。
    /// </summary>
    private static string PassportBody(
        string? userName = null,
        string? password = null,
        string? verifyCode = null,
        string? newUserName = null,
        string? newVerifyCode = null)
    {
        List<string> fields = [];
        if (userName is not null)
        {
            fields.Add($"\"UserName\":\"{RsaPayload.Encrypt(userName)}\"");
        }

        if (password is not null)
        {
            fields.Add($"\"Password\":\"{RsaPayload.Encrypt(password)}\"");
        }

        if (verifyCode is not null)
        {
            fields.Add($"\"VerifyCode\":\"{RsaPayload.Encrypt(verifyCode)}\"");
        }

        if (newUserName is not null)
        {
            fields.Add($"\"NewUserName\":\"{RsaPayload.Encrypt(newUserName)}\"");
        }

        if (newVerifyCode is not null)
        {
            fields.Add($"\"NewVerifyCode\":\"{RsaPayload.Encrypt(newVerifyCode)}\"");
        }

        return "{" + string.Join(',', fields) + "}";
    }

    public static TheoryData<string, string, string?> Endpoints => new()
    {
        // 客户端更新
        { "GET", "/patch/hutao", null },

        // 公告
        { "POST", "/Announcement/List", "[]" },
        { "POST", "/Announcement/List", "[1,2,3]" },

        // 深渊统计
        { "GET", "/Statistics/Overview", null },
        { "GET", "/Statistics/Overview?Last=true", null },
        { "GET", "/Statistics/Avatar/AttendanceRate", null },
        { "GET", "/Statistics/Avatar/AttendanceRate?Last=true", null },
        { "GET", "/Statistics/Avatar/UtilizationRate", null },
        { "GET", "/Statistics/Avatar/HoldingRate", null },
        { "GET", "/Statistics/Avatar/AvatarCollocation", null },
        { "GET", "/Statistics/Weapon/WeaponCollocation", null },
        { "GET", "/Statistics/Team/Combination", null },

        // 深渊记录
        { "GET", "/Record/Check?Uid=100000001", null },
        { "GET", "/Record/Rank?Uid=100000001", null },
        { "POST", "/Record/Upload", TestData.SimpleRecordJson("100000001") },
        { "POST", "/Record/Upload?returningRank=true", TestData.SimpleRecordJson("100000002") },

        // 幻想真境剧诗
        { "GET", "/RoleCombat/Statistics", null },
        { "POST", "/RoleCombat/Upload", TestData.RoleCombatRecordJson("100000003") },

        // 祈愿记录（统计接口公开，其余需要登录）
        { "GET", "/GachaLog/Statistics/CurrentEventStatistics", null },
        { "GET", "/GachaLog/Statistics/Distribution/AvatarEvent", null },
        { "GET", "/GachaLog/Statistics/Distribution/WeaponEvent", null },
        { "GET", "/GachaLog/Statistics/Distribution/Chronicled", null },
        { "GET", "/GachaLog/Statistics/Distribution/Standard", null },
        { "GET", "/GachaLog/Entries", null },
        { "GET", "/GachaLog/EndIds?Uid=100000001", null },
        { "POST", "/GachaLog/Retrieve", """{"Uid":"100000001","EndIds":{}}""" },
        { "GET", "/GachaLog/LimitedRetrieve?uid=100000001&configType=301", null },
        { "POST", "/GachaLog/Upload", TestData.GachaItemsJson("100000001") },
        { "GET", "/GachaLog/Delete?Uid=100000001", null },

        // 账户 v2
        { "GET", "/Passport/v2/UserInfo", null },
        { "GET", "/Passport/v2/LoggedInDevices", null },
        { "POST", "/Passport/v2/RefreshToken", """{"RefreshToken":""}""" },
        { "POST", "/Passport/v2/RevokeToken", """{"DeviceId":""}""" },
        { "POST", "/Passport/v2/RevokeAllTokens", null },
        { "POST", "/Passport/v2/Verify", PassportBody(userName: UniqueUser("v2-verify")) },
        { "POST", "/Passport/v2/Register", PassportBody(userName: UniqueUser("v2-register"), password: "TestPassword123", verifyCode: "INVALID0") },
        { "POST", "/Passport/v2/Cancel", PassportBody(userName: UniqueUser("v2-cancel"), password: "TestPassword123", verifyCode: "INVALID0") },
        { "POST", "/Passport/v2/ResetPassword", PassportBody(userName: UniqueUser("v2-resetpwd"), password: "TestPassword123", verifyCode: "INVALID0") },
        { "POST", "/Passport/v2/ResetUsername", PassportBody(userName: UniqueUser("v2-resetname"), verifyCode: "INVALID0", newUserName: UniqueUser("v2-resetname-new"), newVerifyCode: "INVALID1") },
        { "POST", "/Passport/v2/Login", PassportBody(userName: UniqueUser("v2-login"), password: "TestPassword123") },

        // 账户（旧版，已标记 Obsolete）
        { "GET", "/Passport/UserInfo", null },
        { "POST", "/Passport/Login", PassportBody(userName: UniqueUser("login"), password: "TestPassword123") },
        { "POST", "/Passport/Verify", PassportBody(userName: UniqueUser("verify")) },
        { "POST", "/Passport/Register", PassportBody(userName: UniqueUser("register"), password: "TestPassword123", verifyCode: "INVALID0") },
        { "POST", "/Passport/Cancel", PassportBody(userName: UniqueUser("cancel"), password: "TestPassword123", verifyCode: "INVALID0") },
        { "POST", "/Passport/ResetPassword", PassportBody(userName: UniqueUser("resetpwd"), password: "TestPassword123", verifyCode: "INVALID0") },
        { "POST", "/Passport/ResetUsername", PassportBody(userName: UniqueUser("resetname"), verifyCode: "INVALID0", newUserName: UniqueUser("resetname-new"), newVerifyCode: "INVALID1") },

        // CDN 分发
        { "GET", "/Distribution/GetDownloadToken", null },
        { "GET", "/Distribution/GetAcceleratedMirror?Filename=test.zip", null },

        // 兑换码
        { "POST", "/Redeem/Use", """{"code":"000000000000000000"}""" },

        // 开源许可
        { "POST", "/Accession/ApplyOpenSourceLicense", """{"UserName":"a@example.com","ProjectUrl":"https://example.com","Token":"token"}""" },

        // 遥测日志
        { "POST", "/HutaoLog/Upload", """{"Id":"device-1","Time":1700000000,"Info":"{\"exception\":\"test\"}"}""" },
        { "GET", "/HutaoLog/ByDeviceId?id=device-1", null },

        // OAuth
        { "GET", "/OAuth/Auth?provider=Github&device_id=d&device_name=n&device_os=Windows&callback=https://example.com", null },
        { "GET", "/OAuth/Status?provider=Github", null },
        { "GET", "/OAuth/Unbind?provider=Github", null },

        // 伪造的 state 不是合法密文，回调应拒绝而不是 500（成功分支见 OAuthTests）
        { "GET", "/OAuth/Callback/GitHub?code=smoke&state=c21va2U=", null },

        // 爱发电订单回调：未知 sku 不做外部校验，直接判定为不支持（成功分支见 WebhookTests）
        { "POST", "/Webhook", TestData.AfdianOrderJson("smoke-unknown-sku", "SKU-NOT-CONFIGURED", 1, "smoke@example.com") },


        // 公共 API 与第三方透传
        { "GET", "/publicapi/ip", null },
        { "GET", "/enka/100000001", null },
        { "GET", "/enka/100000001/info", null },
        { "GET", "/Wallpaper/bing", null },
        { "GET", "/Wallpaper/today", null },
        { "GET", "/Wallpaper/hoyoplay", null },
    };

    /// <summary>
    /// 覆盖性校验：宿主实际注册的每一条路由都必须出现在冒烟清单或维护接口清单里，
    /// 清单里的每一条地址也必须命中一条实际路由。新增接口忘了补用例、或接口被改名后
    /// 清单里留下打不到人的死地址，都会在这里暴露。
    /// </summary>
    [Fact]
    public void EveryRegisteredRoute_IsListedInTheSmokeTables()
    {
        List<RouteKey> registered = [.. RegisteredRoutes.Enumerate(factory.Services)];
        Assert.True(registered.Count > 0, "未能从宿主枚举到任何路由，覆盖性校验失去意义");

        List<(string Method, string Url)> listed =
        [
            .. Endpoints.Select(row => (row.Data.Item1, row.Data.Item2)),
            .. ServiceEndpointData.Select(row => (row.Data.Item1, row.Data.Item2)),
        ];

        string[] missing =
        [
            .. registered
                .Where(route => !listed.Any(item =>
                    (route.Method is "*" || string.Equals(item.Method, route.Method, StringComparison.OrdinalIgnoreCase))
                    && RegisteredRoutes.Matches(route.Template, item.Url)))
                .Select(route => $"{route.Method} {route.Template}")
                .Order(StringComparer.Ordinal),
        ];

        Assert.True(missing.Length is 0, $"以下路由未纳入冒烟清单：\n{string.Join('\n', missing)}");

        string[] stale =
        [
            .. listed
                .Where(item => !registered.Any(route =>
                    (route.Method is "*" || string.Equals(item.Method, route.Method, StringComparison.OrdinalIgnoreCase))
                    && RegisteredRoutes.Matches(route.Template, item.Url)))
                .Select(item => $"{item.Method} {item.Url}")
                .Order(StringComparer.Ordinal),
        ];

        Assert.True(stale.Length is 0, $"冒烟清单里的地址已不对应任何注册路由：\n{string.Join('\n', stale)}");
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_DoesNotThrow(string method, string url, string? json)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), url);
        request.WithDevice();
        request.WithClientVersion();

        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(
            response.StatusCode is not HttpStatusCode.InternalServerError,
            $"{method} {url} returned 500: {body}");

        if (RawEndpointPrefixes.Any(prefix => url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        TestResponse parsed = TestResponse.Parse(body);
        Assert.True(parsed.Retcode >= 0, $"{method} {url} returned an unexpected retcode {parsed.Retcode}");
    }

    /// <summary>
    /// 维护接口单独用共享清单再跑一遍，保证 <c>/Service/*</c> 的每一行都被覆盖到。
    /// 语义断言（鉴权分支、业务分支）见 <see cref="ServiceTests"/>。
    /// </summary>
    [Theory]
    [MemberData(nameof(ServiceEndpointData))]
    public async Task ServiceEndpoint_DoesNotThrow(string method, string url, string? json)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), url);
        request.WithDevice();
        request.WithClientVersion();

        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(
            response.StatusCode is not HttpStatusCode.InternalServerError,
            $"{method} {url} returned 500: {body}");

        TestResponse parsed = TestResponse.Parse(body);
        Assert.Equal(ReturnCodes.LoginFail, parsed.Retcode);
    }

    public static TheoryData<string, string, string?> ServiceEndpointData => Infrastructure.ServiceEndpoints.TheoryData;
}
