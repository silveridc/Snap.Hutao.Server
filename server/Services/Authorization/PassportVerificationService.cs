using server.Services.Admin;
using StackExchange.Redis;
using System.Security.Cryptography;

namespace server.Services.Authorization;

// Scoped
public sealed class PassportVerificationService(IConnectionMultiplexer redis, SystemConfigService systemConfigService)
{
    public const string ResetPassword = "ResetPassword";
    public const string CancelRegistration = "CancelRegistration";
    public const string Registration = "Registration";
    public const string ResetUserName = "ResetUsername";
    public const string ResetUserNameNew = "ResetUsernameNew";

    private const string _resetTokenPrefix = "ResetToken";
    private readonly IDatabase db = redis.GetDatabase(1);

    /// <summary>
    /// Attempts to retrieve an active (non-expired) verification code for the specified user and business scenario.
    /// </summary>
    /// <param name="normalizedUserName">The normalized username associated with the verification code.</param>
    /// <param name="key">The key identifying the business scenario.</param>
    /// <param name="code">
    /// When this method returns, contains the active verification code if found and valid;
    /// otherwise, <see langword="null"/>. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a valid, non-expired verification code was found; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetNonExpiredVerifyCode(string normalizedUserName, string key, out string? code)
    {
        code = default;
        if (!db.KeyExists($"{normalizedUserName}.{key}.regenerate"))
        {
            return false;
        }

        code = db.StringGet($"{normalizedUserName}.{key}");
        return true;
    }

    /// <summary>
    /// Gets the remaining cooldown time in seconds before a verification code can be regenerated or resent.
    /// </summary>
    /// <param name="normalizedUserName">The normalized user name.</param>
    /// <param name="key">The business scenario identifier.</param>
    /// <returns>The remaining cooldown time in seconds, or at least 1 second if the cooldown key exists.</returns>
    public int GetVerifyCodeResendCooldownSeconds(string normalizedUserName, string key) =>
        (int)Math.Max(1, Math.Ceiling((db.KeyTimeToLive($"{normalizedUserName}.{key}.regenerate") ?? TimeSpan.Zero).TotalSeconds));

    /// <summary>
    /// Gets the remaining cooldown time in seconds before a password reset token can be regenerated.
    /// </summary>
    /// <param name="normalizedUserName">The normalized user name.</param>
    /// <returns>The remaining cooldown time in seconds.</returns>
    public int GetResetTokenResendCooldownSeconds(string normalizedUserName) =>
        GetVerifyCodeResendCooldownSeconds(normalizedUserName, $"{ResetPassword}.token");

    /// <summary>
    /// Generates a new verification code, caches it in Redis, and initiates the resend throttle cooldown.
    /// </summary>
    /// <param name="normalizedUserName">The normalized user name.</param>
    /// <param name="key">The business scenario identifier.</param>
    /// <returns>The generated alphanumeric verification code.</returns>
    public string GenerateVerifyCode(string normalizedUserName, string key)
    {
        string code = new(Random.Shared.GetItems<char>("ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890", 8));
        db.StringSet($"{normalizedUserName}.{key}", code, TimeSpan.FromSeconds(systemConfigService.VerificationCodeExpireSeconds));
        db.StringSet($"{normalizedUserName}.{key}.regenerate", RedisValue.EmptyString, TimeSpan.FromSeconds(systemConfigService.VerificationResendCooldownSeconds));
        return code;
    }

    /// <summary>
    /// Removes the specified verification code from the cache.
    /// </summary>
    /// <param name="normalizedUserName">The normalized user name.</param>
    /// <param name="key">The business scenario identifier.</param>
    public void RemoveVerifyCode(string normalizedUserName, string key) => db.KeyDelete($"{normalizedUserName}.{key}");

