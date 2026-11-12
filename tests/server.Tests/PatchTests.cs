using server.Services.Admin;
using server.Tests.Infrastructure;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class PatchTests(HutaoServerFactory factory)
{
    private readonly HttpClient client = factory.CreateApiClient();

    /// <summary>
    /// 功能熔断链路：后台开启 Patch.DisableFpsUnlock / Patch.DisableIslandInjection 后，
    /// /patch/hutao 下发 true；关闭时字段省略（客户端视为 false，恢复用户本地设置）.
    /// </summary>
    [Fact]
    public async Task PatchKillSwitches_AreServedOnPatchEndpoint()
    {
        SystemConfigService config = factory.GetService<SystemConfigService>();
        try
        {
            await config.UpdateAsync(new Dictionary<string, string?>
            {
                [Keys.PatchDisableFpsUnlock] = "true",
                [Keys.PatchDisableIslandInjection] = "true",
                [Keys.PatchDisableFpsUnlockReason] = "游戏版本更新，帧率解锁暂不可用",
                [Keys.PatchDisableIslandInjectionReason] = "Island 注入正在修复",
            }, 0);

            using HttpResponseMessage on = await client.GetAsync("/patch/hutao", TestContext.Current.CancellationToken);
            TestResponse parsedOn = await on.ReadTestResponseAsync();
            Assert.Equal(ReturnCodes.Success, parsedOn.Retcode);
            Assert.True(parsedOn.Data.GetProperty("fpsUnlockDisabled").GetBoolean());
            Assert.True(parsedOn.Data.GetProperty("islandInjectionDisabled").GetBoolean());
            Assert.Equal("游戏版本更新，帧率解锁暂不可用", parsedOn.Data.GetProperty("fpsUnlockDisabledReason").GetString());
            Assert.Equal("Island 注入正在修复", parsedOn.Data.GetProperty("islandInjectionDisabledReason").GetString());
        }
        finally
        {
            await config.DeleteAsync(Keys.PatchDisableFpsUnlock);
            await config.DeleteAsync(Keys.PatchDisableIslandInjection);
            await config.DeleteAsync(Keys.PatchDisableFpsUnlockReason);
            await config.DeleteAsync(Keys.PatchDisableIslandInjectionReason);
        }

        using HttpResponseMessage off = await client.GetAsync("/patch/hutao", TestContext.Current.CancellationToken);
        TestResponse parsedOff = await off.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, parsedOff.Retcode);
        Assert.False(parsedOff.Data.TryGetProperty("fpsUnlockDisabled", out _), "未熔断时字段应省略");
        Assert.False(parsedOff.Data.TryGetProperty("islandInjectionDisabled", out _), "未熔断时字段应省略");
        Assert.False(parsedOff.Data.TryGetProperty("fpsUnlockDisabledReason", out _), "未熔断时原因应省略");
        Assert.False(parsedOff.Data.TryGetProperty("islandInjectionDisabledReason", out _), "未熔断时原因应省略");
    }
}
