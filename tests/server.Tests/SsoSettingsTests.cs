using server.Services.Admin;
using server.Tests.Infrastructure;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class SsoSettingsTests
{
    private static readonly string[] OidcKeys =
    [
        Keys.OidcEnabled, Keys.OidcAuthority, Keys.OidcClientId, Keys.OidcClientSecret,
        Keys.OidcRedirectUri, Keys.OidcClientRedirectUri, Keys.OidcScope, Keys.OidcDisplayName,
        Keys.OidcBootstrapClaim, Keys.OidcBootstrapValue, Keys.OidcRequireHttpsMetadata,
    ];

    private readonly HutaoServerFactory factory;

    public SsoSettingsTests(HutaoServerFactory factory)
    {
        this.factory = factory;
    }

    private SystemConfigService GetService()
    {
        return factory.GetService<SystemConfigService>();
    }

    [Fact]
    public async Task Oidc_UsesBuiltInDefaults_WhenNothingOverridden()
    {
        SystemConfigService service = GetService();
        foreach (string key in OidcKeys)
        {
            await service.DeleteAsync(key);
        }

        Assert.False(service.Oidc.Enabled);
        Assert.Equal(string.Empty, service.Oidc.Authority);
        Assert.Equal("openid profile email", service.Oidc.Scope);
        Assert.Equal("sub", service.Oidc.BootstrapClaim);
        Assert.True(service.Oidc.RequireHttpsMetadata);
    }

    [Fact]
    public async Task Oidc_DatabaseOverride_WinsAndRevertsOnDelete()
    {
        SystemConfigService service = GetService();
        await service.DeleteAsync(Keys.OidcAuthority);

        (bool saved, _) = await service.UpdateAsync(new Dictionary<string, string?>
        {
            [Keys.OidcAuthority] = "https://override.test",
        }, 0);
        Assert.True(saved);

        Assert.Equal("https://override.test", service.Oidc.Authority);
        // 未覆盖的键仍然使用内置默认值
        Assert.Equal("openid profile email", service.Oidc.Scope);

        (bool deleted, _) = await service.DeleteAsync(Keys.OidcAuthority);
        Assert.True(deleted);
        Assert.Equal(string.Empty, service.Oidc.Authority);
    }

    [Fact]
    public async Task Oidc_EnabledSwitch_TogglesTheTypedGetter()
    {
        SystemConfigService service = GetService();
        await service.DeleteAsync(Keys.OidcEnabled);

        try
        {
            await service.UpdateAsync(new Dictionary<string, string?>
            {
                [Keys.OidcEnabled] = "true",
            }, 0);
            Assert.True(service.Oidc.Enabled);
        }
        finally
        {
            await service.DeleteAsync(Keys.OidcEnabled);
        }

        Assert.False(service.Oidc.Enabled);
    }
}
