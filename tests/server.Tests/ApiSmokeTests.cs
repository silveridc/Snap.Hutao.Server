using server.Tests.Infrastructure;
using System.Net;

namespace server.Tests;

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

    /// <summary>不返回统一响应外壳的接口前缀（第三方透传、302 重定向、回调协议自有格式）.</summary>
    private static readonly string[] RawEndpointPrefixes =
    [
        "/api/Enka",
        "/api/oauth/authorizations",
        "/api/OAuth/Callback",
        "/api/Webhook",
        "/api/client",
        "/api/passport/v2/captcha",
        "/Admin/Sso/Callback",
        "/Passport/Sso/Callback",
    ];

    /// <summary>每次运行都使用不同账号，避免与其它用例的验证码缓存互相干扰.</summary>
    private static string UniqueUser(string tag) => $"smoke-{tag}-{Guid.NewGuid():N}@example.com";

    /// <summary>
    /// 构造 PassportRequest 请求体.UserName / Password / VerifyCode 等字段
    /// 在客户端均以 RSA-OAEP-SHA1 加密后提交，这里按同样规则构造.
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

        // 启动器功能配置（裸 JSON，未同步过的版本返回 404）
        { "GET", "/api/client/features/AchievementFieldId_0.0.0", null },
        { "GET", "/api/client/features/UnknownFeature_1.0.0", null },

        // 公告
        { "GET", "/api/announcements", "[]" },
        { "GET", "/api/announcements", "[1,2,3]" },

        // 深渊统计
        { "GET", "/api/statistics/overview", null },
        { "GET", "/api/statistics/overview?Last=true", null },
        { "GET", "/api/statistics/avatars/attendance-rate", null },
        { "GET", "/api/statistics/avatars/attendance-rate?Last=true", null },
        { "GET", "/api/statistics/avatars/utilization-rate", null },
        { "GET", "/api/statistics/avatars/holding-rate", null },
        { "GET", "/api/statistics/avatars/collocations", null },
        { "GET", "/api/statistics/weapons/collocations", null },
        { "GET", "/api/statistics/teams/combinations", null },

        // 深渊记录
        { "GET", "/api/records/abyss/100000001/status", null },
        { "GET", "/api/records/abyss/100000001/rank", null },
        { "POST", "/api/records/abyss", TestData.SimpleRecordJson("100000001") },
        { "POST", "/api/records/abyss?returningRank=true", TestData.SimpleRecordJson("100000002") },

        // 幻想真境剧诗
        { "GET", "/api/records/role-combat/statistics", null },
        { "POST", "/api/records/role-combat", TestData.RoleCombatRecordJson("100000003") },

        // 祈愿记录（统计接口公开，其余需要登录）
        { "GET", "/api/gacha-logs/statistics/current-event", null },
        { "GET", "/api/gacha-logs/statistics/distributions/avatar-event", null },
        { "GET", "/api/gacha-logs/statistics/distributions/weapon-event", null },
        { "GET", "/api/gacha-logs/statistics/distributions/chronicled", null },
        { "GET", "/api/gacha-logs/statistics/distributions/standard", null },
        { "GET", "/api/gacha-logs/entries", null },
        { "GET", "/api/gacha-logs/end-ids?Uid=100000001", null },
        { "POST", "/api/gacha-logs/retrievals", """{"Uid":"100000001","EndIds":{}}""" },
        { "GET", "/api/gacha-logs/limited-retrievals?uid=100000001&configType=301", null },
        { "POST", "/api/gacha-logs", TestData.GachaItemsJson("100000001") },
        { "DELETE", "/api/gacha-logs/100000001", null },

        // 账户 v2
        { "GET", "/api/passport/v2/captcha", null },
        { "GET", "/api/passport/v2/captcha/page", null },
        { "GET", "/api/passport/v2/me", null },
        { "GET", "/api/passport/v2/me/login-logs", null },
        { "GET", "/api/passport/v2/sessions", null },
        { "POST", "/api/passport/v2/sessions/refresh", """{"RefreshToken":""}""" },
        { "DELETE", "/api/passport/v2/sessions?deviceId=device", null },
        { "DELETE", "/api/passport/v2/sessions", null },
        { "POST", "/api/passport/v2/verification-codes", PassportBody(userName: UniqueUser("v2-verify")) },
        { "POST", "/api/passport/v2/accounts", PassportBody(userName: UniqueUser("v2-register"), password: "TestPassword123", verifyCode: "INVALID0") },
        { "DELETE", "/api/passport/v2/accounts/me", PassportBody(userName: UniqueUser("v2-cancel"), password: "TestPassword123", verifyCode: "INVALID0") },
        { "PUT", "/api/passport/v2/accounts/me/password", PassportBody(userName: UniqueUser("v2-resetpwd"), password: "TestPassword123", verifyCode: "INVALID0") },
        { "PUT", "/api/passport/v2/accounts/me/username", PassportBody(userName: UniqueUser("v2-resetname"), verifyCode: "INVALID0", newUserName: UniqueUser("v2-resetname-new"), newVerifyCode: "INVALID1") },
        { "POST", "/api/passport/v2/sessions", PassportBody(userName: UniqueUser("v2-login"), password: "TestPassword123") },

        // CDN 分发
        { "GET", "/api/distribution/download-token", null },
        { "GET", "/api/distribution/accelerated-mirror?filename=test.zip", null },

        // 兑换码
        { "POST", "/api/redeem-codes/redemptions", """{"code":"000000000000000000"}""" },

        // 开源许可
        { "POST", "/api/license/require", """{"UserName":"a@example.com","ProjectUrl":"https://example.com","Token":"token"}""" },

        // 遥测日志
        { "POST", "/api/telemetry-logs", """{"Id":"device-1","Time":1700000000,"Info":"{\"exception\":\"test\"}"}""" },
        { "GET", "/api/telemetry-logs/device-1", null },

        // OAuth
        { "GET", "/api/oauth/authorizations/Github?device_id=d&device_name=n&device_os=Windows&callback=https://example.com", null },
        { "GET", "/api/oauth/bindings/Github", null },
        { "DELETE", "/api/oauth/bindings/Github", null },

        // 伪造的 state 不是合法密文，回调应拒绝而不是 500（成功分支见 OAuthTests）
        { "GET", "/api/OAuth/Callback/GitHub?code=smoke&state=c21va2U=", null },

        // 爱发电订单回调：未知 sku 不做外部校验，直接判定为不支持（成功分支见 WebhookTests）
        { "POST", "/api/Webhook", TestData.AfdianOrderJson("smoke-unknown-sku", "SKU-NOT-CONFIGURED", 1, "smoke@example.com") },

        // 后台单点登录：SSO 未启用的宿主里 Status 报未启用、Challenge/Exchange/Callback 走拒绝分支，
        // 全链路的成功分支见 SsoTests
        { "GET", "/api/admin/sso/status", null },
        { "GET", "/api/admin/sso/challenge", null },
        { "GET", "/Admin/Sso/Callback?code=smoke&state=smoke", null },
        { "POST", "/api/admin/sso/sessions", """{"Ticket":"smoke"}""" },

        // 启动器单点登录：同样只跑拒绝分支，成功链路见 SsoTests 与 upd-test/sso-e2e
        { "GET", "/api/passport/sso/status", null },
        { "GET", "/api/passport/sso/challenge", null },
        { "GET", "/Passport/Sso/Callback?code=smoke&state=smoke", null },
        { "POST", "/api/passport/sso/sessions", """{"Ticket":"smoke"}""" },

        // 公共 API 与第三方透传
        { "GET", "/api/public/ip", null },
        { "GET", "/api/Enka/100000001", null },
        { "GET", "/api/Enka/100000001/info", null },
        { "GET", "/api/wallpapers/bing", null },
        { "GET", "/api/wallpapers/today", null },
        { "GET", "/api/wallpapers/hoyoplay", null },
    };

    /// <summary>
    /// 覆盖性校验：宿主实际注册的每一条路由都必须出现在冒烟清单或维护接口清单里，
    /// 清单里的每一条地址也必须命中一条实际路由.新增接口忘了补用例、或接口被改名后
    /// 清单里留下打不到人的死地址，都会在这里暴露.
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
    /// 维护接口单独用共享清单再跑一遍，保证 <c>/Service/*</c> 的每一行都被覆盖到.
    /// 语义断言（鉴权分支、业务分支）见 <see cref="ServiceTests"/>.
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