    /// <summary>
    /// Attempts to validate a provided verification code against the cached code and consumes it if valid.
    /// </summary>
    /// <param name="normalizedUserName">The normalized user name.</param>
    /// <param name="key">The business scenario identifier.</param>
    /// <param name="code">The verification code to validate.</param>
    /// <returns>
    /// <see langword="true"/> if the code matches the cached value (case-insensitive); otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryValidateVerifyCode(string normalizedUserName, string key, string code)
    {
        string? redisCode = db.StringGet($"{normalizedUserName}.{key}");
        if (redisCode is null)
        {
            return false;
        }

        if (!string.Equals(redisCode, code, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        db.KeyDelete($"{normalizedUserName}.{key}");
        return true;
    }

    /// <summary>
    /// Attempts to generate a password reset token if the operation is not currently throttled.
    /// </summary>
    /// <param name="normalizedUserName">The normalized user name used for pointer and throttling keys.</param>
    /// <param name="userName">The actual user name to associate with the reset token.</param>
    /// <param name="token">
    /// When this method returns, contains the generated 32-character hex reset token if creation succeeded;
    /// otherwise, an empty string. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the token was successfully generated.
    /// <see langword="false"/> if the request was throttled by the resend cooldown.
    /// </returns>
    public bool TryGenerateResetToken(string normalizedUserName, string userName, out string token)
    {
        string pointerKey = ResetTokenPointerKey(normalizedUserName);

        if (db.KeyExists(ResetTokenThrottleKey(normalizedUserName)))
        {
            token = string.Empty;
            return false;
        }

        RedisValue previous = db.StringGet(pointerKey);
        if (!previous.IsNullOrEmpty)
        {
            db.KeyDelete(ResetTokenKey(previous.ToString()));
        }

        string generated = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        TimeSpan expiry = TimeSpan.FromSeconds(systemConfigService.ResetPasswordLinkExpireSeconds);
        db.StringSet(ResetTokenKey(generated), userName, expiry);
        db.StringSet(pointerKey, generated, expiry);
        db.StringSet(ResetTokenThrottleKey(normalizedUserName), RedisValue.EmptyString, TimeSpan.FromSeconds(systemConfigService.VerificationResendCooldownSeconds));
        token = generated;
        return true;
    }

    /// <summary>
    /// Inspects a password reset token without consuming or deleting it.
    /// </summary>
    /// <param name="token">The reset token string.</param>
    /// <param name="userName">
    /// When this method returns, contains the associated user name if the token exists;
    /// otherwise, an empty string. This parameter is passed uninitialized.
    /// </param>
    /// <param name="remaining">
    /// When this method returns, contains the remaining validity duration of the token if it exists;
    /// otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the token is valid and exists in Redis; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryPeekResetToken(string token, out string userName, out TimeSpan? remaining)
    {
        string key = ResetTokenKey(token);
        RedisValue value = db.StringGet(key);

        if (value.IsNullOrEmpty)
        {
            userName = string.Empty;
            remaining = null;
            return false;
        }

        userName = value.ToString();
        remaining = db.KeyTimeToLive(key);
        return true;
    }

    /// <summary>
    /// Validates and permanently consumes a password reset token, removing it and its reference pointer from the cache.
    /// </summary>
    /// <param name="token">The reset token string.</param>
    /// <param name="userName">
    /// When this method returns, contains the user name associated with the token if valid;
    /// otherwise, an empty string. This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the token existed and was consumed; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryConsumeResetToken(string token, out string userName)
    {
        RedisValue value = db.StringGet(ResetTokenKey(token));

        if (value.IsNullOrEmpty)
        {
            userName = string.Empty;
            return false;
        }

        userName = value.ToString();
        db.KeyDelete(ResetTokenKey(token));
        db.KeyDelete(ResetTokenPointerKey(userName.ToUpperInvariant()));
        return true;
    }

    /// <summary>
    /// Generates the Redis key for storing a password reset token and its associated user name.
    /// </summary>
    private static string ResetTokenKey(string token) => $"{_resetTokenPrefix}.{token}";

    /// <summary>
    /// Generates the Redis key that tracks the active reset token for a given normalized user name.
    /// </summary>
    private static string ResetTokenPointerKey(string normalizedUserName) => $"{normalizedUserName}.{ResetPassword}.token";

    /// <summary>
    /// Generates the Redis key used to throttle reset token regeneration.
    /// </summary>
    private static string ResetTokenThrottleKey(string normalizedUserName) => $"{normalizedUserName}.{ResetPassword}.token.regenerate";
}
