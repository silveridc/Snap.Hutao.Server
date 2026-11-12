using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using server.Models.Admin;
using server.Options;
using server.Tests.Infrastructure;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class UserPrivilegeTests(HutaoServerFactory factory)
{
    private readonly HttpClient client = factory.CreateApiClient();
    private readonly TestUsers users = factory.Users;

    [Fact]
    public async Task UserPrivilege_GrantsAndRevokesDeveloperLicense()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser target = await users.CreateUniqueAsync("service-privilege");

        TestResponse granted = await client.SendEnvelopeAsync("PATCH", $"/api/admin/users/{target.Id}/privileges", JsonSerializer.Serialize(new UserPrivilegeRequest
        {
            UserId = target.Id,
            IsLicensedDeveloper = true,
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, granted.Retcode);

        TestResponse detail = await client.SendEnvelopeAsync("GET", $"/api/admin/users/{target.Id}", null, maintainer.AccessToken);
        Assert.True(detail.DeserializeData<UserDetail>()!.IsLicensedDeveloper);

        TestResponse revoked = await client.SendEnvelopeAsync("PATCH", $"/api/admin/users/{target.Id}/privileges", JsonSerializer.Serialize(new UserPrivilegeRequest
        {
            UserId = target.Id,
            IsLicensedDeveloper = false,
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, revoked.Retcode);

        TestResponse afterRevoke = await client.SendEnvelopeAsync("GET", $"/api/admin/users/{target.Id}", null, maintainer.AccessToken);
        Assert.False(afterRevoke.DeserializeData<UserDetail>()!.IsLicensedDeveloper);
    }

    [Fact]
    public async Task UserPrivilege_RevokingOwnMaintainerFlag_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await client.SendEnvelopeAsync("PATCH", $"/api/admin/users/{maintainer.Id}/privileges", JsonSerializer.Serialize(new UserPrivilegeRequest
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
        return client.SendEnvelopeAsync("PATCH", $"/api/admin/users/{userId}/privileges", JsonSerializer.Serialize(new UserPrivilegeRequest
        {
            UserId = userId,
            IsMaintainer = isMaintainer,
            IsLicensedDeveloper = isLicensedDeveloper,
        }), caller.AccessToken);
    }

    private async Task<UserDetail> DetailAsync(TestUser caller, int userId)
    {
        TestResponse parsed = await client.SendEnvelopeAsync("GET", $"/api/admin/users/{userId}", null, caller.AccessToken);
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

        TestResponse parsed = await client.SendEnvelopeAsync("PATCH", $"/api/admin/users/{int.MaxValue}/privileges", JsonSerializer.Serialize(new UserPrivilegeRequest
        {
            UserId = int.MaxValue,
            IsMaintainer = true,
        }), maintainer.AccessToken);

        Assert.Equal(ReturnCodes.UserNameNotExists, parsed.Retcode);
    }
}
