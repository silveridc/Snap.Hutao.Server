using MailKit.Net.Smtp;
using MimeKit;
using server.Models.Passport;
using Swashbuckle.AspNetCore.Annotations;

namespace server.Controllers
{
    [ApiController]
    [Route("Passport/v2")]
    [ApiExplorerSettings(GroupName = "Passport")]
    public class PassportV2Controller(IServiceProvider serviceProvider) : ControllerBase
    {
        private readonly PassportVerificationService passportVerificationService = serviceProvider.GetRequiredService<PassportVerificationService>();
        private readonly AppDbContext appDbContext = serviceProvider.GetRequiredService<AppDbContext>();
        private readonly PassportService passportService = serviceProvider.GetRequiredService<PassportService>();
        private readonly MailService mailService = serviceProvider.GetRequiredService<MailService>();

        /// <summary>
        /// 申请邮箱验证码以完成注册、找回密码或账号安全操作。
        /// </summary>
        /// <param name="request">包含用户名与场景标记的请求体。</param>
        /// <returns>验证码申请结果。</returns>
        [SwaggerOperation(
            Summary = "申请邮箱验证码以完成注册、找回密码或账号安全操作。",
            Description = """
            请求体字段：
            - UserName：申请验证码所对应的邮箱账号。
            - IsResetPassword/IsResetUserName/IsResetUserNameNew/IsCancelRegistration：根据场景设置对应标记，其余保持 false。

            注意事项：
            - 同一邮箱同一类型验证码在 60 秒内只能申请一次，请在客户端进行节流处理。
            - 若需要重置用户名，请分别提交旧邮箱与新邮箱两次请求以获取成对验证码。
            """)]
        [HttpPost("Verify")]
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
        /// 使用邮箱验证码注册账号并下发访问令牌。
        /// </summary>
        /// <param name="request">包含用户名、密码与验证码的请求体。</param>
        /// <returns>注册结果。</returns>
        [SwaggerOperation(
            Summary = "使用邮箱验证码注册 Snap Hutao 账号并下发一组访问令牌。",
            Description = """
            请求体字段：
            - UserName：待注册的邮箱账号。
            - Password：新密码，至少 8 位。
            - VerifyCode：通过“申请验证码”接口获取的 6 位纯数字验证码。

            注意事项：
            - 请在客户端妥善保存返回的访问令牌与刷新令牌，以便后续调用需要鉴权的接口。
            - 若验证码已失效或被重复使用，将返回 RegisterFail 错误码。
            """)]
        [HttpPost("Register")]
        public async Task<IActionResult> RegisterAsync([FromBody] PassportRequest request)
        {
            DeviceInfo deviceInfo = this.GetDeviceInfo();
            string normalizedUserName = passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);

            if (!this.passportVerificationService.TryValidateVerifyCode(normalizedUserName, PassportVerificationService.Registration, code))
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

            PassportResult result = await passportService.RegisterAsync(passport, deviceInfo).ConfigureAwait(false);
            return result.Success
                ? Response<TokenResponse>.Success(result.Message, result.LocalizationKey!, result.Token)
                : Models.Response.Response.Fail(ReturnCode.RegisterFail, result.Message, result.LocalizationKey!);
        }

        /// <summary>
        /// 注销现有账号并删除关联数据。
        /// </summary>
        /// <param name="request">包含用户名、密码与验证码的请求体。</param>
        /// <returns>注销结果。</returns>
        [SwaggerOperation(
            Summary = "注销现有账号并删除关联数据。",
            Description = """
            请求体字段：
            - UserName：待注销账号绑定的邮箱。
            - Password：当前登录密码。
            - VerifyCode：申请验证码时设置 IsCancelRegistration 标记后收到的验证码。

            注意事项：
            - 注销操作不可逆，请在客户端做好二次确认。
            - 验证码必须由当前登录用户申请，以避免误操作风险。
            """)]
        [HttpPost("Cancel")]
        public async Task<IActionResult> CancelRegistrationAsync([FromBody] PassportRequest request)
        {
            string normalizedUserName = passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);

