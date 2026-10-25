using MailKit.Net.Smtp;
using MimeKit;
using server.Models.Admin;
using server.Models.Passport;
using server.Services.Admin;

namespace server.Controllers;

[ApiController]
[Route("api/Passport/v2")]
[ApiExplorerSettings(GroupName = "Passport")]
public class PassportV2Controller(IServiceProvider serviceProvider) : ControllerBase
{
    private readonly PassportVerificationService _passportVerificationService = serviceProvider.GetRequiredService<PassportVerificationService>();
    private readonly AppDbContext _appDbContext = serviceProvider.GetRequiredService<AppDbContext>();
    private readonly PassportService _passportService = serviceProvider.GetRequiredService<PassportService>();
    private readonly MailService _mailService = serviceProvider.GetRequiredService<MailService>();
    private readonly AdminSsoService _adminSsoService = serviceProvider.GetRequiredService<AdminSsoService>();

    /// <summary>
    /// 申请邮箱验证码以完成注册、找回密码或账号安全操作；同一邮箱同一场景 60 秒内限一次。
    /// </summary>
    /// <param name="request">包含用户名与场景标记的请求体。</param>
    /// <returns>验证码申请结果。</returns>
    [HttpPost("Verify")]
    public async Task<IActionResult> RequestVerifyCodeAsync([FromBody] PassportRequest request)
    {
        string normalizedUserName = _passportService.DecryptNormalizedUserName(request, out string userName);
        string key = request switch
        {
            { IsResetPassword: true } => PassportVerificationService.ResetPassword,
            { IsResetUserName: true } => PassportVerificationService.ResetUserName,
            { IsResetUserNameNew: true } => PassportVerificationService.ResetUserNameNew,
            { IsCancelRegistration: true } => PassportVerificationService.CancelRegistration,
            _ => "Registration",
        };

        if (_passportVerificationService.TryGetNonExpiredVerifyCode(normalizedUserName, key, out _))
        {
            return Fail("Too many requests, please try again in 1 minute", ReturnCode.VerifyCodeTooFrequently, ServerKeys.ServerPassportVerifyTooFrequent);
        }

        bool userExists = _appDbContext.Users.Any(u => u.NormalizedUserName == normalizedUserName);
        string code = _passportVerificationService.GenerateVerifyCode(normalizedUserName, key);

        try
        {
            return await PrivateRequestVerifyCodeAsync(request, normalizedUserName, userName, userExists, code).ConfigureAwait(false);
        }
        catch (SmtpCommandException)
        {
            return Success("Failed to request verification code", ServerKeys.ServerPassportVerifyRequestFailed);
        }
    }

    /// <summary>
    /// 使用邮箱验证码注册账号并下发访问令牌。
    /// </summary>
    /// <param name="request">包含用户名、密码（至少 8 位）与验证码的请求体。</param>
    /// <returns>注册结果。</returns>
    [HttpPost("Register")]
    public async Task<IActionResult> RegisterAsync([FromBody] PassportRequest request)
    {
        DeviceInfo deviceInfo = this.GetDeviceInfo();
        string normalizedUserName = _passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);

        if (!_passportVerificationService.TryValidateVerifyCode(normalizedUserName, PassportVerificationService.Registration, code))
        {
            return Fail("Verification failed", ReturnCode.RegisterFail, ServerKeys.ServerPassportVerifyFailed);
        }

        Passport passport = new()
        {
            UserName = userName,
            Password = _passportService.Decrypt(request.Password),
        };

        if (passport.Password.Length < 8)
        {
            return Fail("Password must be at least 8 characters", ReturnCode.TooShortPassword, ServerKeys.ServerPassportPasswordTooShort);
        }

