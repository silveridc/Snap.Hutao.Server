using System.Net;
using Microsoft.EntityFrameworkCore;
using server.Models.Passport;
using server.Tests.Infrastructure;

namespace server.Tests;

/// <summary>
/// <c>POST /Webhook</c> 爱发电订单回调：这是无鉴权的第三方入口，
/// 用例覆盖服务时长发放、订单号去重、未配置 sku、用户不存在时改发兑换码等分支。
/// </summary>
[Collection(HutaoServerCollection.Name)]
public sealed class WebhookTests
{
    private const string GachaLogSku = "AFDIAN-TEST-GACHALOG-SKU";
    private const string CdnSku = "AFDIAN-TEST-CDN-SKU";

    private readonly HutaoServerFactory factory;
    private readonly HttpClient client;
    private readonly TestUsers users;

    public WebhookTests(HutaoServerFactory factory)
    {
        this.factory = factory;
        client = factory.CreateApiClient();
        users = factory.Users;
    }

    /// <summary>把两个捐赠 sku 配置成用例使用的固定值（配置是进程级单例，重复设置无副作用）。</summary>
    private async Task ConfigureSkusAsync()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string body = JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["Afdian.SkuGachaLogUploadService"] = GachaLogSku,
            ["Afdian2.SkuCdnService"] = CdnSku,
        });

        using HttpRequestMessage request = new(HttpMethod.Post, "/Service/Config/Update")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.WithToken(maintainer.AccessToken);
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        TestResponse parsed = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);
    }

    /// <summary>让爱发电的订单查询桩按订单号回显这笔订单，使回调的安全校验通过。</summary>
    private void StubOrderValidation(string tradeNo, string skuId, int count)
    {
        factory.StubHttp.MapJson("afdian.com/api/open/query-order",
            TestData.AfdianQueryOrderResponseJson(tradeNo, skuId, count),
            bodyFragment: tradeNo);
    }

    private async Task<HttpResponseMessage> SendOrderAsync(string tradeNo, string skuId, int count, string remark)
    {
        return await client.PostAsync("/Webhook",
            new StringContent(TestData.AfdianOrderJson(tradeNo, skuId, count, remark), Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);
    }

    private int AfdianQueryCount()
    {
        return factory.StubHttp.Requests.Count(request => request.Url.Contains("afdian.com/api/open/query-order", StringComparison.Ordinal));
    }

    private static async Task AssertAcceptedAsync(HttpResponseMessage response)
    {
        // 爱发电只看 ec/em，这里不使用统一响应外壳
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode is HttpStatusCode.OK, $"回调返回 {(int)response.StatusCode}：{body}");

        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal(200, document.RootElement.GetProperty("ec").GetInt32());
    }

    private async Task<UserInfo> UserInfoAsync(TestUser user)
    {
        using HttpResponseMessage response = await client.GetWithTokenAsync("/Passport/v2/UserInfo", user.AccessToken);
        TestResponse parsed = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);
        return parsed.DeserializeData<UserInfo>()!;
    }

    [Fact]
    public async Task GachaLogOrder_ForAnActiveSubscription_ExtendsFromTheCurrentExpireAt()
    {
        await ConfigureSkusAsync();
        TestUser buyer = await users.CreateUniqueAsync("afdian-buyer",
            user => user.GachaLogExpireAt = DateTimeOffset.UtcNow.AddDays(10).ToUnixTimeSeconds());
        string tradeNo = $"GACHALOG-{Guid.NewGuid():N}";
        StubOrderValidation(tradeNo, GachaLogSku, 2);

        DateTimeOffset before = (await UserInfoAsync(buyer)).GachaLogExpireAt;
        await AssertAcceptedAsync(await SendOrderAsync(tradeNo, GachaLogSku, 2, buyer.UserName));

        DateTimeOffset after = (await UserInfoAsync(buyer)).GachaLogExpireAt;

        // 购买 2 个月按每月 30 天发放，且在有效期上累加
        AssertExpiringAround(after, before.AddDays(60), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task GachaLogOrder_ForAnExpiredAccount_RestartsFromNow()
    {
        await ConfigureSkusAsync();
        TestUser buyer = await users.CreateUniqueAsync("afdian-renew");
        string tradeNo = $"GACHALOG-{Guid.NewGuid():N}";
        StubOrderValidation(tradeNo, GachaLogSku, 1);

        // 新用户没有任何有效期，直接按当前时刻起算一个月
        DateTimeOffset before = (await UserInfoAsync(buyer)).GachaLogExpireAt;
        Assert.True(before <= DateTimeOffset.UtcNow, $"新账号不应有祈愿服务有效期：{before:o}");
        await AssertAcceptedAsync(await SendOrderAsync(tradeNo, GachaLogSku, 1, buyer.UserName));

        AssertExpiringAround((await UserInfoAsync(buyer)).GachaLogExpireAt, DateTimeOffset.UtcNow.AddDays(30), TimeSpan.FromMinutes(5));
    }

    /// <summary>断言到期时间落在期望值附近（服务端以秒存储，另有请求耗时）。</summary>
    private static void AssertExpiringAround(DateTimeOffset actual, DateTimeOffset expected, TimeSpan tolerance)
    {
        Assert.True((actual - expected).Duration() <= tolerance,
            $"到期时间 {actual:o}，期望约 {expected:o}（容差 {tolerance}）");
    }

    [Fact]
    public async Task SameOrderNumber_Twice_ExtendsOnlyOnce()
    {
        await ConfigureSkusAsync();
        TestUser buyer = await users.CreateUniqueAsync("afdian-duplicate");
        string tradeNo = $"GACHALOG-{Guid.NewGuid():N}";
        StubOrderValidation(tradeNo, GachaLogSku, 1);

        await AssertAcceptedAsync(await SendOrderAsync(tradeNo, GachaLogSku, 1, buyer.UserName));
        DateTimeOffset first = (await UserInfoAsync(buyer)).GachaLogExpireAt;

        // 爱发电会重复推送同一笔订单，回调按订单号去重
        await AssertAcceptedAsync(await SendOrderAsync(tradeNo, GachaLogSku, 1, buyer.UserName));
        Assert.Equal(first, (await UserInfoAsync(buyer)).GachaLogExpireAt);
    }

    [Fact]
    public async Task UnconfiguredSku_IsIgnoredWithoutQueryingAfdian()
    {
        await ConfigureSkusAsync();
        TestUser buyer = await users.CreateUniqueAsync("afdian-unknownsku");
        string tradeNo = $"GACHALOG-{Guid.NewGuid():N}";
        DateTimeOffset before = (await UserInfoAsync(buyer)).GachaLogExpireAt;
        int validations = AfdianQueryCount();

        await AssertAcceptedAsync(await SendOrderAsync(tradeNo, "SKU-NOT-CONFIGURED", 1, buyer.UserName));

        Assert.Equal(before, (await UserInfoAsync(buyer)).GachaLogExpireAt);

        // 未配置的 sku 直接判定为不支持，不会去向爱发电回查订单
        Assert.Equal(validations, AfdianQueryCount());
    }

    [Fact]
    public async Task OrderWithoutSkuDetail_IsIgnored()
    {
        await ConfigureSkusAsync();
        TestUser buyer = await users.CreateUniqueAsync("afdian-nosku");
        string tradeNo = $"GACHALOG-{Guid.NewGuid():N}";
        DateTimeOffset before = (await UserInfoAsync(buyer)).GachaLogExpireAt;

        string body = JsonSerializer.Serialize(new
        {
            type = "order",
            data = new { order = new { out_trade_no = tradeNo, remark = buyer.UserName, sku_detail = Array.Empty<object>() } },
        });
        using HttpResponseMessage response = await client.PostAsync("/Webhook",
            new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        await AssertAcceptedAsync(response);
        Assert.Equal(before, (await UserInfoAsync(buyer)).GachaLogExpireAt);
    }

    [Fact]
    public async Task CdnOrder_ExtendsCdnService()
    {
        await ConfigureSkusAsync();
        TestUser buyer = await users.CreateUniqueAsync("afdian-cdn");
        string tradeNo = $"CDN-{Guid.NewGuid():N}";
        StubOrderValidation(tradeNo, CdnSku, 1);

        DateTimeOffset before = (await UserInfoAsync(buyer)).CdnExpireAt;
        await AssertAcceptedAsync(await SendOrderAsync(tradeNo, CdnSku, 1, buyer.UserName));

        DateTimeOffset after = (await UserInfoAsync(buyer)).CdnExpireAt;
        Assert.True(after > before, $"CDN 服务到期时间未延长：{before} -> {after}");
    }

    [Fact]
    public async Task OrderFromUnknownUser_GeneratesRedeemCodeAndMailsIt()
    {
        await ConfigureSkusAsync();
        string missingUser = $"afdian-missing-{Guid.NewGuid():N}@example.com";
        string tradeNo = $"GACHALOG-{Guid.NewGuid():N}";
        StubOrderValidation(tradeNo, GachaLogSku, 1);

        factory.Smtp.Clear();
        await AssertAcceptedAsync(await SendOrderAsync(tradeNo, GachaLogSku, 1, missingUser));

        Assert.Contains(factory.Smtp.Messages, message => message.Contains(missingUser, StringComparison.OrdinalIgnoreCase));

        string? code = await factory.WithDbContextAsync(context =>
            context.RedeemCodes.AsNoTracking()
                .Where(c => c.Description == "For unregistered user" && c.Creator == "AfdianWebhookService")
                .OrderByDescending(c => c.Id)
                .Select(c => c.Code)
                .FirstOrDefaultAsync(TestContext.Current.CancellationToken));

        Assert.False(string.IsNullOrEmpty(code));
    }

    [Fact]
    public async Task ForgedOrderNumber_IsRejectedWhenAfdianDoesNotKnowIt()
    {
        await ConfigureSkusAsync();
        TestUser buyer = await users.CreateUniqueAsync("afdian-forged");
        string tradeNo = $"FORGED-{Guid.NewGuid():N}";

        // 默认桩返回空订单列表，相当于爱发电查不到这笔订单
        DateTimeOffset before = (await UserInfoAsync(buyer)).GachaLogExpireAt;
        await AssertAcceptedAsync(await SendOrderAsync(tradeNo, GachaLogSku, 12, buyer.UserName));

        Assert.Equal(before, (await UserInfoAsync(buyer)).GachaLogExpireAt);
    }

    [Fact]
    public async Task OrderWithMismatchedCount_IsRejected()
    {
        await ConfigureSkusAsync();
        TestUser buyer = await users.CreateUniqueAsync("afdian-mismatch");
        string tradeNo = $"MISMATCH-{Guid.NewGuid():N}";
        StubOrderValidation(tradeNo, GachaLogSku, 1);

        DateTimeOffset before = (await UserInfoAsync(buyer)).GachaLogExpireAt;

        // 回调声称买了 5 个月，但爱发电的订单记录只有 1 个月
        await AssertAcceptedAsync(await SendOrderAsync(tradeNo, GachaLogSku, 5, buyer.UserName));

        Assert.Equal(before, (await UserInfoAsync(buyer)).GachaLogExpireAt);
    }
}
