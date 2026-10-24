using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Models.Context;
using server.Models.Entity.Passport;
using server.Services.Admin;
using server.Tests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace server.Tests;

/// <summary>
/// 后台重置用户密码对「无 SecurityStamp 的历史用户行」的容错及其边界。
/// 正常注册走 UserManager.CreateAsync 会自动盖 stamp，但手工 SQL 插入/迁移来的行可能没有；
/// UserManager 的令牌路径（GeneratePasswordResetTokenAsync → GetSecurityStampAsync）读到 null
/// 会直接抛 InvalidOperationException，接口表现为 500（生产实测 UserId=2 即此症状）。
/// 边界覆盖：null 与空串两种缺失形态、正常用户路径、不存在的用户、密码策略拒绝（不能误伤原密码）、连续重置。
/// </summary>
[Collection(HutaoServerCollection.Name)]
public sealed class UserAdminTests
{
    private const string NewPassword = "NewPassword123";

    private readonly HutaoServerFactory factory;
    private readonly HttpClient client;
    private readonly TestUsers users;

    public UserAdminTests(HutaoServerFactory factory)
    {
        this.factory = factory;
        client = factory.CreateApiClient();
        users = factory.Users;
    }

    /// <summary>把指定用户的 stamp 抹成给定值（null 或空串），模拟生产上手工插入/迁移来的历史行。</summary>
    private static async Task SetStampAsync(HutaoServerFactory factory, int userId, string? stamp)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Users.Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityStamp, stamp));
    }

    private static async Task<HutaoUser> ReloadUserAsync(HutaoServerFactory factory, int userId)
    {
        return (await FindUserAsync(factory, userId))!;
    }

    private static async Task<HutaoUser?> FindUserAsync(HutaoServerFactory factory, int userId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId);
    }

    private static async Task<bool> PasswordWorksAsync(HutaoServerFactory factory, int userId, string password)
    {
        // CheckPasswordAsync 只做哈希比对，不读 stamp，不受被测行为影响。
        // await 必须发生在 using 内：scope 释放后续跑的 EF 操作会撞 ObjectDisposedException。
        using IServiceScope scope = factory.Services.CreateScope();
        UserManager<HutaoUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<HutaoUser>>();
        HutaoUser user = await userManager.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        return await userManager.CheckPasswordAsync(user, password);
    }

    private static async Task<(bool Ok, string? Error, string? Key)> ResetAsync(HutaoServerFactory factory, int userId, string newPassword)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<UserAdminService>()
            .ResetPasswordAsync(userId, newPassword);
    }

    [Fact]
    public async Task ResetPassword_ToleratesLegacyUserRowWithoutSecurityStamp()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser victim = await users.CreateUniqueAsync("nostamp");
        await SetStampAsync(factory, victim.Id, null);

        using HttpRequestMessage request = new(HttpMethod.Post, "/Service/User/ResetPassword");
        request.Headers.Authorization = new("Bearer", maintainer.AccessToken);
        request.Content = JsonContent.Create(new { UserId = victim.Id, NewPassword });

        using HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonNode root = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.True((int)root["retcode"]! == 0, $"重置历史无 stamp 用户的密码不应 500：{root.ToJsonString()}");
    }

    /// <summary>空串与 null 走同一个补盖分支：IsNullOrEmpty 而不是只判 null。</summary>
    [Fact]
    public async Task ResetPassword_ToleratesEmptyStringSecurityStamp()
    {
        TestUser victim = await users.CreateUniqueAsync("emptystamp");
        await SetStampAsync(factory, victim.Id, string.Empty);

        (bool ok, string? error, _) = await ResetAsync(factory, victim.Id, NewPassword);
        Assert.True(ok, $"空串 stamp 应当走补盖分支并成功：{error}");

        HutaoUser repaired = await ReloadUserAsync(factory, victim.Id);
        Assert.False(string.IsNullOrEmpty(repaired.SecurityStamp), "补盖后库里必须有 stamp");
        Assert.True(await PasswordWorksAsync(factory, victim.Id, NewPassword));
    }

    [Fact]
    public async Task ResetPassword_OnNormalUser_ReplacesThePasswordAndKeepsAStamp()
    {
        TestUser victim = await users.CreateUniqueAsync("normalstamp");
        Assert.False(string.IsNullOrEmpty((await ReloadUserAsync(factory, victim.Id)).SecurityStamp));

        (bool ok, string? error, _) = await ResetAsync(factory, victim.Id, NewPassword);
        Assert.True(ok, error);

        Assert.False(await PasswordWorksAsync(factory, victim.Id, TestUsers.Password), "旧密码必须失效");
        Assert.True(await PasswordWorksAsync(factory, victim.Id, NewPassword), "新密码必须可用");
        Assert.False(string.IsNullOrEmpty((await ReloadUserAsync(factory, victim.Id)).SecurityStamp));
    }

    [Fact]
    public async Task ResetPassword_ForMissingUser_ReportsNotFoundInsteadOfThrowing()
    {
        int ghostId = 1_000_000;
        Assert.Null(await FindUserAsync(factory, ghostId));

        (bool ok, _, string? key) = await ResetAsync(factory, ghostId, NewPassword);
        Assert.False(ok);
        Assert.Equal(ServerKeys.ServerAdminUserNotFound, key);
    }

    /// <summary>策略拒绝（最短 8 位）必须以失败返回而不是异常，且不能动到原密码。</summary>
    [Fact]
    public async Task ResetPassword_WithTooShortPassword_FailsWithoutChangingThePassword()
    {
        TestUser victim = await users.CreateUniqueAsync("shortpw");

        (bool ok, string? error, _) = await ResetAsync(factory, victim.Id, "1qaz2ws");
        Assert.False(ok);
        Assert.False(string.IsNullOrEmpty(error), "策略失败要带回原因");
        Assert.Contains("8", error, StringComparison.Ordinal);

        Assert.True(await PasswordWorksAsync(factory, victim.Id, TestUsers.Password), "被拒绝的重置不能误伤原密码");
        Assert.False(await PasswordWorksAsync(factory, victim.Id, "1qaz2ws"));
    }

    /// <summary>第一次重置会把 stamp 轮换掉；第二次必须基于新 stamp 重新发令牌并继续成功。</summary>
    [Fact]
    public async Task ResetPassword_TwiceInARow_BothSucceed()
    {
        TestUser victim = await users.CreateUniqueAsync("twice");
        await SetStampAsync(factory, victim.Id, null);

        (bool first, string? firstError, _) = await ResetAsync(factory, victim.Id, "FirstPass1234");
        Assert.True(first, firstError);
        (bool second, string? secondError, _) = await ResetAsync(factory, victim.Id, "SecondPass4321");
        Assert.True(second, secondError);

        Assert.False(await PasswordWorksAsync(factory, victim.Id, "FirstPass1234"));
        Assert.True(await PasswordWorksAsync(factory, victim.Id, "SecondPass4321"));
    }
}
