using server.Models.Admin;
using server.Tests.Infrastructure;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class SystemOverviewTests(HutaoServerFactory factory)
{
    private readonly HttpClient client = factory.CreateApiClient();
    private readonly TestUsers users = factory.Users;

    [Fact]
    public async Task SystemOverview_ReturnsLiveProbeResults()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await client.SendEnvelopeAsync("GET", "/api/admin/system/overview", null, maintainer.AccessToken);
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
}
