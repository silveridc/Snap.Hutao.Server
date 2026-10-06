using MailKit.Net.Smtp;
using MimeKit;
using server.Models.Context;
using server.Models.Entity.Passport;
using server.Models.Passport;
using server.Models.Response;
using server.Services;
using server.Services.Authorization;

namespace server.Controllers
{
    [Obsolete]
    [ApiController]
    [Route("[controller]")]
    [ApiExplorerSettings(GroupName = "Passport")]
    public class PassportController(IServiceProvider serviceProvider) : ControllerBase
    {
        private readonly PassportVerificationService passportVerificationService = serviceProvider.GetRequiredService<PassportVerificationService>();
        private readonly AppDbContext appDbContext = serviceProvider.GetRequiredService<AppDbContext>();
        private readonly PassportService passportService = serviceProvider.GetRequiredService<PassportService>();
        private readonly MailService mailService = serviceProvider.GetRequiredService<MailService>();

        /// <summary>
        /// 申请邮箱验证码。
        /// </summary>
        /// <param name="request">包含用户名与场景标记的请求体。</param>
        /// <returns>验证码申请结果。</returns>
        [HttpPost("Verify")]
        public async Task<IActionResult> RequestVerifyCodeAsync([FromBody] PassportRequest request)
        {
            string normalizedUserName = passportService.DecryptNormalizedUserName(request, out string userName);
            string key = request switch
            {
                { IsResetPassword: true } => "ResetPassword",
                { IsResetUserName: true } => "ResetUsername",
                { IsResetUserNameNew: true } => "ResetUsernameNew",
                { IsCancelRegistration: true } => "CancelRegistration",
                _ => "Registration",
            };

            if (passportVerificationService.TryGetNonExpiredVerifyCode(normalizedUserName, key, out _))
            {
                return Models.Response.Response.Fail(ReturnCode.VerifyCodeTooFrequently, "Too many requests, please try again in 1 minute", ServerKeys.ServerPassportVerifyTooFrequent);
            }

            bool userExists = appDbContext.Users.Any(u => u.NormalizedUserName == normalizedUserName);
            string code = passportVerificationService.GenerateVerifyCode(normalizedUserName, key);

            try
            {
                return await PrivateRequestVerifyCodeAsync(request, normalizedUserName, userName, userExists, code).ConfigureAwait(false);
            }
            catch (SmtpCommandException)
            {
                return Models.Response.Response.Success("Failed to request verification code", ServerKeys.ServerPassportVerifyRequestFailed);
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
            string normalizedUserName = passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);

            if (!this.passportVerificationService.TryValidateVerifyCode(normalizedUserName, "Registration", code))
            {
                return Models.Response.Response.Fail(ReturnCode.RegisterFail, "Verification failed", ServerKeys.ServerPassportVerifyFailed);
            }

            Passport passport = new()
            {
                UserName = userName,
                Password = passportService.Decrypt(request.Password),
            };

            if (passport.Password.Length < 8)
            {
                return Models.Response.Response.Fail(ReturnCode.TooShortPassword, "Password must be at least 8 characters", ServerKeys.ServerPassportPasswordTooShort);
            }

            PassportResult result = await passportService.RegisterAsync(passport, default).ConfigureAwait(false);
            return result.Success
                ? Response<string>.Success(result.Message, result.LocalizationKey!, result.Token.AccessToken)
                : Models.Response.Response.Fail(ReturnCode.RegisterFail, result.Message, result.LocalizationKey!);
        }

        /// <summary>
        /// 注销账号。
        /// </summary>
        /// <param name="request">包含用户名、密码与验证码的请求体。</param>
        /// <returns>注销结果。</returns>
        [HttpPost("Cancel")]
        public async Task<IActionResult> CancelRegistrationAsync([FromBody] PassportRequest request)
        {
            string normalizedUserName = passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);

            if (!passportVerificationService.TryValidateVerifyCode(normalizedUserName, "CancelRegistration", code))
            {
                return Models.Response.Response.Fail(ReturnCode.RegisterFail, "Verification failed", ServerKeys.ServerPassportVerifyFailed);
            }

            Passport passport = new()
            {
                UserName = userName,
                Password = passportService.Decrypt(request.Password),
            };

            PassportResult result = await passportService.CancelAsync(passport).ConfigureAwait(false);

            return result.Success
                ? Models.Response.Response.Success(result.Message, result.LocalizationKey!)
                : Models.Response.Response.Fail(ReturnCode.CancelFail, result.Message, result.LocalizationKey!);
        }

        /// <summary>
        /// 重置密码。
        /// </summary>
        /// <param name="request">包含用户名、新密码与验证码的请求体。</param>
        /// <returns>重置结果。</returns>
        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPasswordAsync([FromBody] PassportRequest request)
        {
            string normalizedUserName = passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);

            if (!passportVerificationService.TryValidateVerifyCode(normalizedUserName, "ResetPassword", code))
            {
                return Models.Response.Response.Fail(ReturnCode.RegisterFail, "Verification failed", ServerKeys.ServerPassportVerifyFailed);
            }

            Passport passport = new()
            {
                UserName = userName,
                Password = passportService.Decrypt(request.Password),
            };

            if (passport.Password.Length < 8)
            {
                return Models.Response.Response.Fail(ReturnCode.TooShortPassword, "Password must be at least 8 characters", ServerKeys.ServerPassportPasswordTooShort);
            }

            PassportResult result = await passportService.ResetPasswordAsync(passport, default).ConfigureAwait(false);