            if (!passportVerificationService.TryValidateVerifyCode(normalizedUserName, PassportVerificationService.CancelRegistration, code))
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
        /// 通过邮箱验证码设置新的登录密码。
        /// </summary>
        /// <param name="request">包含用户名、新密码与验证码的请求体。</param>
        /// <returns>重置结果。</returns>
        [SwaggerOperation(
            Summary = "通过邮箱验证码设置新的登录密码。",
            Description = """
            请求体字段：
            - UserName：需要重置密码的邮箱账号。
            - Password：新的登录密码，必须与旧密码不同。
            - VerifyCode：申请验证码时设置 IsResetPassword 标记后收到的验证码。

            注意事项：
            - 操作成功后会重新签发访问令牌，请同步更新客户端持有的令牌。
            """)]
        [HttpPost("ResetPassword")]
        public async Task<IActionResult> ResetPasswordAsync([FromBody] PassportRequest request)
        {
            DeviceInfo deviceInfo = this.GetDeviceInfo();
            string normalizedUserName = passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);

            if (!passportVerificationService.TryValidateVerifyCode(normalizedUserName, PassportVerificationService.ResetPassword, code))
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

            PassportResult result = await passportService.ResetPasswordAsync(passport, deviceInfo).ConfigureAwait(false);

