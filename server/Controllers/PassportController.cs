using MailKit.Net.Smtp;
using MimeKit;
using server.Models.Passport;

namespace server.Controllers;

[Obsolete]
[ApiController]
[Route("api/[controller]")]
[ApiExplorerSettings(GroupName = "Passport")]
public class PassportController(IServiceProvider serviceProvider) : ControllerBase
{
    private readonly PassportVerificationService _passportVerificationService = serviceProvider.GetRequiredService<PassportVerificationService>();
    private readonly AppDbContext _appDbContext = serviceProvider.GetRequiredService<AppDbContext>();
    private readonly PassportService _passportService = serviceProvider.GetRequiredService<PassportService>();
    private readonly MailService _mailService = serviceProvider.GetRequiredService<MailService>();

    /// <summary>
    /// 申请邮箱验证码。
    /// </summary>
    /// <param name="request">包含用户名与场景标记的请求体。</param>
    /// <returns>验证码申请结果。</returns>
    [HttpPost("Verify")]
    public async Task<IActionResult> RequestVerifyCodeAsync([FromBody] PassportRequest request)
    {
        string normalizedUserName = _passportService.DecryptNormalizedUserName(request, out string userName);
        string key = request switch
        {
            { IsResetPassword: true } => "ResetPassword",
            { IsResetUserName: true } => "ResetUsername",
            { IsResetUserNameNew: true } => "ResetUsernameNew",
            { IsCancelRegistration: true } => "CancelRegistration",
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
    /// 注册账号。
    /// </summary>
    /// <param name="request">包含用户名、密码与验证码的请求体。</param>
    /// <returns>注册结果。</returns>
    [HttpPost("Register")]
    public async Task<IActionResult> RegisterAsync([FromBody] PassportRequest request)
    {
        string normalizedUserName = _passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);

        if (!_passportVerificationService.TryValidateVerifyCode(normalizedUserName, "Registration", code))
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

        PassportResult result = await _passportService.RegisterAsync(passport, default).ConfigureAwait(false);
        return result.Success
            ? Success<string>(result.Message, result.Token.AccessToken, result.LocalizationKey!)
            : Fail(result.Message, ReturnCode.RegisterFail, result.LocalizationKey!);
    }

    /// <summary>
    /// 注销账号。
    /// </summary>
    /// <param name="request">包含用户名、密码与验证码的请求体。</param>
    /// <returns>注销结果。</returns>
    [HttpPost("Cancel")]
    public async Task<IActionResult> CancelRegistrationAsync([FromBody] PassportRequest request)
    {
        string normalizedUserName = _passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);

        if (!_passportVerificationService.TryValidateVerifyCode(normalizedUserName, "CancelRegistration", code))
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
    /// 重置密码。
    /// </summary>
    /// <param name="request">包含用户名、新密码与验证码的请求体。</param>
    /// <returns>重置结果。</returns>
    [HttpPost("ResetPassword")]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] PassportRequest request)
    {
        string normalizedUserName = _passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);

        if (!_passportVerificationService.TryValidateVerifyCode(normalizedUserName, "ResetPassword", code))
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

        PassportResult result = await _passportService.ResetPasswordAsync(passport, default).ConfigureAwait(false);

        return result.Success
            ? Success<string>(result.Message, result.Token.AccessToken, result.LocalizationKey!)
            : Fail(result.Message, ReturnCode.RegisterFail, result.LocalizationKey!);
    }

    /// <summary>
    /// 重置用户名。
    /// </summary>
    /// <param name="request">包含原用户名、新用户名与验证码的请求体。</param>
    /// <returns>重置结果。</returns>
    [HttpPost("ResetUsername")]
    public async Task<IActionResult> ResetUsernameAsync([FromBody] PassportRequest request)
    {
        string normalizedUserName = _passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);
        string newNormalizedUserName = _passportService.DecryptNewNormalizedUserNameAndNewVerifyCode(request, out string newUserName, out string newCode);

        if (!_passportVerificationService.TryValidateVerifyCode(normalizedUserName, "ResetUsername", code) || !_passportVerificationService.TryValidateVerifyCode(newNormalizedUserName, "ResetUsernameNew", newCode))
        {
            return Fail("Verification failed", ReturnCode.RegisterFail, ServerKeys.ServerPassportVerifyFailed);
        }

        Passport passport = new()
        {
            UserName = userName,
            NewUserName = newUserName,
        };

        PassportResult result = await _passportService.ResetUsernameAsync(passport, default).ConfigureAwait(false);

        return result.Success
            ? Success<string>(result.Message, result.Token.AccessToken, result.LocalizationKey!)
            : Fail(result.Message, ReturnCode.RegisterFail, result.LocalizationKey!);
    }

    /// <summary>
    /// 登录账号。
    /// </summary>
    /// <param name="request">包含用户名与密码的请求体。</param>
    /// <returns>登录结果。</returns>
    [HttpPost("Login")]
    public async Task<IActionResult> LoginAsync([FromBody] PassportRequest request)
    {
        Passport passport = new()
        {
            UserName = _passportService.Decrypt(request.UserName),
            Password = _passportService.Decrypt(request.Password),
        };

        PassportResult result = await _passportService.LoginAsync(passport, default).ConfigureAwait(false);

        return result.Success
            ? Success<string>(result.Message, result.Token.AccessToken, result.LocalizationKey!)
            : Fail(result.Message, ReturnCode.LoginFail, result.LocalizationKey!);
    }

    /// <summary>
    /// 获取当前登录用户信息。
    /// </summary>
    /// <returns>用户信息。</returns>
    [Authorize]
    [HttpGet("UserInfo")]
    public async Task<IActionResult> GetUserInfoAsync()
    {
        HutaoUser? user = await this.GetUserAsync(_appDbContext.Users).ConfigureAwait(false);

        if (user is null)
        {
            return Fail("User does not exist", ReturnCode.UserNameNotExists, ServerKeys.ServerPassportUserInfoNotExist);
        }

        return Success<UserInfo>(
            "User info acquired successfully",
            new()
            {
                NormalizedUserName = user.NormalizedUserName ?? user.UserName,
                UserName = user.UserName,
                IsLicensedDeveloper = user.IsLicensedDeveloper,
                IsMaintainer = user.IsMaintainer,
                GachaLogExpireAt = DateTimeOffset.FromUnixTimeSeconds(user.GachaLogExpireAt),
                CdnExpireAt = DateTimeOffset.FromUnixTimeSeconds(user.CdnExpireAt),
            },
            ServerKeys.ServerPassportUserInfoSucceed);
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
            // 重置密码
            if (request.IsResetPassword)
            {
                if (!userExists)
                {
                    _passportVerificationService.RemoveVerifyCode(normalizedUserName, "ResetPassword");
                    return Fail("Failed to request verification code", ReturnCode.VerifyCodeNotAllowed, ServerKeys.ServerPassportVerifyRequestUserNotExisted);
                }

                await _mailService.SendResetPasswordVerifyCodeAsync(userName, code).ConfigureAwait(false);
                return Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
            }

            // 重置用户名
            if (request.IsResetUserName)
            {
                if (!userExists)
                {
                    _passportVerificationService.RemoveVerifyCode(normalizedUserName, "ResetUsername");
                    return Fail("Failed to request verification code", ReturnCode.VerifyCodeNotAllowed, ServerKeys.ServerPassportVerifyRequestUserNotExisted);
                }

                await _mailService.SendResetUsernameVerifyCodeAsync(userName, code).ConfigureAwait(false);
                return Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
            }

            // 注销账号
            if (request.IsCancelRegistration)
            {
                // 注销时特判，只能注销当前用户
                HutaoUser? user = await this.GetUserAsync(_appDbContext.Users).ConfigureAwait(false);
                if (user is null || !string.Equals(normalizedUserName, user.NormalizedUserName, StringComparison.Ordinal))
                {
                    _passportVerificationService.RemoveVerifyCode(normalizedUserName, "CancelRegistration");
                    return Fail("Failed to request verification code", ReturnCode.VerifyCodeNotAllowed, ServerKeys.ServerPassportVerifyRequestNotCurrentUser);
                }

                await _mailService.SendCancelRegistrationVerifyCodeAsync(userName, code).ConfigureAwait(false);
                return Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
            }

            // 注册账号，用户名已存在
            if (userExists)
            {
                _passportVerificationService.RemoveVerifyCode(normalizedUserName, "Registration");
                _passportVerificationService.RemoveVerifyCode(normalizedUserName, "ResetUsernameNew");
                return Fail("Failed to request verification code", ReturnCode.VerifyCodeNotAllowed, ServerKeys.ServerPassportVerifyRequestUserAlreadyExisted);
            }

            if (request.IsResetUserNameNew)
            {
                await _mailService.SendResetUsernameVerifyCodeAsync(userName, code).ConfigureAwait(false);
                return Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
            }

            // 默认注册
            await _mailService.SendRegistrationVerifyCodeAsync(userName, code).ConfigureAwait(false);
            return Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
        }
        catch (ParseException)
        {
            return Fail("Failed to request verification code", ReturnCode.InvalidUserName, ServerKeys.ServerPassportVerifyRequestUserNameInvalid);
        }
    }
}
