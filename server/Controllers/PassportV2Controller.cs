using MailKit.Net.Smtp;
using MimeKit;
using server.Models.Admin;
using server.Models.Passport;
using server.Models.ReCaptcha;
using server.Services.Admin;
using server.Services.Mail;

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
    ReCaptchaService reCaptchaService) : ControllerBase
{
    /// <summary>
    /// Requests an email verification code for registration or account security operations.
    /// Password recovery is handled by sending a time-limited reset link instead.
    /// </summary>
    /// <param name="request">The request body containing the user name and the scenario flags.</param>
    /// <returns>The verification code or reset link request result.</returns>
    [HttpPost("verification-codes")]
    public async Task<IActionResult> RequestVerifyCodeAsync([FromBody] PassportRequest request)
    {
        string normalizedUserName = passportService.DecryptNormalizedUserName(request, out string userName);

        try
        {
            if (request.IsResetPassword)
            {
                return await RequestResetPasswordLinkAsync(normalizedUserName, userName).ConfigureAwait(false);
            }

            string key = request switch
            {
                { IsResetUserName: true } => PassportVerificationService.ResetUserName,
                { IsResetUserNameNew: true } => PassportVerificationService.ResetUserNameNew,
                { IsCancelRegistration: true } => PassportVerificationService.CancelRegistration,
                _ => "Registration",
            };

            if (passportVerificationService.TryGetNonExpiredVerifyCode(normalizedUserName, key, out _))
            {
                int cooldownSeconds = passportVerificationService.GetVerifyCodeResendCooldownSeconds(normalizedUserName, key);
                return Fail($"Too many requests, please try again in {cooldownSeconds} seconds", ReturnCode.VerifyCodeTooFrequently);
            }

            bool userExists = appDbContext.Users.Any(u => u.NormalizedUserName == normalizedUserName);
            string code = passportVerificationService.GenerateVerifyCode(normalizedUserName, key);
            return await PrivateRequestVerifyCodeAsync(request, normalizedUserName, userName, userExists, code).ConfigureAwait(false);
        }
        catch (SmtpCommandException)
        {
            return Success("Failed to request verification code", ServerKeys.ServerPassportVerifyRequestFailed);
        }
        catch (ParseException)
        {
            return Fail("Failed to request verification code", ReturnCode.InvalidUserName, ServerKeys.ServerPassportVerifyRequestUserNameInvalid);
        }
    }

    /// <summary>
    /// Registers an account with an email verification code and issues the access token.
    /// </summary>
    /// <param name="request">The request body containing the user name, the password (at least 8 characters), and the verification code.</param>
    /// <returns>The registration result.</returns>
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
    /// Cancels the current account and deletes the related data. This operation is irreversible.
    /// </summary>
    /// <param name="request">The request body containing the user name, the password, and the verification code.</param>
    /// <returns>The cancellation result.</returns>
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
    /// Gets the state of a password reset link, returning the masked email and the remaining validity
    /// for the landing page to display.
    /// </summary>
    /// <param name="token">The one-time token carried by the link.</param>
    /// <returns>The link state.</returns>
    [HttpGet("password/reset/{token}")]
    public IActionResult GetResetPasswordLinkAsync([FromRoute] string token)
    {
        if (!passportVerificationService.TryPeekResetToken(token, out string userName, out TimeSpan? remaining))
        {
            return Fail("Verification failed", ReturnCode.RegisterFail, ServerKeys.ServerPassportVerifyFailed);
        }

        PassportResetLinkState state = new()
        {
            UserName = MaskEmail(userName),
            ExpireInSeconds = (int)Math.Max(0, Math.Ceiling((remaining ?? TimeSpan.Zero).TotalSeconds)),
        };

        return Success<PassportResetLinkState>("Reset link is valid", state, ServerKeys.ServerPassportVerifyRequestSuccess);
    }

    /// <summary>
    /// Sets a new password using a reset link; the link is invalidated immediately after use.
    /// </summary>
    /// <param name="request">The request body containing the link token and the new password (at least 8 characters).</param>
    /// <returns>The reset result.</returns>
    [HttpPut("password/reset")]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] PassportRequest request)
    {
        string password = passportService.Decrypt(request.Password);

        if (password.Length < 8)
        {
            return Fail("Password must be at least 8 characters", ReturnCode.TooShortPassword, ServerKeys.ServerPassportPasswordTooShort);
        }

        if (!passportVerificationService.TryConsumeResetToken(passportService.Decrypt(request.Token), out string userName))
        {
            return Fail("Verification failed", ReturnCode.RegisterFail, ServerKeys.ServerPassportVerifyFailed);
        }

        Passport passport = new()
        {
            UserName = userName,
            Password = password,
        };

        // 落地页在浏览器里打开，没有设备头，按无设备下发令牌
        PassportResult result = await passportService.ResetPasswordAsync(passport, deviceInfo: null).ConfigureAwait(false);

        return result.Success
            ? Success(result.Message, result.LocalizationKey!)
            : Fail(result.Message, ReturnCode.RegisterFail, result.LocalizationKey!);
    }

    /// <summary>
    /// Changes the bound email address and issues a new access token.
    /// </summary>
    /// <param name="request">The request body containing the current email, the new email, and their verification codes.</param>
    /// <returns>The reset result.</returns>
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
    /// Gets the human verification configuration for the sign-in page; the site key is a public value.
    /// </summary>
    /// <returns>Whether captcha is enabled and the reCAPTCHA site key.</returns>
    [HttpGet("captcha")]
    public IActionResult GetCaptchaSettings()
    {
        bool enabled = systemConfigService.CaptchaEnabled;
        return Success<PassportCaptchaSettings>("Captcha settings acquired successfully", new()
        {
            Enabled = enabled,
            SiteKey = enabled ? systemConfigService.RecaptchaSiteKey : null,
        });
    }

    /// <summary>
    /// Serves the reCAPTCHA page used by the launcher's embedded browser: it executes v3 automatically after loading
    /// and redirects back to the sentinel callback address.
    /// </summary>
    /// <returns>The HTML page.</returns>
    [HttpGet("captcha/page")]
    public IActionResult GetCaptchaPage()
    {
        string siteKey = systemConfigService.RecaptchaSiteKey;
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
    /// Signs in with the email and password and issues an access token and a refresh token.
    /// </summary>
    /// <param name="request">The request body containing the user name, the password, and the captcha token.</param>
    /// <returns>The sign-in result.</returns>
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

            if (captcha is not { Success: true, Action: "login" } || captcha.Score < systemConfigService.RecaptchaMinScore)
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
    /// Gets a page of the current account's login logs so users can check for unexpected sign-ins.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>The login records, including the IP region.</returns>
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
    /// Gets the basic profile and resource expiry times of the currently signed-in user.
    /// </summary>
    /// <returns>The user information.</returns>
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
    /// Gets all currently valid sign-in devices of the account.
    /// </summary>
    /// <returns>The signed-in device list.</returns>
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
    /// Exchanges a refresh token for a new access token.
    /// </summary>
    /// <param name="request">The request body containing the refresh token.</param>
    /// <returns>The new token.</returns>
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
    /// Revokes refresh tokens: with a device ID only that device is revoked; without one every device of the account is revoked.
    /// </summary>
    /// <param name="deviceId">The encrypted device ID.</param>
    /// <returns>The revoke result.</returns>
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
    /// Masks the email address so accounts cannot be enumerated through the link state endpoint.
    /// </summary>
    /// <param name="email">The original email address.</param>
    /// <returns>A display value such as ab***@example.com.</returns>
    private static string MaskEmail(string email)
    {
        int at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0)
        {
            return $"{email[..Math.Min(email.Length, 2)]}***";
        }

        string local = email[..at];
        string prefix = local.Length <= 2 ? local[..1] : local[..2];
        return $"{prefix}***{email[at..]}";
    }

    /// <summary>
    /// Sends the verification code email for the requested scenario.
    /// </summary>
    /// <param name="request">The verification code request.</param>
    /// <param name="normalizedUserName">The normalized user name.</param>
    /// <param name="userName">The original user name.</param>
    /// <param name="userExists">Whether the user exists.</param>
    /// <param name="code">The verification code.</param>
    /// <returns>The verification code delivery result.</returns>
    private async Task<IActionResult> PrivateRequestVerifyCodeAsync(PassportRequest request, string normalizedUserName, string userName, bool userExists, string code)
    {
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

    /// <summary>
    /// Issues a password reset link and sends it by email, throttled per email address by the configured cooldown.
    /// </summary>
    /// <param name="normalizedUserName">The normalized email address.</param>
    /// <param name="userName">The original email address.</param>
    /// <returns>The link delivery result.</returns>
    private async Task<IActionResult> RequestResetPasswordLinkAsync(string normalizedUserName, string userName)
    {
        if (!appDbContext.Users.Any(u => u.NormalizedUserName == normalizedUserName))
        {
            return Fail("Failed to request verification code", ReturnCode.VerifyCodeNotAllowed, ServerKeys.ServerPassportVerifyRequestUserNotExisted);
        }

        if (!passportVerificationService.TryGenerateResetToken(normalizedUserName, userName, out string token))
        {
            int cooldownSeconds = passportVerificationService.GetResetTokenResendCooldownSeconds(normalizedUserName);
            return Fail($"Too many requests, please try again in {cooldownSeconds} seconds", ReturnCode.VerifyCodeTooFrequently);
        }

        await mailService.SendResetPasswordLinkAsync(userName, token).ConfigureAwait(false);
        return Success("Reset link sent successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
    }
}
