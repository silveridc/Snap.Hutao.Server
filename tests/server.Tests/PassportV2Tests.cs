using server.Models.Passport;
using server.Tests.Infrastructure;
using System.Net;

namespace server.Tests;

/// <summary>
/// <c>/Passport/v2/*</c> 账户接口：注册、登录、令牌刷新、设备管理与用户信息。
/// 覆盖正常流程与各校验/鉴权失败分支。
/// </summary>
[Collection(HutaoServerCollection.Name)]
public sealed class PassportV2Tests
{
    private const string Password = "TestPassword123";

    private readonly HutaoServerFactory factory;
    private readonly HttpClient client;
    private readonly TestUsers users;

    public PassportV2Tests(HutaoServerFactory factory)
    {
        this.factory = factory;
        client = factory.CreateApiClient();
        users = new TestUsers(factory);
    }

    private static string NewEmail(string tag) => $"{tag}-{Guid.NewGuid():N}@example.com";

    private static Dictionary<string, object?> Body(
        string? userName = null,
        string? password = null,
        string? verifyCode = null,
        string? newUserName = null,
        string? newVerifyCode = null,
        bool isResetPassword = false,
        bool isResetUserName = false,
        bool isResetUserNameNew = false,
        bool isCancelRegistration = false)
    {
        Dictionary<string, object?> body = [];
        if (userName is not null)
        {
            body["UserName"] = RsaPayload.Encrypt(userName);
        }

        if (password is not null)
        {
            body["Password"] = RsaPayload.Encrypt(password);
        }

        if (verifyCode is not null)
        {
            body["VerifyCode"] = RsaPayload.Encrypt(verifyCode);
        }

        if (newUserName is not null)
        {
            body["NewUserName"] = RsaPayload.Encrypt(newUserName);
        }

        if (newVerifyCode is not null)
        {
            body["NewVerifyCode"] = RsaPayload.Encrypt(newVerifyCode);
        }

        if (isResetPassword)
        {
            body["IsResetPassword"] = true;
        }

        if (isResetUserName)
        {
            body["IsResetUserName"] = true;
        }

        if (isResetUserNameNew)
        {
            body["IsResetUserNameNew"] = true;
        }

        if (isCancelRegistration)
        {
            body["IsCancelRegistration"] = true;
        }

        return body;
    }

    /// <summary>
    /// 申请验证码并从假 SMTP 中取出邮件里的验证码。
    /// 每次调用前清空收件队列，避免取到其它用例遗留的验证码。
    /// </summary>
    private async Task<string> RequestVerifyCodeAsync(Dictionary<string, object?> body, string? token = null)
    {
        factory.Smtp.Clear();

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync("/Passport/v2/Verify", body, token);
        TestResponse parsed = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);

        return await factory.Smtp.WaitForVerifyCodeAsync(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Register_WithVerificationCode_SucceedsAndIssuesToken()
    {
        string email = NewEmail("v2-register");

        string code = await RequestVerifyCodeAsync(Body(userName: email));

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/Register", Body(userName: email, password: Password, verifyCode: code), token: null);

        TestResponse parsed = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);

        TokenResponse? token = parsed.DeserializeData<TokenResponse>();
        Assert.NotNull(token);
        Assert.False(string.IsNullOrEmpty(token!.AccessToken));
        Assert.False(string.IsNullOrEmpty(token.RefreshToken));

        // 首次注册会赠送 3 天祈愿记录与 CDN 服务
        using HttpResponseMessage infoResponse = await client.GetWithTokenAsync("/Passport/v2/UserInfo", token.AccessToken);
        TestResponse info = await infoResponse.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, info.Retcode);

        UserInfo? user = info.DeserializeData<UserInfo>();
        Assert.NotNull(user);
        Assert.Equal(email.ToUpperInvariant(), user!.NormalizedUserName);
        Assert.True(user.GachaLogExpireAt > DateTimeOffset.UtcNow);
        Assert.True(user.CdnExpireAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Register_WithWrongVerificationCode_Fails()
    {
        string email = NewEmail("v2-register-bad");

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/Register", Body(userName: email, password: Password, verifyCode: "WRONGCOD"), token: null);

        TestResponse parsed = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.RegisterFail, parsed.Retcode);
    }