            return result.Success
                ? Response<string>.Success(result.Message, result.LocalizationKey!, result.Token.AccessToken)
                : Models.Response.Response.Fail(ReturnCode.RegisterFail, result.Message, result.LocalizationKey!);
        }

        /// <summary>
        /// 重置用户名。
        /// </summary>
        /// <param name="request">包含原用户名、新用户名与验证码的请求体。</param>
        /// <returns>重置结果。</returns>
        [HttpPost("ResetUsername")]
        public async Task<IActionResult> ResetUsernameAsync([FromBody] PassportRequest request)
        {
            string normalizedUserName = passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);
            string newNormalizedUserName = passportService.DecryptNewNormalizedUserNameAndNewVerifyCode(request, out string newUserName, out string newCode);

            if (!passportVerificationService.TryValidateVerifyCode(normalizedUserName, "ResetUsername", code) || !passportVerificationService.TryValidateVerifyCode(newNormalizedUserName, "ResetUsernameNew", newCode))
            {
                return Models.Response.Response.Fail(ReturnCode.RegisterFail, "Verification failed", ServerKeys.ServerPassportVerifyFailed);
            }

            Passport passport = new()
            {
                UserName = userName,
                NewUserName = newUserName,
            };

            PassportResult result = await passportService.ResetUsernameAsync(passport, default).ConfigureAwait(false);

            return result.Success
                ? Response<string>.Success(result.Message, result.LocalizationKey!, result.Token.AccessToken)
                : Models.Response.Response.Fail(ReturnCode.RegisterFail, result.Message, result.LocalizationKey!);
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
                UserName = passportService.Decrypt(request.UserName),
                Password = passportService.Decrypt(request.Password),
            };

            PassportResult result = await passportService.LoginAsync(passport, default).ConfigureAwait(false);

            return result.Success
                ? Response<string>.Success(result.Message, result.LocalizationKey!, result.Token.AccessToken)
                : Models.Response.Response.Fail(ReturnCode.LoginFail, result.Message, result.LocalizationKey!);
        }

        /// <summary>
        /// 获取当前登录用户信息。
        /// </summary>
        /// <returns>用户信息。</returns>
        [Authorize]
        [HttpGet("UserInfo")]
        public async Task<IActionResult> GetUserInfoAsync()
        {
            HutaoUser? user = await this.GetUserAsync(appDbContext.Users).ConfigureAwait(false);

            if (user is null)
            {
                return Models.Response.Response.Fail(ReturnCode.UserNameNotExists, "User does not exist", ServerKeys.ServerPassportUserInfoNotExist);
            }

            return Response<UserInfo>.Success("User info acquired successfully", ServerKeys.ServerPassportUserInfoSucceed, new()
            {
                NormalizedUserName = user.NormalizedUserName ?? user.UserName,
                UserName = user.UserName,
                IsLicensedDeveloper = user.IsLicensedDeveloper,
                IsMaintainer = user.IsMaintainer,
                GachaLogExpireAt = DateTimeOffset.FromUnixTimeSeconds(user.GachaLogExpireAt),
                CdnExpireAt = DateTimeOffset.FromUnixTimeSeconds(user.CdnExpireAt),
            });
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
                        passportVerificationService.RemoveVerifyCode(normalizedUserName, "ResetPassword");
                        return Models.Response.Response.Fail(ReturnCode.VerifyCodeNotAllowed, "Failed to request verification code", ServerKeys.ServerPassportVerifyRequestUserNotExisted);
                    }

                    await mailService.SendResetPasswordVerifyCodeAsync(userName, code).ConfigureAwait(false);
                    return Models.Response.Response.Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
                }

                // 重置用户名
                if (request.IsResetUserName)
                {
                    if (!userExists)
                    {
                        passportVerificationService.RemoveVerifyCode(normalizedUserName, "ResetUsername");
                        return Models.Response.Response.Fail(ReturnCode.VerifyCodeNotAllowed, "Failed to request verification code", ServerKeys.ServerPassportVerifyRequestUserNotExisted);
                    }

                    await mailService.SendResetUsernameVerifyCodeAsync(userName, code).ConfigureAwait(false);
                    return Models.Response.Response.Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
                }

                // 注销账号
                if (request.IsCancelRegistration)
                {
                    // 注销时特判，只能注销当前用户
                    HutaoUser? user = await this.GetUserAsync(appDbContext.Users).ConfigureAwait(false);
                    if (user is null || !string.Equals(normalizedUserName, user.NormalizedUserName, StringComparison.Ordinal))
                    {
                        passportVerificationService.RemoveVerifyCode(normalizedUserName, "CancelRegistration");
                        return Models.Response.Response.Fail(ReturnCode.VerifyCodeNotAllowed, "Failed to request verification code", ServerKeys.ServerPassportVerifyRequestNotCurrentUser);
                    }

                    await mailService.SendCancelRegistrationVerifyCodeAsync(userName, code).ConfigureAwait(false);
                    return Models.Response.Response.Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
                }

                // 注册账号，用户名已存在
                if (userExists)
                {
                    passportVerificationService.RemoveVerifyCode(normalizedUserName, "Registration");
                    passportVerificationService.RemoveVerifyCode(normalizedUserName, "ResetUsernameNew");
                    return Models.Response.Response.Fail(ReturnCode.VerifyCodeNotAllowed, "Failed to request verification code", ServerKeys.ServerPassportVerifyRequestUserAlreadyExisted);
                }

                if (request.IsResetUserNameNew)
                {
                    await mailService.SendResetUsernameVerifyCodeAsync(userName, code).ConfigureAwait(false);
                    return Models.Response.Response.Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
                }

                // 默认注册
                await mailService.SendRegistrationVerifyCodeAsync(userName, code).ConfigureAwait(false);
                return Models.Response.Response.Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
            }
            catch (ParseException)
            {
                return Models.Response.Response.Fail(ReturnCode.InvalidUserName, "Failed to request verification code", ServerKeys.ServerPassportVerifyRequestUserNameInvalid);
            }
        }
    }
}