using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using server.Extension;
using server.Models.Admin;
using server.Services.PublicAPI;
using server.Tests.Infrastructure;
using System.Net;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class AuditLogTests(HutaoServerFactory factory)
{
    private readonly HttpClient client = factory.CreateApiClient();
    private readonly TestUsers users = factory.Users;

    [Fact]
    public async Task AuditLog_RecordsPostOperationsWithRedactedDetails()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string marker = $"audit-{Guid.NewGuid():N}";

        // 用带 Description 的写操作产生一条审计记录
        TestResponse generated = await client.SendEnvelopeAsync("POST", "/api/admin/redeem-codes/generation", JsonSerializer.Serialize(new
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

        TestResponse audit = await client.SendEnvelopeAsync("GET", "/api/admin/audit-logs?page=1&pageSize=100", null, maintainer.AccessToken);
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

        await client.SendEnvelopeAsync("POST", "/api/admin/redeem-codes/generation", JsonSerializer.Serialize(new
        {
            Count = 1,
            Type = 1,
            ServiceType = 1,
            Value = 1,
            Description = marker,
            ExpireTime = DateTimeOffset.UtcNow.AddDays(1),
            Times = 1,
        }), maintainer.AccessToken);

        TestResponse audit = await client.SendEnvelopeAsync("GET", "/api/admin/audit-logs?page=1&pageSize=100", null, maintainer.AccessToken);
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
        await client.SendEnvelopeAsync("POST", "/api/admin/redeem-codes/generation", JsonSerializer.Serialize(new
        {
            Count = 1,
            Type = 1,
            ServiceType = 1,
            Value = 1,
            Description = marker,
            ExpireTime = DateTimeOffset.UtcNow.AddDays(1),
            Times = 1,
        }), maintainer.AccessToken, realIp: "223.5.5.5");

        TestResponse audit = await client.SendEnvelopeAsync("GET", "/api/admin/audit-logs?page=1&pageSize=100", null, maintainer.AccessToken);
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

        await client.SendEnvelopeAsync("PUT", $"/api/admin/users/{int.MaxValue}/password", JsonSerializer.Serialize(new UserResetPasswordRequest
        {
            UserId = int.MaxValue,
            NewPassword = secret,
        }), maintainer.AccessToken);

        TestResponse audit = await client.SendEnvelopeAsync("GET", "/api/admin/audit-logs?page=1&pageSize=100", null, maintainer.AccessToken);
        PagedResult<AuditLogItem>? page = audit.DeserializeData<PagedResult<AuditLogItem>>();
        Assert.NotNull(page);

        Assert.DoesNotContain(page!.Items, item => item.Detail?.Contains(secret, StringComparison.Ordinal) is true);
        Assert.Contains(page.Items, item => item.Detail?.Contains("\"NewPassword\":\"***\"", StringComparison.Ordinal) is true);
    }
}