    [Fact]
    public async Task Register_WithShortPassword_Fails()
    {
        string email = NewEmail("v2-register-short");

        string code = await RequestVerifyCodeAsync(Body(userName: email));

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/Register", Body(userName: email, password: "short", verifyCode: code), token: null);

        TestResponse parsed = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.TooShortPassword, parsed.Retcode);
    }

    [Fact]
    public async Task Register_WithExistingAccount_Fails()
    {
        TestUser existing = await users.CreateUniqueAsync("v2-register-dup");
        string email = existing.UserName;

        using HttpResponseMessage verifyResponse = await client.PostJsonWithTokenAsync(
            "/Passport/v2/Verify", Body(userName: email), token: null);

        // 已存在的邮箱不允许申请注册验证码
        TestResponse verify = await verifyResponse.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.VerifyCodeNotAllowed, verify.Retcode);
    }

    [Fact]
    public async Task Verify_RequestedTwiceWithinOneMinute_IsThrottled()
    {
        string email = NewEmail("v2-verify-throttle");

        using HttpResponseMessage first = await client.PostJsonWithTokenAsync("/Passport/v2/Verify", Body(userName: email), token: null);
        Assert.Equal(ReturnCodes.Success, (await first.ReadTestResponseAsync()).Retcode);

        using HttpResponseMessage second = await client.PostJsonWithTokenAsync("/Passport/v2/Verify", Body(userName: email), token: null);
        Assert.Equal(ReturnCodes.VerifyCodeTooFrequently, (await second.ReadTestResponseAsync()).Retcode);
    }

    [Fact]
    public async Task Verify_ForResetPasswordOfUnknownAccount_IsRejected()
    {
        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/Verify", Body(userName: NewEmail("v2-verify-unknown"), isResetPassword: true), token: null);

        TestResponse parsed = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.VerifyCodeNotAllowed, parsed.Retcode);
    }

    [Fact]
    public async Task Register_WithoutDeviceHeader_IsRejected()
    {
        // Register/Login/LoggedInDevices 会读取设备头，缺失时由异常处理中间件返回错误
        using HttpRequestMessage request = new(HttpMethod.Post, "/Passport/v2/Register")
        {
            Content = JsonContent.Create(Body(userName: NewEmail("v2-nodevice"), password: Password, verifyCode: "INVALID0")),
        };
        request.WithClientVersion();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(
            response.StatusCode is HttpStatusCode.InternalServerError or HttpStatusCode.BadRequest,
            $"Expected the missing device header to be rejected, got {(int)response.StatusCode}: {body}");
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenPair()
    {
        TestUser user = await users.CreateUniqueAsync("v2-login");

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/Login", Body(userName: user.UserName, password: Password), token: null);

        TestResponse parsed = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);

        TokenResponse? token = parsed.DeserializeData<TokenResponse>();
        Assert.NotNull(token);
        Assert.False(string.IsNullOrEmpty(token!.AccessToken));
        Assert.False(string.IsNullOrEmpty(token.RefreshToken));

        // 设备版访问令牌有效期 15 分钟
        Assert.Equal(15 * 60, token.ExpiresIn);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Fails()
    {
        TestUser user = await users.CreateUniqueAsync("v2-login-bad");

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/Login", Body(userName: user.UserName, password: "WrongPassword123"), token: null);

        TestResponse parsed = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.LoginFail, parsed.Retcode);
    }

    [Fact]
    public async Task Login_WithUnknownAccount_Fails()
    {
        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/Login", Body(userName: NewEmail("v2-login-unknown"), password: Password), token: null);

        TestResponse parsed = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.LoginFail, parsed.Retcode);
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_IssuesNewAccessToken()
    {
        TestUser user = await users.CreateUniqueAsync("v2-refresh");

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/RefreshToken",
            new Dictionary<string, object?> { ["RefreshToken"] = RsaPayload.Encrypt(user.RefreshToken) },
            token: null);

        TestResponse parsed = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);

        TokenResponse? token = parsed.DeserializeData<TokenResponse>();
        Assert.NotNull(token);
        Assert.False(string.IsNullOrEmpty(token!.AccessToken));
    }

    [Fact]
    public async Task RefreshToken_WithEmptyToken_ReturnsInvalidRequestBody()
    {
        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/RefreshToken", new Dictionary<string, object?> { ["RefreshToken"] = string.Empty }, token: null);

        TestResponse parsed = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.InvalidRequestBody, parsed.Retcode);
    }

    [Fact]
    public async Task RefreshToken_WithGarbageToken_Fails()
    {
        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/RefreshToken",
            new Dictionary<string, object?> { ["RefreshToken"] = RsaPayload.Encrypt("not-a-real-refresh-token") },
            token: null);

        TestResponse parsed = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.LoginFail, parsed.Retcode);
    }

    [Fact]
    public async Task LoggedInDevices_ListsTheDeviceUsedToLogIn()
    {
        string deviceId = $"device-{Guid.NewGuid():N}";
        TestUser user = await users.CreateUniqueAsync("v2-devices");
        TokenResponse token = await users.IssueTokenAsync(user.Id, new DeviceInfo(deviceId, "Integration Device", "Windows"));

        using HttpRequestMessage request = new(HttpMethod.Get, "/Passport/v2/LoggedInDevices");
        request.WithToken(token.AccessToken);
        request.WithDevice(deviceId, "Integration Device", "Windows");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        TestResponse parsed = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);

        List<LoggedInDeviceInfo>? devices = parsed.DeserializeData<List<LoggedInDeviceInfo>>();
        Assert.NotNull(devices);
        Assert.Contains(devices!, device => device.DeviceId == deviceId);
        Assert.Contains(devices!, device => device.DeviceId == deviceId && device.IsCurrentDevice);
    }

    [Fact]
    public async Task RevokeToken_RemovesOnlyTheTargetedDevice()
    {
        string deviceA = $"device-a-{Guid.NewGuid():N}";
        string deviceB = $"device-b-{Guid.NewGuid():N}";

        TestUser user = await users.CreateUniqueAsync("v2-revoke");
        TokenResponse tokenA = await users.IssueTokenAsync(user.Id, new DeviceInfo(deviceA, "Device A", "Windows"));
        await users.IssueTokenAsync(user.Id, new DeviceInfo(deviceB, "Device B", "Windows"));

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/RevokeToken",
            new Dictionary<string, object?> { ["DeviceId"] = RsaPayload.Encrypt(deviceA) },
            tokenA.AccessToken);

        Assert.Equal(ReturnCodes.Success, (await response.ReadTestResponseAsync()).Retcode);

        using HttpRequestMessage listRequest = new(HttpMethod.Get, "/Passport/v2/LoggedInDevices");
        listRequest.WithToken(tokenA.AccessToken);
        listRequest.WithDevice(deviceA);

        using HttpResponseMessage listResponse = await client.SendAsync(listRequest, TestContext.Current.CancellationToken);
        List<LoggedInDeviceInfo>? devices = (await listResponse.ReadTestResponseAsync()).DeserializeData<List<LoggedInDeviceInfo>>();

        Assert.NotNull(devices);
        Assert.DoesNotContain(devices!, device => device.DeviceId == deviceA);
        Assert.Contains(devices!, device => device.DeviceId == deviceB);
    }

    [Fact]
    public async Task RevokeToken_WithEmptyDeviceId_ReturnsInvalidRequestBody()
    {
        TestUser user = await users.CreateUniqueAsync("v2-revoke-empty");

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/RevokeToken", new Dictionary<string, object?> { ["DeviceId"] = string.Empty }, token: user.AccessToken);

        TestResponse parsed = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.InvalidRequestBody, parsed.Retcode);
    }

    [Fact]
    public async Task RevokeAllTokens_InvalidatesEveryRefreshToken()
    {
        TestUser user = await users.CreateUniqueAsync("v2-revoke-all");
        TokenResponse deviceToken = await users.IssueTokenAsync(user.Id, new DeviceInfo($"device-{Guid.NewGuid():N}", "Device", "Windows"));

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/RevokeAllTokens", new Dictionary<string, object?>(), token: deviceToken.AccessToken);

        Assert.Equal(ReturnCodes.Success, (await response.ReadTestResponseAsync()).Retcode);

        using HttpResponseMessage refreshResponse = await client.PostJsonWithTokenAsync(
            "/Passport/v2/RefreshToken",
            new Dictionary<string, object?> { ["RefreshToken"] = RsaPayload.Encrypt(deviceToken.RefreshToken) },
            token: null);

        Assert.Equal(ReturnCodes.LoginFail, (await refreshResponse.ReadTestResponseAsync()).Retcode);
    }

    [Fact]
    public async Task UserInfo_WithoutToken_ReturnsLoginFailEnvelope()
    {
        using HttpResponseMessage response = await client.GetWithTokenAsync("/Passport/v2/UserInfo", token: null);

        TestResponse parsed = await response.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.LoginFail, parsed.Retcode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UserInfo_ReflectsPrivilegeFlags()
    {
        TestUser maintainer = await users.MaintainerAsync();

        using HttpResponseMessage response = await client.GetWithTokenAsync("/Passport/v2/UserInfo", maintainer.AccessToken);
        UserInfo? info = (await response.ReadTestResponseAsync()).DeserializeData<UserInfo>();

        Assert.NotNull(info);
        Assert.True(info!.IsMaintainer);
        Assert.True(info.IsLicensedDeveloper);
    }

    [Fact]
    public async Task ResetPassword_WithValidCode_ChangesPassword()
    {
        TestUser user = await users.CreateUniqueAsync("v2-resetpwd");
        const string newPassword = "BrandNewPassword456";

        string code = await RequestVerifyCodeAsync(Body(userName: user.UserName, isResetPassword: true));

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/ResetPassword", Body(userName: user.UserName, password: newPassword, verifyCode: code), token: null);
        Assert.Equal(ReturnCodes.Success, (await response.ReadTestResponseAsync()).Retcode);

        using HttpResponseMessage loginResponse = await client.PostJsonWithTokenAsync(
            "/Passport/v2/Login", Body(userName: user.UserName, password: newPassword), token: null);

        Assert.Equal(ReturnCodes.Success, (await loginResponse.ReadTestResponseAsync()).Retcode);
    }

    [Fact]
    public async Task ResetPassword_WithSamePassword_Fails()
    {
        TestUser user = await users.CreateUniqueAsync("v2-resetpwd-same");

        string code = await RequestVerifyCodeAsync(Body(userName: user.UserName, isResetPassword: true));

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/ResetPassword", Body(userName: user.UserName, password: Password, verifyCode: code), token: null);

        Assert.Equal(ReturnCodes.RegisterFail, (await response.ReadTestResponseAsync()).Retcode);
    }

    [Fact]
    public async Task ResetUsername_WithBothCodes_SwitchesAccountName()
    {
        TestUser user = await users.CreateUniqueAsync("v2-resetname");
        string newEmail = NewEmail("v2-resetname-new");

        string oldCode = await RequestVerifyCodeAsync(Body(userName: user.UserName, isResetUserName: true));
        string newCode = await RequestVerifyCodeAsync(Body(userName: newEmail, isResetUserNameNew: true));

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/ResetUsername",
            Body(userName: user.UserName, verifyCode: oldCode, newUserName: newEmail, newVerifyCode: newCode),
            token: null);
        Assert.Equal(ReturnCodes.Success, (await response.ReadTestResponseAsync()).Retcode);

        using HttpResponseMessage loginResponse = await client.PostJsonWithTokenAsync(
            "/Passport/v2/Login", Body(userName: newEmail, password: Password), token: null);

        Assert.Equal(ReturnCodes.Success, (await loginResponse.ReadTestResponseAsync()).Retcode);
    }

    [Fact]
    public async Task Cancel_WithValidCode_DeletesAccount()
    {
        TestUser user = await users.CreateUniqueAsync("v2-cancel");

        string code = await RequestVerifyCodeAsync(Body(userName: user.UserName, isCancelRegistration: true), user.AccessToken);

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/Cancel", Body(userName: user.UserName, password: Password, verifyCode: code), token: null);
        Assert.Equal(ReturnCodes.Success, (await response.ReadTestResponseAsync()).Retcode);

        using HttpResponseMessage loginResponse = await client.PostJsonWithTokenAsync(
            "/Passport/v2/Login", Body(userName: user.UserName, password: Password), token: null);

        Assert.Equal(ReturnCodes.LoginFail, (await loginResponse.ReadTestResponseAsync()).Retcode);
    }

    [Fact]
    public async Task Cancel_WithWrongCode_IsRejected()
    {
        TestUser user = await users.CreateUniqueAsync("v2-cancel-badcode");

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/Passport/v2/Cancel", Body(userName: user.UserName, password: Password, verifyCode: "INVALID0"), token: null);

        Assert.Equal(ReturnCodes.RegisterFail, (await response.ReadTestResponseAsync()).Retcode);
    }
}
