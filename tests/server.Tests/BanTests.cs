using server.Models.Admin;
using server.Tests.Infrastructure;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class BanTests(HutaoServerFactory factory)
{
    private readonly HttpClient client = factory.CreateApiClient();
    private readonly TestUsers users = factory.Users;

    [Fact]
    public async Task BanAdd_ThenRemove_RoundTripsThroughTheBanList()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string uid = "900000101";

        TestResponse added = await client.SendEnvelopeAsync("POST", $"/api/admin/bans?uid={uid}&reason=integration", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, added.Retcode);

        // 重复封禁同一个 Uid 不报错，只是不再新增
        TestResponse again = await client.SendEnvelopeAsync("POST", $"/api/admin/bans?uid={uid}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, again.Retcode);

        TestResponse list = await client.SendEnvelopeAsync("GET", $"/api/admin/bans?page=1&pageSize=100&keyword={uid}", null, maintainer.AccessToken);
        PagedResult<BanItem>? page = list.DeserializeData<PagedResult<BanItem>>();
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.Uid == uid && item.Reason == "integration");

        TestResponse removed = await client.SendEnvelopeAsync("DELETE", $"/api/admin/bans/{uid}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, removed.Retcode);

        TestResponse afterRemove = await client.SendEnvelopeAsync("GET", $"/api/admin/bans?page=1&pageSize=100&keyword={uid}", null, maintainer.AccessToken);
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

        TestResponse parsed = await client.SendEnvelopeAsync("POST", $"/api/admin/bans?uid={Uri.EscapeDataString(uid)}", null, maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    [Fact]
    public async Task BanBatchAdd_ReturnsTheNumberOfNewlyBannedUids()
    {
        TestUser maintainer = await users.MaintainerAsync();
        List<string> uids = ["900000201", "900000202", "900000203"];

        string body = JsonSerializer.Serialize(new BatchUidRequest { Uids = uids, Reason = "batch" });
        TestResponse parsed = await client.SendEnvelopeAsync("POST", "/api/admin/bans/batch", body, maintainer.AccessToken);

        Assert.Equal(ReturnCodes.Success, parsed.Retcode);
        Assert.Equal(uids.Count, parsed.Data.GetInt32());

        // 再批量封禁同样的 Uid 时新增数量为 0
        TestResponse again = await client.SendEnvelopeAsync("POST", "/api/admin/bans/batch", body, maintainer.AccessToken);
        Assert.Equal(0, again.Data.GetInt32());
    }
}
