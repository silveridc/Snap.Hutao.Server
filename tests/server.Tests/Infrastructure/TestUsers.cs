using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using server.Models.Entity.Passport;
using server.Models.Passport;
using server.Services.Authorization;

namespace server.Tests.Infrastructure;

/// <summary>测试用账号，覆盖各鉴权分支。</summary>
public sealed record TestUser(int Id, string UserName, string Password, string AccessToken, string RefreshToken);

/// <summary>
/// 通过 <see cref="UserManager{TUser}"/> 直接播种用户，并用被测项目的
/// <see cref="PassportService"/> 签发真实 JWT，保证与线上鉴权路径一致。
/// </summary>
public sealed class TestUsers(HutaoServerFactory factory)
{
    public const string Password = "TestPassword123";

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, TestUser> cache = [];

    /// <summary>维护者，可访问 /Service/*。</summary>
    public Task<TestUser> MaintainerAsync() => GetOrCreateAsync("maintainer", user =>
    {
        user.IsMaintainer = true;
        user.IsLicensedDeveloper = true;
    });

    /// <summary>仅拥有开发者授权，可绕过各服务的时间限制。</summary>
    public Task<TestUser> DeveloperAsync() => GetOrCreateAsync("developer", user => user.IsLicensedDeveloper = true);

    /// <summary>祈愿记录服务在期。</summary>
    public Task<TestUser> GachaLogAsync() => GetOrCreateAsync("gachalog", user => user.GachaLogExpireAt = InDays(30));

    /// <summary>CDN 服务在期。</summary>
    public Task<TestUser> CdnAsync() => GetOrCreateAsync("cdn", user => user.CdnExpireAt = InDays(30));

    /// <summary>普通用户，未订阅任何服务。</summary>
    public Task<TestUser> PlainAsync() => GetOrCreateAsync("plain", _ => { });

    /// <summary>创建一个全新用户，避免与其他测试共享状态。</summary>
    public async Task<TestUser> CreateUniqueAsync(string prefix, Action<HutaoUser>? configure = null)
    {
        string userName = $"{prefix}-{Guid.NewGuid():N}@example.com";

        using IServiceScope scope = factory.Services.CreateScope();
        UserManager<HutaoUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<HutaoUser>>();

        HutaoUser user = new() { UserName = userName, Email = userName };
        configure?.Invoke(user);

        IdentityResult result = await userManager.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));

        PassportService passportService = scope.ServiceProvider.GetRequiredService<PassportService>();
        TokenResponse token = await passportService.CreateTokenResponseAsync(user.Id, new DeviceInfo($"device-{Guid.NewGuid():N}", "TestDevice", "Windows"));

        return new(user.Id, userName, Password, token.AccessToken, token.RefreshToken);
    }

    /// <summary>为已有用户签发一对新的令牌（可指定设备）。</summary>
    public async Task<TokenResponse> IssueTokenAsync(int userId, DeviceInfo? deviceInfo = null)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        PassportService passportService = scope.ServiceProvider.GetRequiredService<PassportService>();
        return await passportService.CreateTokenResponseAsync(userId, deviceInfo ?? new DeviceInfo($"device-{Guid.NewGuid():N}", "TestDevice", "Windows"));
    }

    private async Task<TestUser> GetOrCreateAsync(string key, Action<HutaoUser> configure)
    {
        await gate.WaitAsync();
        try
        {
            if (cache.TryGetValue(key, out TestUser? existing))
            {
                return existing;
            }

            TestUser created = await CreateUniqueAsync(key, configure);
            cache[key] = created;
            return created;
        }
        finally
        {
            gate.Release();
        }
    }

    private static long InDays(int days)
    {
        return DateTimeOffset.UtcNow.AddDays(days).ToUnixTimeSeconds();
    }
}
