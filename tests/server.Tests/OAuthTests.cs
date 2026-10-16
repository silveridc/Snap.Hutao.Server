using System.Net;
using Microsoft.EntityFrameworkCore;
using server.Models.Context;
using server.Models.Entity.Passport;
using server.Models.Passport;
using server.Services.OAuth;
using server.Tests.Infrastructure;

namespace server.Tests;

/// <summary>
/// <c>/OAuth/*</c> 第三方登录与绑定接口。GitHub 侧的令牌交换与用户信息
/// 由 <see cref="StubHttpMessageHandler"/> 提供，state 参数按服务端同样的 AES 规则构造。
/// </summary>
[Collection(HutaoServerCollection.Name)]
public sealed class OAuthTests
{
    private const string CallbackUri = "https://hutao.app/oauth-callback";

    private readonly HutaoServerFactory factory;
    private readonly HttpClient client;
    private readonly TestUsers users;

    public OAuthTests(HutaoServerFactory factory)
    {
        this.factory = factory;
        client = factory.CreateApiClient();
        users = factory.Users;
    }

    /// <summary>
    /// 把一个 GitHub 账号接到指定用户上（走真实的绑定回调），并返回该 GitHub 的 node_id。
    /// </summary>
    private async Task<string> BindGitHubAsync(int userId, string deviceId)
    {
        string nodeId = $"ND_{Guid.NewGuid():N}";
        StubGitHub(nodeId, $"user-{nodeId}");

        string state = TestOAuthState.BindState(userId, deviceId, CallbackUri);
        using HttpResponseMessage response = await client.GetAsync($"/OAuth/Callback/GitHub?code=code-{Guid.NewGuid():N}&state={Uri.EscapeDataString(state)}",
            TestContext.Current.CancellationToken);

        Assert.True(response.StatusCode is HttpStatusCode.Redirect,
            $"绑定回调失败：{(int)response.StatusCode} {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");

        return nodeId;
    }

    /// <summary>登记 GitHub 令牌交换与用户信息的桩数据。</summary>
    private void StubGitHub(string nodeId, string login)
    {
        factory.StubHttp.MapJson(
            "github.com/login/oauth/access_token",
            $$"""
              {"access_token":"ghu_{{nodeId}}","expires_in":28800,"refresh_token":"ghr_{{nodeId}}",
               "refresh_token_expires_in":15811200,"token_type":"bearer","scope":""}
              """);
        factory.StubHttp.MapJson(
            "api.github.com/user",
            $$"""{"id":12345,"node_id":"{{nodeId}}","login":"{{login}}"}""");
    }

    private async Task<TestResponse> StatusAsync(string accessToken)
    {
        using HttpResponseMessage response = await client.GetWithTokenAsync("/OAuth/Status?provider=Github", accessToken);
        return await response.ReadTestResponseAsync();
    }

