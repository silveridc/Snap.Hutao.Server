using MailKit.Net.Smtp;
using MimeKit;
using server.Models.Admin;
using server.Models.Passport;
using server.Models.ReCaptcha;
using server.Services.Admin;

namespace server.Controllers;

[ApiController]
[Route("api/passport/v2")]
[ApiExplorerSettings(GroupName = "Passport")]
public class PassportV2Controller(
    PassportVerificationService passportVerificationService,
    AppDbContext appDbContext,
    PassportService passportService,
    MailService mailService,
    AdminSsoService adminSsoService,
    AdminQueryService adminQueryService,
    UserLoginLogService userLoginLogService,
    SystemConfigService systemConfigService,
    ReCaptchaService reCaptchaService,
    AppOptions appOptions) : ControllerBase
{
    /// <summary>
    /// 申请邮箱验证码以完成注册、找回密码或账号安全操作；同一邮箱同一场景 60 秒内限一次.
    /// </summary>
    /// <param name="request">包含用户名与场景标记的请求体.</param>
    /// <returns>验证码申请结果.</returns>
    [HttpPost("verification-codes")]
    public async Task<IActionResult> RequestVerifyCodeAsync([FromBody] PassportRequest request)
    {
        string normalizedUserName = passportService.DecryptNormalizedUserName(request, out string userName);
        string key = request switch
        {
            { IsResetPassword: true } => PassportVerificationService.ResetPassword,
            { IsResetUserName: true } => PassportVerificationService.ResetUserName,
            { IsResetUserNameNew: true } => PassportVerificationService.ResetUserNameNew,
            { IsCancelRegistration: true } => PassportVerificationService.CancelRegistration,
            _ => "Registration",
        };

        if (passportVerificationService.TryGetNonExpiredVerifyCode(normalizedUserName, key, out _))
        {
            return Fail("Too many requests, please try again in 1 minute", ReturnCode.VerifyCodeTooFrequently, ServerKeys.ServerPassportVerifyTooFrequent);
        }

        bool userExists = appDbContext.Users.Any(u => u.NormalizedUserName == normalizedUserName);
        string code = passportVerificationService.GenerateVerifyCode(normalizedUserName, key);

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
    /// 使用邮箱验证码注册账号并下发访问令牌.
    /// </summary>
    /// <param name="request">包含用户名、密码（至少 8 位）与验证码的请求体.</param>
    /// <returns>注册结果.</returns>
    [HttpPost("accounts")]
    public async Task<IActionResult> RegisterAsync([FromBody] PassportRequest request)
    {
        DeviceInfo deviceInfo = this.GetDeviceInfo();
        string normalizedUserName = passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);

        if (!passportVerificationService.TryValidateVerifyCode(normalizedUserName, PassportVerificationService.Registration, code))
        {
            return Fail("Verification failed", ReturnCode.RegisterFail, ServerKeys.ServerPassportVerifyFailed);
        }

        Passport passport = new()
        {
            UserName = userName,
            Password = passportService.Decrypt(request.Password),
        };

        if (passport.Password.Length < 8)
        {
            return Fail("Password must be at least 8 characters", ReturnCode.TooShortPassword, ServerKeys.ServerPassportPasswordTooShort);
        }

        PassportResult result = await passportService.RegisterAsync(passport, deviceInfo).ConfigureAwait(false);
        return result.Success
            ? Success<TokenResponse>(result.Message, result.Token, result.LocalizationKey!)
            : Fail(result.Message, ReturnCode.RegisterFail, result.LocalizationKey!);
    }

    /// <summary>
    /// 注销现有账号并删除关联数据，操作不可逆.
    /// </summary>
    /// <param name="request">包含用户名、密码与验证码的请求体.</param>
    /// <returns>注销结果.</returns>
    [HttpDelete("accounts/me")]
    public async Task<IActionResult> CancelRegistrationAsync([FromBody] PassportRequest request)
    {
        string normalizedUserName = passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);

        if (!passportVerificationService.TryValidateVerifyCode(normalizedUserName, PassportVerificationService.CancelRegistration, code))
        {
            return Fail("Verification failed", ReturnCode.RegisterFail, ServerKeys.ServerPassportVerifyFailed);
        }

        Passport passport = new()
        {
            UserName = userName,
            Password = passportService.Decrypt(request.Password),
        };

        PassportResult result = await passportService.CancelAsync(passport).ConfigureAwait(false);

        return result.Success
            ? Success(result.Message, result.LocalizationKey!)
            : Fail(result.Message, ReturnCode.CancelFail, result.LocalizationKey!);
    }

    /// <summary>
    /// 通过邮箱验证码设置新的登录密码.
    /// </summary>
    /// <param name="request">包含用户名、新密码与验证码的请求体.</param>
    /// <returns>重置结果.</returns>
    [HttpPut("accounts/me/password")]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] PassportRequest request)
    {
        DeviceInfo deviceInfo = this.GetDeviceInfo();
        string normalizedUserName = passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);

        if (!passportVerificationService.TryValidateVerifyCode(normalizedUserName, PassportVerificationService.ResetPassword, code))
        {
            return Fail("Verification failed", ReturnCode.RegisterFail, ServerKeys.ServerPassportVerifyFailed);
        }

        Passport passport = new()
        {
            UserName = userName,
            Password = passportService.Decrypt(request.Password),
        };

        if (passport.Password.Length < 8)
        {
            return Fail("Password must be at least 8 characters", ReturnCode.TooShortPassword, ServerKeys.ServerPassportPasswordTooShort);
        }

        PassportResult result = await passportService.ResetPasswordAsync(passport, deviceInfo).ConfigureAwait(false);

        return result.Success
            ? Success<TokenResponse>(result.Message, result.Token, result.LocalizationKey!)
            : Fail(result.Message, ReturnCode.RegisterFail, result.LocalizationKey!);
    }

    /// <summary>
    /// 修改绑定邮箱并重新签发访问令牌.
    /// </summary>
    /// <param name="request">包含原邮箱、新邮箱及对应验证码的请求体.</param>
    /// <returns>重置结果.</returns>
    [HttpPut("accounts/me/username")]
    public async Task<IActionResult> ResetUsernameAsync([FromBody] PassportRequest request)
    {
        DeviceInfo deviceInfo = this.GetDeviceInfo();
        string normalizedUserName = passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);
        string newNormalizedUserName = passportService.DecryptNewNormalizedUserNameAndNewVerifyCode(request, out string newUserName, out string newCode);

        if (!passportVerificationService.TryValidateVerifyCode(normalizedUserName, PassportVerificationService.ResetUserName, code) ||
            !passportVerificationService.TryValidateVerifyCode(newNormalizedUserName, PassportVerificationService.ResetUserNameNew, newCode))
        {
            return Fail("Verification failed", ReturnCode.RegisterFail, ServerKeys.ServerPassportVerifyFailed);
        }

        Passport passport = new()
        {
            UserName = userName,
            NewUserName = newUserName,
        };

        PassportResult result = await passportService.ResetUsernameAsync(passport, deviceInfo).ConfigureAwait(false);

        return result.Success
            ? Success<TokenResponse>(result.Message, result.Token, result.LocalizationKey!)
            : Fail(result.Message, ReturnCode.RegisterFail, result.LocalizationKey!);
    }

    /// <summary>
    /// 获取登录页的人机验证配置（站点密钥是公开值）.
    /// </summary>
    /// <returns>是否启用与 reCAPTCHA 站点密钥.</returns>
    [HttpGet("captcha")]
    public IActionResult GetCaptchaSettings()
    {
        bool enabled = systemConfigService.CaptchaEnabled;
        return Success<PassportCaptchaSettings>("Captcha settings acquired successfully", new()
        {
            Enabled = enabled,
            SiteKey = enabled ? appOptions.ReCaptchaSiteKey : null,
        });
    }

    /// <summary>
    /// 启动器内置浏览器使用的 reCAPTCHA 页面，加载后自动执行 v3 并跳回哨兵地址.
    /// </summary>
    /// <returns>HTML 页面.</returns>
    [HttpGet("captcha/page")]
    public IActionResult GetCaptchaPage()
    {
        string siteKey = appOptions.ReCaptchaSiteKey;
        if (!systemConfigService.CaptchaEnabled || string.IsNullOrEmpty(siteKey) || !siteKey.All(static ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-'))
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";

        string html = $$"""
            <!doctype html>
            <html lang="zh-CN">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>Snap Hutao</title>
              <style>
                html, body { height: 100%; margin: 0; }
                body { display: flex; align-items: center; justify-content: center; background: #f5f6f7; color: #444; font-family: "Segoe UI", "Microsoft YaHei", sans-serif; font-size: 14px; }
              </style>
              <script src="https://www.recaptcha.net/recaptcha/api.js?render={{siteKey}}"></script>
              <script>
                grecaptcha.ready(function () {
                  grecaptcha.execute('{{siteKey}}', { action: 'login' }).then(function (token) {
                    window.location.href = 'https://captcha.snaphutao.invalid/callback?token=' + encodeURIComponent(token);
                  }).catch(function () {
                    window.location.href = 'https://captcha.snaphutao.invalid/callback?error=1';
                  });
                });
              </script>
            </head>
            <body>正在验证，请稍候… / Verifying, please wait…</body>
            </html>
            """;

        return Content(html, "text/html; charset=utf-8");
    }

    /// <summary>
    /// 使用邮箱与密码登录并颁发访问令牌与刷新令牌.
    /// </summary>
    /// <param name="request">包含用户名、密码与人机验证令牌的请求体.</param>
    /// <returns>登录结果.</returns>
    [HttpPost("sessions")]
    public async Task<IActionResult> LoginAsync([FromBody] PassportRequest request)
    {
        DeviceInfo deviceInfo = this.GetDeviceInfo();
        Passport passport = new()
        {
            UserName = passportService.Decrypt(request.UserName),
            Password = passportService.Decrypt(request.Password),
        };

        if (systemConfigService.CaptchaEnabled)
        {
            ReCaptchaResponse? captcha = await reCaptchaService
                .VerifyAsync(request.CaptchaToken ?? string.Empty, HttpContext.GetClientIp())
                .ConfigureAwait(false);

            if (captcha is not { Success: true, Action: "login", Score: > 0.5f })
            {
                await userLoginLogService
                    .RecordAsync(HttpContext, deviceInfo, "password", passport.UserName, false, ServerKeys.ServerPassportReCaptchaFailed)
                    .ConfigureAwait(false);

                return Fail("reCAPTCHA verification failed", ReturnCode.ReCaptchaVerifyFailed, ServerKeys.ServerPassportReCaptchaFailed);
            }
        }

        PassportResult result = await passportService.LoginAsync(passport, deviceInfo).ConfigureAwait(false);

        await userLoginLogService
            .RecordAsync(HttpContext, deviceInfo, "password", passport.UserName, result.Success, result.Success ? null : result.LocalizationKey)
            .ConfigureAwait(false);

        return result.Success
            ? Success<TokenResponse>(result.Message, result.Token, result.LocalizationKey!)
            : Fail(result.Message, ReturnCode.LoginFail, result.LocalizationKey!);
    }

    /// <summary>
    /// 分页获取当前账号的登录日志，供客户端自助确认是否有异常登录.
    /// </summary>
    /// <returns>登录记录（含 IP 属地）.</returns>
    [Authorize]
    [HttpGet("me/login-logs")]
    public async Task<IActionResult> GetLoginLogsAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!this.TryGetUserId(out int userId))
        {
            return Fail("User does not exist", ReturnCode.UserNameNotExists, ServerKeys.ServerPassportUserInfoNotExist);
        }

        PagedResult<UserLoginLogItem> result = await adminQueryService
            .GetLoginLogsAsync(page, pageSize, keyword: null, success: null, from: null, to: null, userId)
            .ConfigureAwait(false);

        return Success<PagedResult<UserLoginLogItem>>("Login log acquired successfully", result, ServerKeys.ServerAdminLoginLogListSucceed);
    }

    /// <summary>
    /// 获取当前登录用户的基础资料与资源到期时间.
    /// </summary>
    /// <returns>用户信息.</returns>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetUserInfoAsync()
    {
        if (this.TryGetAdminAccountId(out int adminAccountId))
        {
            AdminAccountState? admin = await adminSsoService.GetAccountStateAsync(adminAccountId).ConfigureAwait(false);
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

        HutaoUser? user = await this.GetUserAsync(appDbContext.Users).ConfigureAwait(false);

        if (user is null)
        {
            return Fail("User does not exist", ReturnCode.UserNameNotExists, ServerKeys.ServerPassportUserInfoNotExist);
        }

        // 启用单点登录后，主站维护者账号降级为只读；被后台账号绑定的用户才继续可写（启动器的 SSO 登录走的就是这条路）
        bool canWrite = adminSsoService.IsEnabled
            ? await adminSsoService.IsUserProvisionedAsync(user.Id, HttpContext.RequestAborted).ConfigureAwait(false)
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
    /// 查询当前账号所有有效的登录设备.
    /// </summary>
    /// <returns>已登录设备列表.</returns>
    [Authorize]
    [HttpGet("sessions")]
    public async Task<IActionResult> GetLoggedInDevicesAsync()
    {
        DeviceInfo deviceInfo = this.GetDeviceInfo();
        int userId = this.GetUserId();
        List<LoggedInDeviceInfo> devices = await passportService.GetLoggedInDevicesAsync(userId, deviceInfo).ConfigureAwait(false);
        return Success<List<LoggedInDeviceInfo>>("Logged-in devices acquired successfully", devices, ServerKeys.ServerPassportLoggedInDevicesSucceed);
    }

    /// <summary>
    /// 使用刷新令牌换取新的访问令牌.
    /// </summary>
    /// <param name="request">包含刷新令牌的请求体.</param>
    /// <returns>新的令牌.</returns>
    [HttpPost("sessions/refresh")]
    public async Task<IActionResult> RefreshTokenAsync([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return Fail("Refresh token must not be empty", ReturnCode.InvalidRequestBody, ServerKeys.ServerPassportRefreshTokenEmpty);
        }

        TokenResponse? tokenResponse = await passportService.RefreshTokenAsync(passportService.Decrypt(request.RefreshToken));
        if (tokenResponse is null)
        {
            return Fail("Refresh token is invalid or expired", ReturnCode.LoginFail, ServerKeys.ServerPassportRefreshTokenInvalid);
        }

        return Success<TokenResponse>("Token refreshed successfully", tokenResponse, ServerKeys.ServerPassportTokenRefreshSucceed);
    }

    /// <summary>
    /// 撤销刷新令牌：携带 deviceId 只撤销该设备，缺省时撤销当前账号的全部设备.
    /// </summary>
    /// <param name="deviceId">加密后的设备 ID.</param>
    /// <returns>撤销结果.</returns>
    [Authorize]
    [HttpDelete("sessions")]
    public async Task<IActionResult> RevokeSessionsAsync([FromQuery] string? deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            await passportService.RevokeAllUserTokensAsync(this.GetUserId());
            return Success("All tokens have been revoked", ServerKeys.ServerPassportRevokeAllTokensSucceed);
        }

        return await passportService.RevokeRefreshTokenAsync(passportService.Decrypt(deviceId)).ConfigureAwait(false)
            ? Success("Token revoked successfully", ServerKeys.ServerPassportTokenRevokeSuccess)
            : Fail("Failed to revoke token", ReturnCode.RefreshTokenDbException, ServerKeys.ServerPassportTokenRevokeFailed);
    }

    /// <summary>
    /// 根据场景发送对应的验证码邮件.
    /// </summary>
    /// <param name="request">验证码请求.</param>
    /// <param name="normalizedUserName">标准化后的用户名.</param>
    /// <param name="userName">原始用户名.</param>
    /// <param name="userExists">用户是否存在.</param>
    /// <param name="code">验证码.</param>
    /// <returns>验证码发送结果.</returns>
    private async Task<IActionResult> PrivateRequestVerifyCodeAsync(PassportRequest request, string normalizedUserName, string userName, bool userExists, string code)
    {
        try
        {
            if (request.IsResetPassword)
            {
                if (!userExists)
                {
                    passportVerificationService.RemoveVerifyCode(normalizedUserName, PassportVerificationService.ResetPassword);
                    return Fail("Failed to request verification code", ReturnCode.VerifyCodeNotAllowed, ServerKeys.ServerPassportVerifyRequestUserNotExisted);
                }

                await mailService.SendResetPasswordVerifyCodeAsync(userName, code).ConfigureAwait(false);
                return Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
            }

            if (request.IsResetUserName)
            {
                if (!userExists)
                {
                    passportVerificationService.RemoveVerifyCode(normalizedUserName, PassportVerificationService.ResetUserName);
                    return Fail("Failed to request verification code", ReturnCode.VerifyCodeNotAllowed, ServerKeys.ServerPassportVerifyRequestUserNotExisted);
                }

                await mailService.SendResetUsernameVerifyCodeAsync(userName, code).ConfigureAwait(false);
                return Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
            }

            if (request.IsCancelRegistration)
            {
                if (!userExists)
                {
                    passportVerificationService.RemoveVerifyCode(normalizedUserName, PassportVerificationService.CancelRegistration);
                    return Fail("Failed to request verification code", ReturnCode.VerifyCodeNotAllowed, ServerKeys.ServerPassportVerifyRequestUserNotExisted);
                }

                await mailService.SendCancelRegistrationVerifyCodeAsync(userName, code).ConfigureAwait(false);
                return Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
            }

            if (userExists)
            {
                passportVerificationService.RemoveVerifyCode(normalizedUserName, PassportVerificationService.Registration);
                passportVerificationService.RemoveVerifyCode(normalizedUserName, PassportVerificationService.ResetUserNameNew);
                return Fail("Failed to request verification code", ReturnCode.VerifyCodeNotAllowed, ServerKeys.ServerPassportVerifyRequestUserAlreadyExisted);
            }

            if (request.IsResetUserNameNew)
            {
                await mailService.SendResetUsernameVerifyCodeAsync(userName, code).ConfigureAwait(false);
                return Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
            }

            await mailService.SendRegistrationVerifyCodeAsync(userName, code).ConfigureAwait(false);
            return Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
        }
        catch (ParseException)
        {
            return Fail("Failed to request verification code", ReturnCode.InvalidUserName, ServerKeys.ServerPassportVerifyRequestUserNameInvalid);
        }
    }
}
