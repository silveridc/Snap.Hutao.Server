using server.Models.Admin;
using server.Tests.Infrastructure;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class RedeemTests(HutaoServerFactory factory)
{
    private readonly HttpClient client = factory.CreateApiClient();
    private readonly TestUsers users = factory.Users;

    [Fact]
    public async Task RedeemGenerate_ProducesAn18CharacterCodeThatCanBeRedeemed()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser target = await users.CreateUniqueAsync("service-redeem");

        TestResponse generated = await client.SendEnvelopeAsync("POST", "/api/admin/redeem-codes/generation", JsonSerializer.Serialize(new
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

        TestResponse generated = await client.SendEnvelopeAsync("POST", "/api/admin/redeem-codes/generation", JsonSerializer.Serialize(new
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
        TestResponse before = await client.SendEnvelopeAsync("GET", $"/api/admin/redeem-codes/{id}/usages", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, before.Retcode);
        Assert.Empty(before.DeserializeData<List<RedeemCodeUsageItem>>()!);

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/api/redeem-codes/redemptions", new { Code = code, Username = target.UserName }, token: null);
        Assert.Equal(ReturnCodes.Success, (await response.ReadTestResponseAsync()).Retcode);

        TestResponse after = await client.SendEnvelopeAsync("GET", $"/api/admin/redeem-codes/{id}/usages", null, maintainer.AccessToken);
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

        TestResponse generated = await client.SendEnvelopeAsync("POST", "/api/admin/redeem-codes/generation", JsonSerializer.Serialize(new
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

        TestResponse updated = await client.SendEnvelopeAsync("PUT", $"/api/admin/redeem-codes/{id}", JsonSerializer.Serialize(new
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

        TestResponse deleted = await client.SendEnvelopeAsync("DELETE", $"/api/admin/redeem-codes/{id}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, deleted.Retcode);

        TestResponse again = await client.SendEnvelopeAsync("DELETE", $"/api/admin/redeem-codes/{id}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.NotFound, again.Retcode);
    }

    private async Task<uint> FindRedeemIdAsync(TestUser maintainer, string code)
    {
        TestResponse list = await client.SendEnvelopeAsync("GET", $"/api/admin/redeem-codes?page=1&pageSize=100&keyword={code}", null, maintainer.AccessToken);
        PagedResult<RedeemCodeItem>? page = list.DeserializeData<PagedResult<RedeemCodeItem>>();

        return page?.Items.FirstOrDefault(item => item.Code == code)?.Id ?? 0;
    }
}