        PassportResult result = await _passportService.RegisterAsync(passport, deviceInfo).ConfigureAwait(false);
        return result.Success
            ? Success<TokenResponse>(result.Message, result.Token, result.LocalizationKey!)
            : Fail(result.Message, ReturnCode.RegisterFail, result.LocalizationKey!);
    }

    /// <summary>
    /// 注销现有账号并删除关联数据，操作不可逆。
    /// </summary>
    /// <param name="request">包含用户名、密码与验证码的请求体。</param>
    /// <returns>注销结果。</returns>
    [HttpPost("Cancel")]
    public async Task<IActionResult> CancelRegistrationAsync([FromBody] PassportRequest request)
    {
        string normalizedUserName = _passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);

        if (!_passportVerificationService.TryValidateVerifyCode(normalizedUserName, PassportVerificationService.CancelRegistration, code))
        {
            return Fail("Verification failed", ReturnCode.RegisterFail, ServerKeys.ServerPassportVerifyFailed);
        }

        Passport passport = new()
        {
            UserName = userName,
            Password = _passportService.Decrypt(request.Password),
        };

        PassportResult result = await _passportService.CancelAsync(passport).ConfigureAwait(false);

        return result.Success
            ? Success(result.Message, result.LocalizationKey!)
            : Fail(result.Message, ReturnCode.CancelFail, result.LocalizationKey!);
    }

    /// <summary>
    /// 通过邮箱验证码设置新的登录密码。
    /// </summary>
    /// <param name="request">包含用户名、新密码与验证码的请求体。</param>
    /// <returns>重置结果。</returns>
    [HttpPost("ResetPassword")]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] PassportRequest request)
    {
        DeviceInfo deviceInfo = this.GetDeviceInfo();
        string normalizedUserName = _passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);

        if (!_passportVerificationService.TryValidateVerifyCode(normalizedUserName, PassportVerificationService.ResetPassword, code))
        {
            return Fail("Verification failed", ReturnCode.RegisterFail, ServerKeys.ServerPassportVerifyFailed);
        }

        Passport passport = new()
        {
            UserName = userName,
            Password = _passportService.Decrypt(request.Password),
        };

        if (passport.Password.Length < 8)
        {
            return Fail("Password must be at least 8 characters", ReturnCode.TooShortPassword, ServerKeys.ServerPassportPasswordTooShort);
        }

        PassportResult result = await _passportService.ResetPasswordAsync(passport, deviceInfo).ConfigureAwait(false);

        return result.Success
            ? Success<TokenResponse>(result.Message, result.Token, result.LocalizationKey!)
            : Fail(result.Message, ReturnCode.RegisterFail, result.LocalizationKey!);
    }

    /// <summary>
    /// 修改绑定邮箱并重新签发访问令牌。
    /// </summary>
    /// <param name="request">包含原邮箱、新邮箱及对应验证码的请求体。</param>
    /// <returns>重置结果。</returns>
    [HttpPost("ResetUsername")]
    public async Task<IActionResult> ResetUsernameAsync([FromBody] PassportRequest request)
    {
        DeviceInfo deviceInfo = this.GetDeviceInfo();
        string normalizedUserName = _passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);
        string newNormalizedUserName = _passportService.DecryptNewNormalizedUserNameAndNewVerifyCode(request, out string newUserName, out string newCode);

        if (!_passportVerificationService.TryValidateVerifyCode(normalizedUserName, PassportVerificationService.ResetUserName, code) ||
            !_passportVerificationService.TryValidateVerifyCode(newNormalizedUserName, PassportVerificationService.ResetUserNameNew, newCode))
        {
            return Fail("Verification failed", ReturnCode.RegisterFail, ServerKeys.ServerPassportVerifyFailed);
        }

        Passport passport = new()
        {
            UserName = userName,
            NewUserName = newUserName,
        };

        PassportResult result = await _passportService.ResetUsernameAsync(passport, deviceInfo).ConfigureAwait(false);

        return result.Success
            ? Success<TokenResponse>(result.Message, result.Token, result.LocalizationKey!)
            : Fail(result.Message, ReturnCode.RegisterFail, result.LocalizationKey!);
    }

    /// <summary>
    /// 使用邮箱与密码登录并颁发访问令牌与刷新令牌。
    /// </summary>
    /// <param name="request">包含用户名与密码的请求体。</param>
    /// <returns>登录结果。</returns>
    [HttpPost("Login")]
    public async Task<IActionResult> LoginAsync([FromBody] PassportRequest request)
    {
        DeviceInfo deviceInfo = this.GetDeviceInfo();
        Passport passport = new()
        {
            UserName = _passportService.Decrypt(request.UserName),
            Password = _passportService.Decrypt(request.Password),
        };

        PassportResult result = await _passportService.LoginAsync(passport, deviceInfo).ConfigureAwait(false);

        return result.Success
            ? Success<TokenResponse>(result.Message, result.Token, result.LocalizationKey!)
            : Fail(result.Message, ReturnCode.LoginFail, result.LocalizationKey!);
    }

    /// <summary>
    /// 获取当前登录用户的基础资料与资源到期时间。
    /// </summary>
    /// <returns>用户信息。</returns>
    [Authorize]
    [HttpGet("UserInfo")]
    public async Task<IActionResult> GetUserInfoAsync()
    {
        if (this.TryGetAdminAccountId(out int adminAccountId))
        {
            AdminAccountState? admin = await _adminSsoService.GetAccountStateAsync(adminAccountId).ConfigureAwait(false);
            if (admin is null)
            {
                return Fail("User does not exist", ReturnCode.UserNameNotExists, ServerKeys.ServerPassportUserInfoNotExist);
            }

            return Success<UserInfo>(
                "User info acquired successfully",
                new()
                {
                    UserName = admin.UserName,
                    NormalizedUserName = admin.UserName.ToUpperInvariant(),
                    AuthKind = "admin",
                    CanWrite = admin.IsEnabled,
                },
                ServerKeys.ServerPassportUserInfoSucceed);
        }

        HutaoUser? user = await this.GetUserAsync(_appDbContext.Users).ConfigureAwait(false);

        if (user is null)
        {
            return Fail("User does not exist", ReturnCode.UserNameNotExists, ServerKeys.ServerPassportUserInfoNotExist);
        }

        // 启用单点登录后，主站维护者账号降级为只读；被后台账号绑定的用户才继续可写（启动器的 SSO 登录走的就是这条路）
        bool canWrite = _adminSsoService.IsEnabled
            ? await _adminSsoService.IsUserProvisionedAsync(user.Id, HttpContext.RequestAborted).ConfigureAwait(false)
            : user.IsMaintainer;

        return Success<UserInfo>(
            "User info acquired successfully",
            new()
            {
                NormalizedUserName = user.NormalizedUserName ?? user.UserName,
                UserName = user.UserName,
                IsLicensedDeveloper = user.IsLicensedDeveloper,
                IsMaintainer = user.IsMaintainer,
                CanWrite = canWrite,
                GachaLogExpireAt = DateTimeOffset.FromUnixTimeSeconds(user.GachaLogExpireAt),
                CdnExpireAt = DateTimeOffset.FromUnixTimeSeconds(user.CdnExpireAt),
            },
            ServerKeys.ServerPassportUserInfoSucceed);
    }

    /// <summary>
    /// 查询当前账号所有有效的登录设备。
    /// </summary>
    /// <returns>已登录设备列表。</returns>
    [Authorize]
    [HttpGet("LoggedInDevices")]
    public async Task<IActionResult> GetLoggedInDevicesAsync()
    {
        DeviceInfo deviceInfo = this.GetDeviceInfo();
        int userId = this.GetUserId();
        List<LoggedInDeviceInfo> devices = await _passportService.GetLoggedInDevicesAsync(userId, deviceInfo).ConfigureAwait(false);
        return Success<List<LoggedInDeviceInfo>>("Logged-in devices acquired successfully", devices, ServerKeys.ServerPassportLoggedInDevicesSucceed);
    }

    /// <summary>
    /// 使用刷新令牌换取新的访问令牌。
    /// </summary>
    /// <param name="request">包含刷新令牌的请求体。</param>
    /// <returns>新的令牌。</returns>
    [HttpPost("RefreshToken")]
    public async Task<IActionResult> RefreshTokenAsync([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return Fail("Refresh token must not be empty", ReturnCode.InvalidRequestBody, ServerKeys.ServerPassportRefreshTokenEmpty);
        }

        TokenResponse? tokenResponse = await _passportService.RefreshTokenAsync(_passportService.Decrypt(request.RefreshToken));
        if (tokenResponse is null)
        {
            return Fail("Refresh token is invalid or expired", ReturnCode.LoginFail, ServerKeys.ServerPassportRefreshTokenInvalid);
        }

        return Success<TokenResponse>("Token refreshed successfully", tokenResponse, ServerKeys.ServerPassportTokenRefreshSucceed);
    }

    /// <summary>
    /// 撤销指定设备的刷新令牌，仅限当前账号的设备。
    /// </summary>
    /// <param name="request">包含设备 ID 的请求体。</param>
    /// <returns>撤销结果。</returns>
    [Authorize]
    [HttpPost("RevokeToken")]
    public async Task<IActionResult> RevokeTokenAsync([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.DeviceId))
        {
            return Fail("Device ID must not be empty", ReturnCode.InvalidRequestBody, ServerKeys.ServerPassportDeviceIdEmpty);
        }

        return await _passportService.RevokeRefreshTokenAsync(_passportService.Decrypt(request.DeviceId)).ConfigureAwait(false)
            ? Success("Token revoked successfully", ServerKeys.ServerPassportTokenRevokeSuccess)
            : Fail("Failed to revoke token", ReturnCode.RefreshTokenDbException, ServerKeys.ServerPassportTokenRevokeFailed);
    }

    /// <summary>
    /// 撤销当前账号所有刷新令牌并强制登出所有设备。
    /// </summary>
    /// <returns>撤销结果。</returns>
    [Authorize]
    [HttpPost("RevokeAllTokens")]
    public async Task<IActionResult> RevokeAllTokensAsync()
    {
        int userId = this.GetUserId();
        await _passportService.RevokeAllUserTokensAsync(userId);
        return Success("All tokens have been revoked", ServerKeys.ServerPassportRevokeAllTokensSucceed);
    }

    /// <summary>
    /// 根据场景发送对应的验证码邮件。
    /// </summary>
    /// <param name="request">验证码请求。</param>
    /// <param name="normalizedUserName">标准化后的用户名。</param>
    /// <param name="userName">原始用户名。</param>
    /// <param name="userExists">用户是否存在。</param>
    /// <param name="code">验证码。</param>
    /// <returns>验证码发送结果。</returns>
    private async Task<IActionResult> PrivateRequestVerifyCodeAsync(PassportRequest request, string normalizedUserName, string userName, bool userExists, string code)
    {
        try
        {
            if (request.IsResetPassword)
            {
                if (!userExists)
                {
                    _passportVerificationService.RemoveVerifyCode(normalizedUserName, PassportVerificationService.ResetPassword);
                    return Fail("Failed to request verification code", ReturnCode.VerifyCodeNotAllowed, ServerKeys.ServerPassportVerifyRequestUserNotExisted);
                }

                await _mailService.SendResetPasswordVerifyCodeAsync(userName, code).ConfigureAwait(false);
                return Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
            }

            if (request.IsResetUserName)
            {
                if (!userExists)
                {
                    _passportVerificationService.RemoveVerifyCode(normalizedUserName, PassportVerificationService.ResetUserName);
                    return Fail("Failed to request verification code", ReturnCode.VerifyCodeNotAllowed, ServerKeys.ServerPassportVerifyRequestUserNotExisted);
                }

                await _mailService.SendResetUsernameVerifyCodeAsync(userName, code).ConfigureAwait(false);
                return Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
            }

            if (request.IsCancelRegistration)
            {
                if (!userExists)
                {
                    _passportVerificationService.RemoveVerifyCode(normalizedUserName, PassportVerificationService.CancelRegistration);
                    return Fail("Failed to request verification code", ReturnCode.VerifyCodeNotAllowed, ServerKeys.ServerPassportVerifyRequestUserNotExisted);
                }

                await _mailService.SendCancelRegistrationVerifyCodeAsync(userName, code).ConfigureAwait(false);
                return Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
            }

            if (userExists)
            {
                _passportVerificationService.RemoveVerifyCode(normalizedUserName, PassportVerificationService.Registration);
                _passportVerificationService.RemoveVerifyCode(normalizedUserName, PassportVerificationService.ResetUserNameNew);
                return Fail("Failed to request verification code", ReturnCode.VerifyCodeNotAllowed, ServerKeys.ServerPassportVerifyRequestUserAlreadyExisted);
            }

            if (request.IsResetUserNameNew)
            {
                await _mailService.SendResetUsernameVerifyCodeAsync(userName, code).ConfigureAwait(false);
                return Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
            }

            await _mailService.SendRegistrationVerifyCodeAsync(userName, code).ConfigureAwait(false);
            return Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
        }
        catch (ParseException)
        {
            return Fail("Failed to request verification code", ReturnCode.InvalidUserName, ServerKeys.ServerPassportVerifyRequestUserNameInvalid);
        }
    }
}
