using Microsoft.Extensions.DependencyInjection;
using server.Services.Admin;
using server.Tests.Infrastructure;

namespace server.Tests;

/// <summary>
/// SSO 后台配置的三态语义：system_settings 覆盖值优先，未配置的键回退 App:Oidc
/// （appsettings/环境变量），删除覆盖键恢复跟随配置。管理端保存后下一次读取即生效。
/// </summary>
[Collection(HutaoServerCollection.Name)]
public sealed class SsoSettingsTests
{
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
    public async Task Oidc_FallsBackToAppOptions_WhenNothingOverridden()
    {
        SystemConfigService service = GetService();
        await service.DeleteAsync(SystemConfigService.Keys.OidcAuthority);

        Assert.Equal(
            factory.Services.GetRequiredService<Options.AppOptions>().Oidc.Authority,
            service.Oidc.Authority);
    }

    [Fact]
    public async Task Oidc_DatabaseOverride_WinsAndRevertsOnDelete()
    {
        SystemConfigService service = GetService();
        string fallback = service.Oidc.Authority;

        (bool saved, _) = await service.UpdateAsync(new Dictionary<string, string?>
        {
            [SystemConfigService.Keys.OidcAuthority] = "https://override.test",
        }, 0);
        Assert.True(saved);

        Assert.Equal("https://override.test", service.Oidc.Authority);
        // 未覆盖的键仍然回退配置文件
        Assert.Equal(factory.Services.GetRequiredService<Options.AppOptions>().Oidc.Scope, service.Oidc.Scope);

        (bool deleted, _) = await service.DeleteAsync(SystemConfigService.Keys.OidcAuthority);
        Assert.True(deleted);
        Assert.Equal(fallback, service.Oidc.Authority);
    }

    [Fact]
    public async Task Oidc_EnabledSwitch_OverridesTheConfigValue()
    {
        SystemConfigService service = GetService();
        bool configEnabled = factory.Services.GetRequiredService<Options.AppOptions>().Oidc.Enabled;

        try
        {
            await service.UpdateAsync(new Dictionary<string, string?>
            {
                [SystemConfigService.Keys.OidcEnabled] = (!configEnabled).ToString(),
            }, 0);
            Assert.Equal(!configEnabled, service.Oidc.Enabled);
        }
        finally
        {
            await service.DeleteAsync(SystemConfigService.Keys.OidcEnabled);
        }

        Assert.Equal(configEnabled, service.Oidc.Enabled);
    }
}
