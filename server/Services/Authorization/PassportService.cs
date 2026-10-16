using server.Extension;
using server.Models.Passport;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace server.Services.Authorization
{
    // Scoped
    public sealed class PassportService(UserManager<HutaoUser> userManager, AppDbContext appDbContext, AppOptions appOptions)
    {
        private const int LegacyAccessTokenExpirationMinutes = 3 * 60;
        private const int AccessTokenExpirationMinutes = 15;
        private const int RefreshTokenExpirationDays = 30;

        private readonly SymmetricSecurityKey jwtSigningKey = appOptions.GetJwtSecurityKey();
        private readonly string rsaPrivateKeyPath = appOptions.RSAPrivateKey;

        public string Decrypt(string source)
        {
            using RSA rsa = RSA.Create(2048);
            string rsaPrivateKey = File.ReadAllText(rsaPrivateKeyPath);
            rsa.ImportFromPem(rsaPrivateKey);
            byte[] decryptedBytes = rsa.Decrypt(Convert.FromBase64String(source), RSAEncryptionPadding.OaepSHA1);
            return Encoding.UTF8.GetString(decryptedBytes);
        }

        public async Task<PassportResult> RegisterAsync(Passport passport, DeviceInfo? deviceInfo)
        {
            if (await userManager.FindByNameAsync(passport.UserName).ConfigureAwait(false) is not null)
            {
                return new(false, "This email address is already registered", ServerKeys.ServerPassportServiceEmailHasRegistered);
            }

            HutaoUser newUser = new() { UserName = passport.UserName };
            IdentityResult result = await userManager.CreateAsync(newUser, passport.Password).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                StringBuilder messageBuilder = new();

                foreach (IdentityError error in result.Errors)
                {
                    messageBuilder.AppendLine($"[{error.Code}]: {error.Description}");
                }

                return new(false, $"Registration failed: {messageBuilder}", null);
            }

            TokenResponse tokenResponse = await CreateTokenResponseAsync(newUser, deviceInfo).ConfigureAwait(false);

            if (!await appDbContext.RegistrationRecords.AnyAsync(r => r.UserName == passport.UserName).ConfigureAwait(false))
            {
                await appDbContext.RegistrationRecords.AddAndSaveAsync(new() { UserName = passport.UserName }).ConfigureAwait(false);

                newUser.CdnExpireAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)TimeSpan.FromDays(3).TotalSeconds;
                newUser.GachaLogExpireAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)TimeSpan.FromDays(3).TotalSeconds;
                await userManager.UpdateAsync(newUser).ConfigureAwait(false);
                return new(true, "Registration succeeded; first-time registration includes 3 days of Hutao Cloud access", ServerKeys.ServerPassportRegisterSucceedFirstTime, tokenResponse);
            }

            return new(true, "Registration succeeded", ServerKeys.ServerPassportRegisterSucceed, tokenResponse);
        }

        public async Task<PassportResult> ResetPasswordAsync(Passport passport, DeviceInfo? deviceInfo)
        {
            if (await userManager.FindByNameAsync(passport.UserName).ConfigureAwait(false) is { } user)
            {
                if (await userManager.CheckPasswordAsync(user, passport.Password).ConfigureAwait(false))
                {
                    return new(false, "The new password must be different from the old password", ServerKeys.ServerPassportResetPasswordTheSamePassword);
                }

                await userManager.RemovePasswordAsync(user).ConfigureAwait(false);
                await userManager.AddPasswordAsync(user, passport.Password).ConfigureAwait(false);

                TokenResponse tokenResponse = await CreateTokenResponseAsync(user, deviceInfo).ConfigureAwait(false);
                return new(true, "New password set successfully", ServerKeys.ServerPassportResetPasswordSucceed, tokenResponse);
            }

            return new(false, "This email address is not registered", ServerKeys.ServerPassportServiceEmailHasNotRegistered);
        }

        public async Task<PassportResult> ResetUsernameAsync(Passport passport, DeviceInfo? deviceInfo)
        {
            if (await userManager.FindByNameAsync(passport.NewUserName).ConfigureAwait(false) is not null)
            {
                return new(false, "This email address is already registered", ServerKeys.ServerPassportServiceEmailHasRegistered);
            }

            if (await userManager.FindByNameAsync(passport.UserName).ConfigureAwait(false) is { } user)
            {
                await userManager.SetUserNameAsync(user, passport.NewUserName).ConfigureAwait(false);

                if (!await appDbContext.RegistrationRecords.AnyAsync(r => r.UserName == passport.NewUserName).ConfigureAwait(false))
                {
                    await appDbContext.RegistrationRecords.AddAndSaveAsync(new() { UserName = passport.NewUserName }).ConfigureAwait(false);
                }

                TokenResponse tokenResponse = await CreateTokenResponseAsync(user, deviceInfo).ConfigureAwait(false);
                return new(true, "Email address changed successfully", ServerKeys.ServerPassportResetUserNameSucceed, tokenResponse);
            }

            return new(false, "This email address is not registered", ServerKeys.ServerPassportServiceEmailHasNotRegistered);
        }

        public async Task<PassportResult> LoginAsync(Passport passport, DeviceInfo? deviceInfo)
        {
            if (await userManager.FindByNameAsync(passport.UserName).ConfigureAwait(false) is { } user)
            {
                if (await userManager.CheckPasswordAsync(user, passport.Password).ConfigureAwait(false))
                {
                    TokenResponse tokenResponse = await CreateTokenResponseAsync(user, deviceInfo).ConfigureAwait(false);
                    return new(true, "Login succeeded", ServerKeys.ServerPassportLoginSucceed, tokenResponse);
                }
            }

            return new(false, "Incorrect email address or password", ServerKeys.ServerPassportUserNameOrPasswordIncorrect);
        }

        public async Task<PassportResult> CancelAsync(Passport passport)
        {
            if (await userManager.FindByNameAsync(passport.UserName).ConfigureAwait(false) is { } user)
            {
                if (await userManager.CheckPasswordAsync(user, passport.Password).ConfigureAwait(false))
                {
                    await userManager.DeleteAsync(user).ConfigureAwait(false);
                    return new(true, "Account deleted successfully", ServerKeys.ServerPassportUnregisterSucceed);
                }

                return new(false, "Failed to delete account", ServerKeys.ServerPassportServiceUnregisterPasswordIncorrect);
            }

            return new PassportResult(false, "Failed to delete account", ServerKeys.ServerPassportServiceUnregisterUserNotFound);
        }

        public async Task<List<LoggedInDeviceInfo>> GetLoggedInDevicesAsync(int userId, DeviceInfo deviceInfo)
        {
            return await appDbContext.RefreshTokens
                .Where(rt => rt.UserId == userId)
                .Select(rt => new LoggedInDeviceInfo
                {
                    DeviceId = rt.DeviceId,
                    DeviceName = rt.DeviceName,
                    OperatingSystem = rt.OperatingSystem,
                    LastLoginAt = rt.CreatedAt,
                    ExpiresAt = rt.ExpiresAt,
                    IsCurrentDevice = rt.DeviceId == deviceInfo.DeviceId,
                })
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<TokenResponse?> RefreshTokenAsync(string refreshTokenValue)
        {
            RefreshToken? refreshToken = await appDbContext.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == refreshTokenValue)
                .ConfigureAwait(false);

            if (refreshToken is null || refreshToken.ExpiresAt <= DateTime.UtcNow)
            {
                return default;
            }

            await appDbContext.RefreshTokens.RemoveAndSaveAsync(refreshToken).ConfigureAwait(false);
            return await CreateTokenResponseAsync(refreshToken);
        }

        public async Task<bool> RevokeRefreshTokenAsync(string deviceId)
        {
            RefreshToken? refreshToken = await appDbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.DeviceId == deviceId)
                .ConfigureAwait(false);

            if (refreshToken == null)
            {
                return false;
            }

            await appDbContext.RefreshTokens.RemoveAndSaveAsync(refreshToken).ConfigureAwait(false);

            return true;
        }

        public async Task RevokeAllUserTokensAsync(int userId)
        {
            await appDbContext.RefreshTokens
                .Where(rt => rt.UserId == userId)
                .ExecuteDeleteAsync()
                .ConfigureAwait(false);
        }

        public Task<TokenResponse> CreateTokenResponseAsync(RefreshToken refreshToken)
        {
            return CreateTokenResponseAsync(refreshToken.UserId, new(refreshToken.DeviceId, refreshToken.DeviceName, refreshToken.OperatingSystem));
        }

        public Task<TokenResponse> CreateTokenResponseAsync(HutaoUser user, DeviceInfo? deviceInfo)
        {
            return CreateTokenResponseAsync(user.Id, deviceInfo);
        }

        public async Task<TokenResponse> CreateTokenResponseAsync(int userId, DeviceInfo? deviceInfo)
        {
            string accessToken = CreateAccessToken(userId, deviceInfo);
            int expirationMinutes = deviceInfo is null ? LegacyAccessTokenExpirationMinutes : AccessTokenExpirationMinutes;
            if (deviceInfo is null)
            {
                return new()
                {
                    AccessToken = accessToken,
                    RefreshToken = default!,
                    ExpiresIn = expirationMinutes * 60,
                };
            }

            RefreshToken refreshToken = await CreateRefreshTokenAsync(userId, deviceInfo);

            return new()
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                ExpiresIn = expirationMinutes * 60,
            };
        }

        private static unsafe string GenerateRefreshToken()
        {
            Span<byte> randomBytes = stackalloc byte[64];
            RandomNumberGenerator.Create().GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        private string CreateAccessToken(int userId, DeviceInfo? deviceInfo)
        {
            JwtSecurityTokenHandler handler = new();
            ClaimsIdentity claimsIdentity = new([
                new(PassportClaimTypes.UserId, userId.ToString()),
            ]);

            if (deviceInfo is not null)
            {
                claimsIdentity.AddClaim(new(JwtRegisteredClaimNames.Jti, deviceInfo.DeviceId));
            }

            int expirationMinutes = deviceInfo is null ? LegacyAccessTokenExpirationMinutes : AccessTokenExpirationMinutes;

            SecurityTokenDescriptor descriptor = new()
            {
                Subject = claimsIdentity,
                Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                Issuer = appOptions.JWTIssuer,
                SigningCredentials = new(jwtSigningKey, SecurityAlgorithms.HmacSha256Signature),
            };

            SecurityToken token = handler.CreateToken(descriptor);
            return handler.WriteToken(token);
        }

        private async Task<RefreshToken> CreateRefreshTokenAsync(int userId, DeviceInfo deviceInfo)
        {
            RefreshToken refreshToken = new()
            {
                UserId = userId,
                Token = GenerateRefreshToken(),
                ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenExpirationDays),
                CreatedAt = DateTime.UtcNow,
                DeviceId = deviceInfo.DeviceId,
                DeviceName = deviceInfo.DeviceName,
                OperatingSystem = deviceInfo.OperatingSystem,
            };

            await appDbContext.RefreshTokens.AddAndSaveAsync(refreshToken);
            return refreshToken;
        }
    }
}