    [Fact]
    public async Task Auth_Anonymous_RedirectsToGitHubAuthorizeWithEncryptedState()
    {
        using HttpResponseMessage response = await client.GetAsync(
            "/OAuth/Auth?provider=Github&device_id=device-auth&device_name=Test%20Device&device_os=Windows&callback=https://hutao.app/cb",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        string? location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.StartsWith("https://github.com/login/oauth/authorize?client_id=test-client-id&state=", location, StringComparison.Ordinal);

        // state 必须是密文而不是明文 JSON
        Assert.DoesNotContain("DeviceInfo", location, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not-base64-at-all")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAA=")] // 合法 Base64，但不是本密钥的密文
    public async Task Callback_WithUndecryptableState_IsRejected(string state)
    {
        using HttpResponseMessage response = await client.GetAsync($"/OAuth/Callback/GitHub?code=any&state={Uri.EscapeDataString(state)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Callback_Bind_StoresIdentityAndIsVisibleInStatus()
    {
        TestUser user = await users.CreateUniqueAsync("oauth-bind");
        string nodeId = await BindGitHubAsync(user.Id, $"device-bind-{Guid.NewGuid():N}");

        TestResponse status = await StatusAsync(user.AccessToken);
        Assert.Equal(ReturnCodes.Success, status.Retcode);

        OAuthBindStatus? bind = status.DeserializeData<OAuthBindStatus>();
        Assert.NotNull(bind);
        Assert.True(bind!.IsBinded);
        Assert.StartsWith("user-ND_", bind.DisplayName, StringComparison.Ordinal);

        OAuthBindIdentity? identity = await factory.WithDbContextAsync(context =>
            context.OAuthBindIdentities.AsNoTracking().SingleOrDefaultAsync(i => i.ProviderId == nodeId, TestContext.Current.CancellationToken));

        Assert.NotNull(identity);
        Assert.Equal(user.Id, identity!.UserId);
        Assert.Equal(OAuthProviderKind.Github, identity.ProviderKind);
    }

    [Fact]
    public async Task Callback_BindTwiceWithTheSameAccount_KeepsTheOriginalBinding()
    {
        TestUser user = await users.CreateUniqueAsync("oauth-retarget");
        string deviceId = $"device-retarget-{Guid.NewGuid():N}";
        string nodeId = await BindGitHubAsync(user.Id, deviceId);

        // 同一用户再次绑定同一 GitHub 账号：仍然成功，且不会产生第二条绑定记录
        StubGitHub(nodeId, "renamed-login");
        string state = TestOAuthState.BindState(user.Id, deviceId, CallbackUri);
        using HttpResponseMessage second = await client.GetAsync($"/OAuth/Callback/GitHub?code=code-2&state={Uri.EscapeDataString(state)}",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Equal(CallbackUri, second.Headers.Location?.ToString());

        int count = await factory.WithDbContextAsync(context =>
            context.OAuthBindIdentities.AsNoTracking().CountAsync(i => i.ProviderId == nodeId, TestContext.Current.CancellationToken));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Callback_BindAccountThatBelongsToAnotherUser_IsRejected()
    {
        TestUser owner = await users.CreateUniqueAsync("oauth-owner");
        TestUser attacker = await users.CreateUniqueAsync("oauth-attacker");
        string nodeId = await BindGitHubAsync(owner.Id, $"device-owner-{Guid.NewGuid():N}");

        string state = TestOAuthState.BindState(attacker.Id, $"device-attacker-{Guid.NewGuid():N}", CallbackUri);
        using HttpResponseMessage response = await client.GetAsync($"/OAuth/Callback/GitHub?code=code-3&state={Uri.EscapeDataString(state)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("This GitHub account is already linked to another Snap Hutao account", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // 绑定关系没有被动过
        TestResponse status = await StatusAsync(attacker.AccessToken);
        Assert.False(status.DeserializeData<OAuthBindStatus>()!.IsBinded);
    }

    [Fact]
    public async Task Callback_Login_WithBoundAccount_IssuesUsableTokens()
    {
        TestUser user = await users.CreateUniqueAsync("oauth-login");
        string deviceId = $"device-login-{Guid.NewGuid():N}";
        string nodeId = await BindGitHubAsync(user.Id, deviceId);

        StubGitHub(nodeId, "login-account");
        string state = TestOAuthState.LoginState(deviceId, CallbackUri);
        using HttpResponseMessage response = await client.GetAsync($"/OAuth/Callback/GitHub?code=code-4&state={Uri.EscapeDataString(state)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        string? location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.StartsWith($"{CallbackUri}?access_token=", location, StringComparison.Ordinal);

        string accessToken = QueryValue(location, "access_token");
        using HttpResponseMessage userInfo = await client.GetWithTokenAsync("/Passport/v2/UserInfo", accessToken);
        TestResponse parsed = await userInfo.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);
        Assert.Equal(user.UserName, parsed.Data.GetProperty("UserName").GetString());
    }

    [Fact]
    public async Task Callback_Login_WithUnboundGitHubAccount_IsRejected()
    {
        StubGitHub($"ND_{Guid.NewGuid():N}", "stranger");

        string state = TestOAuthState.LoginState($"device-stranger-{Guid.NewGuid():N}", CallbackUri);
        using HttpResponseMessage response = await client.GetAsync($"/OAuth/Callback/GitHub?code=code-5&state={Uri.EscapeDataString(state)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("This GitHub account is not linked to a Snap Hutao account", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Callback_WhenTokenExchangeFails_IsRejected()
    {
        TestUser user = await users.CreateUniqueAsync("oauth-tokenfail");

        // 桩数据未登记成功分支时返回 400，这里显式模拟 GitHub 拒绝交换令牌
        factory.StubHttp.MapJson("github.com/login/oauth/access_token", """{"error":"invalid_grant"}""", HttpStatusCode.BadRequest);

        string state = TestOAuthState.BindState(user.Id, $"device-fail-{Guid.NewGuid():N}", CallbackUri);
        using HttpResponseMessage response = await client.GetAsync($"/OAuth/Callback/GitHub?code=bad-code&state={Uri.EscapeDataString(state)}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Failed to obtain an access token", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Unbind_ThenStatus_ReportsNotBinded()
    {
        TestUser user = await users.CreateUniqueAsync("oauth-unbind");
        await BindGitHubAsync(user.Id, $"device-unbind-{Guid.NewGuid():N}");
        Assert.True((await StatusAsync(user.AccessToken)).DeserializeData<OAuthBindStatus>()!.IsBinded);

        using HttpResponseMessage unbind = await client.GetWithTokenAsync("/OAuth/Unbind?provider=Github", user.AccessToken);
        TestResponse parsed = await unbind.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);

        TestResponse status = await StatusAsync(user.AccessToken);
        Assert.False(status.DeserializeData<OAuthBindStatus>()!.IsBinded);
    }

    [Fact]
    public async Task Status_And_Unbind_RequireSignIn()
    {
        foreach (string url in new[] { "/OAuth/Status?provider=Github", "/OAuth/Unbind?provider=Github" })
        {
            using HttpResponseMessage response = await client.GetAsync(url, TestContext.Current.CancellationToken);
            TestResponse parsed = await response.ReadTestResponseAsync();
            Assert.Equal(ReturnCodes.LoginFail, parsed.Retcode);
        }
    }

    [Fact]
    public async Task UnknownProvider_IsRejected()
    {
        using HttpResponseMessage response = await client.GetAsync("/OAuth/Auth?provider=9527&device_id=d&device_name=n&device_os=Windows&callback=https://hutao.app/cb",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string QueryValue(string url, string key)
    {
        string query = url.Substring(url.IndexOf('?') + 1);
        foreach (string pair in query.Split('&'))
        {
            string[] parts = pair.Split('=', 2);
            if (parts.Length is 2 && parts[0] == key)
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        throw new InvalidOperationException($"Query parameter '{key}' not found in '{url}'.");
    }
}