            return result.Success
                ? Response<TokenResponse>.Success(result.Message, result.LocalizationKey!, result.Token)
                : Models.Response.Response.Fail(ReturnCode.RegisterFail, result.Message, result.LocalizationKey!);
        }

        /// <summary>
        /// 修改绑定邮箱并重新签发访问令牌。
        /// </summary>
        /// <param name="request">包含原邮箱、新邮箱及对应验证码的请求体。</param>
        /// <returns>重置结果。</returns>
        [SwaggerOperation(
            Summary = "修改绑定邮箱并重新签发访问令牌。",
            Description = """
            请求体字段：
            - UserName 与 VerifyCode：原邮箱及其验证码。
            - NewUserName 与 NewVerifyCode：新邮箱及其验证码。

            注意事项：
            - 请分别为旧邮箱与新邮箱申请验证码，并在同一次请求中提交。
            - 成功后将返回新的访问令牌与刷新令牌，请替换客户端本地缓存。
            """)]
        [HttpPost("ResetUsername")]
        public async Task<IActionResult> ResetUsernameAsync([FromBody] PassportRequest request)
        {
            DeviceInfo deviceInfo = this.GetDeviceInfo();
            string normalizedUserName = passportService.DecryptNormalizedUserNameAndVerifyCode(request, out string userName, out string code);
            string newNormalizedUserName = passportService.DecryptNewNormalizedUserNameAndNewVerifyCode(request, out string newUserName, out string newCode);

            if (!passportVerificationService.TryValidateVerifyCode(normalizedUserName, PassportVerificationService.ResetUserName, code) ||
                !passportVerificationService.TryValidateVerifyCode(newNormalizedUserName, PassportVerificationService.ResetUserNameNew, newCode))
            {
                return Models.Response.Response.Fail(ReturnCode.RegisterFail, "Verification failed", ServerKeys.ServerPassportVerifyFailed);
            }

            Passport passport = new()
            {
                UserName = userName,
                NewUserName = newUserName,
            };

            PassportResult result = await passportService.ResetUsernameAsync(passport, deviceInfo).ConfigureAwait(false);

            return result.Success
                ? Response<TokenResponse>.Success(result.Message, result.LocalizationKey!, result.Token)
                : Models.Response.Response.Fail(ReturnCode.RegisterFail, result.Message, result.LocalizationKey!);
        }

        /// <summary>
        /// 使用邮箱与密码登录并颁发访问令牌与刷新令牌。
        /// </summary>
        /// <param name="request">包含用户名与密码的请求体。</param>
        /// <returns>登录结果。</returns>
        [SwaggerOperation(
            Summary = "使用邮箱与密码登录并颁发访问令牌与刷新令牌。",
            Description = """
            请求体字段：
            - UserName：登录所使用的邮箱账号。
            - Password：账号密码。

            注意事项：
            - 登录成功后返回访问令牌与刷新令牌，请在客户端安全保存。
            - 若账号存在安全风险，可结合设备管理接口执行强制登出。
            """)]
        [HttpPost("Login")]
        public async Task<IActionResult> LoginAsync([FromBody] PassportRequest request)
        {
            DeviceInfo deviceInfo = this.GetDeviceInfo();
            Passport passport = new()
            {
                UserName = passportService.Decrypt(request.UserName),
                Password = passportService.Decrypt(request.Password),
            };

            PassportResult result = await passportService.LoginAsync(passport, deviceInfo).ConfigureAwait(false);

            return result.Success
                ? Response<TokenResponse>.Success(result.Message, result.LocalizationKey!, result.Token)
                : Models.Response.Response.Fail(ReturnCode.LoginFail, result.Message, result.LocalizationKey!);
        }

        /// <summary>
        /// 获取当前登录用户的基础资料与资源到期时间。
        /// </summary>
        /// <returns>用户信息。</returns>
        [SwaggerOperation(
            Summary = "获取当前登录用户的基础资料与资源到期时间。",
            Description = """
            注意事项：
            - 需携带有效的 Bearer Token 才能访问。
            - 可用于刷新客户端缓存的账号信息与服务到期时间。
            """)]
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
        /// 查询当前账号所有有效的登录设备。
        /// </summary>
        /// <returns>已登录设备列表。</returns>
        [SwaggerOperation(
            Summary = "查询当前账号所有有效的登录设备。",
            Description = """
            注意事项：
            - 需携带有效的 Bearer Token 才能访问。
            - 返回数据包含设备标识、名称、系统类型与登录时间，可据此实现设备管理。
            """)]
        [Authorize]
        [HttpGet("LoggedInDevices")]
        public async Task<IActionResult> GetLoggedInDevicesAsync()
        {
            DeviceInfo deviceInfo = this.GetDeviceInfo();
            int userId = this.GetUserId();
            List<LoggedInDeviceInfo> devices = await passportService.GetLoggedInDevicesAsync(userId, deviceInfo).ConfigureAwait(false);
            return Response<List<LoggedInDeviceInfo>>.Success("Logged-in devices acquired successfully", ServerKeys.ServerPassportLoggedInDevicesSucceed, devices);
        }

        /// <summary>
        /// 使用刷新令牌换取新的访问令牌。
        /// </summary>
        /// <param name="request">包含刷新令牌的请求体。</param>
        /// <returns>新的令牌。</returns>
        [SwaggerOperation(
            Summary = "使用刷新令牌换取新的访问令牌。",
            Description = """
            请求体字段：
            - RefreshToken：刷新令牌。

            注意事项：
            - 刷新接口无需登录态，但请求仍需走安全信道。
            - 若刷新令牌无效或已过期，将返回对应错误码。
            """)]
        [HttpPost("RefreshToken")]
        public async Task<IActionResult> RefreshTokenAsync([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrEmpty(request.RefreshToken))
            {
                return Models.Response.Response.Fail(ReturnCode.InvalidRequestBody, "Refresh token must not be empty", ServerKeys.ServerPassportRefreshTokenEmpty);
            }

            TokenResponse? tokenResponse = await passportService.RefreshTokenAsync(passportService.Decrypt(request.RefreshToken));
            if (tokenResponse is null)
            {
                return Models.Response.Response.Fail(ReturnCode.LoginFail, "Refresh token is invalid or expired", ServerKeys.ServerPassportRefreshTokenInvalid);
            }

            return Response<TokenResponse>.Success("Token refreshed successfully", ServerKeys.ServerPassportTokenRefreshSucceed, tokenResponse);
        }

        /// <summary>
        /// 撤销指定设备的刷新令牌。
        /// </summary>
        /// <param name="request">包含设备 ID 的请求体。</param>
        /// <returns>撤销结果。</returns>
        [SwaggerOperation(
            Summary = "撤销指定设备的刷新令牌。",
            Description = """
            请求体字段：
            - DeviceId：设备唯一标识，可从“查询已登录设备”接口获取。

            注意事项：
            - 需携带有效的 Bearer Token，并仅能操作当前账号的设备。
            - 撤销后该设备将失去刷新访问令牌的能力。
            """)]
        [Authorize]
        [HttpPost("RevokeToken")]
        public async Task<IActionResult> RevokeTokenAsync([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrEmpty(request.DeviceId))
            {
                return Models.Response.Response.Fail(ReturnCode.InvalidRequestBody, "Device ID must not be empty", ServerKeys.ServerPassportDeviceIdEmpty);
            }

            return await passportService.RevokeRefreshTokenAsync(passportService.Decrypt(request.DeviceId)).ConfigureAwait(false)
                ? Models.Response.Response.Success("Token revoked successfully", ServerKeys.ServerPassportTokenRevokeSuccess)
                : Models.Response.Response.Fail(ReturnCode.RefreshTokenDbException, "Failed to revoke token", ServerKeys.ServerPassportTokenRevokeFailed);
        }

        /// <summary>
        /// 撤销当前账号所有刷新令牌并强制登出所有设备。
        /// </summary>
        /// <returns>撤销结果。</returns>
        [SwaggerOperation(
            Summary = "撤销当前账号所有刷新令牌并强制登出所有设备。",
            Description = """
            注意事项：
            - 需携带有效的 Bearer Token 才能访问。
            - 可在账号异常时执行，所有设备将被要求重新登录。
            """)]
        [Authorize]
        [HttpPost("RevokeAllTokens")]
        public async Task<IActionResult> RevokeAllTokensAsync()
        {
            int userId = this.GetUserId();
            await passportService.RevokeAllUserTokensAsync(userId);
            return Models.Response.Response.Success("All tokens have been revoked", ServerKeys.ServerPassportRevokeAllTokensSucceed);
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
                        passportVerificationService.RemoveVerifyCode(normalizedUserName, PassportVerificationService.ResetPassword);
                        return Models.Response.Response.Fail(ReturnCode.VerifyCodeNotAllowed, "Failed to request verification code", ServerKeys.ServerPassportVerifyRequestUserNotExisted);
                    }

                    await mailService.SendResetPasswordVerifyCodeAsync(userName, code).ConfigureAwait(false);
                    return Models.Response.Response.Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
                }

                if (request.IsResetUserName)
                {
                    if (!userExists)
                    {
                        passportVerificationService.RemoveVerifyCode(normalizedUserName, PassportVerificationService.ResetUserName);
                        return Models.Response.Response.Fail(ReturnCode.VerifyCodeNotAllowed, "Failed to request verification code", ServerKeys.ServerPassportVerifyRequestUserNotExisted);
                    }

                    await mailService.SendResetUsernameVerifyCodeAsync(userName, code).ConfigureAwait(false);
                    return Models.Response.Response.Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
                }

                if (request.IsCancelRegistration)
                {
                    if (!userExists)
                    {
                        passportVerificationService.RemoveVerifyCode(normalizedUserName, PassportVerificationService.CancelRegistration);
                        return Models.Response.Response.Fail(ReturnCode.VerifyCodeNotAllowed, "Failed to request verification code", ServerKeys.ServerPassportVerifyRequestUserNotExisted);
                    }

                    await mailService.SendCancelRegistrationVerifyCodeAsync(userName, code).ConfigureAwait(false);
                    return Models.Response.Response.Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
                }

                if (userExists)
                {
                    passportVerificationService.RemoveVerifyCode(normalizedUserName, PassportVerificationService.Registration);
                    passportVerificationService.RemoveVerifyCode(normalizedUserName, PassportVerificationService.ResetUserNameNew);
                    return Models.Response.Response.Fail(ReturnCode.VerifyCodeNotAllowed, "Failed to request verification code", ServerKeys.ServerPassportVerifyRequestUserAlreadyExisted);
                }

                if (request.IsResetUserNameNew)
                {
                    await mailService.SendResetUsernameVerifyCodeAsync(userName, code).ConfigureAwait(false);
                    return Models.Response.Response.Success("Verification code requested successfully", ServerKeys.ServerPassportVerifyRequestSuccess);
                }

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