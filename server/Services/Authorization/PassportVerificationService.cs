using server.Core;
using server.Services.Admin;
using StackExchange.Redis;
using System.Security.Cryptography;

namespace server.Services.Authorization;

// Scoped
public sealed class PassportVerificationService(AppOptions appOptions, SystemConfigService systemConfigService)
{
    public const string ResetPassword = "ResetPassword";
    public const string CancelRegistration = "CancelRegistration";
    public const string Registration = "Registration";
    public const string ResetUserName = "ResetUsername";
    public const string ResetUserNameNew = "ResetUsernameNew";

    private const string _resetTokenPrefix = "ResetToken";

    private readonly ConnectionMultiplexer _redis = ConnectionMultiplexer.Connect(appOptions.RedisAddress);

    // 冷却状态独立于验证码有效期，检查时不修改已签发验证码。
    public bool TryGetNonExpiredVerifyCode(string normalizedUserName, string key, out string? code)
    {
        IDatabase db = _redis.GetDatabase(15);

        code = default;
        if (!db.KeyExists($"{normalizedUserName}.{key}.regenerate"))
        {
            return false;
        }

        code = db.StringGet($"{normalizedUserName}.{key}");
        return true;
    }

    public int GetVerifyCodeResendCooldownSeconds(string normalizedUserName, string key)
    {
        TimeSpan? remaining = _redis.GetDatabase(15).KeyTimeToLive($"{normalizedUserName}.{key}.regenerate");
        return (int)Math.Max(1, Math.Ceiling((remaining ?? TimeSpan.Zero).TotalSeconds));
    }

    public int GetResetTokenResendCooldownSeconds(string normalizedUserName)
    {
        return GetVerifyCodeResendCooldownSeconds(normalizedUserName, $"{ResetPassword}.token");
    }

    public string GenerateVerifyCode(string normalizedUserName, string key)
    {
        string code = RandomHelper.GetUpperAndNumberString(8);
        IDatabase db = _redis.GetDatabase(15);

        db.StringSet($"{normalizedUserName}.{key}", code, TimeSpan.FromSeconds(systemConfigService.VerificationCodeExpireSeconds));
        db.StringSet($"{normalizedUserName}.{key}.regenerate", RedisValue.EmptyString, TimeSpan.FromSeconds(systemConfigService.VerificationResendCooldownSeconds));
        return code;
    }

    // 删除或消费凭据不提前清除冷却键，限流持续到签发时设置的 TTL 到期。
    public void RemoveVerifyCode(string normalizedUserName, string key)
    {
        IDatabase db = _redis.GetDatabase(15);

        db.KeyDelete($"{normalizedUserName}.{key}");
    }

    public bool TryValidateVerifyCode(string normalizedUserName, string key, string code)
    {
        IDatabase db = _redis.GetDatabase(15);

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

    /// <summary>签发一次性重置链接 token，值里存原始邮箱，重置时按它找回账号.</summary>
    /// <param name="normalizedUserName">标准化后的邮箱.</param>
    /// <param name="userName">原始邮箱.</param>
    /// <param name="token">32 位小写十六进制 token，被限流时为空.</param>
    /// <returns>仍处于上次签发的冷却期时返回 false.</returns>
    public bool TryGenerateResetToken(string normalizedUserName, string userName, out string token)
    {
        IDatabase db = _redis.GetDatabase(15);
        string pointerKey = ResetTokenPointerKey(normalizedUserName);

        if (db.KeyExists(ResetTokenThrottleKey(normalizedUserName)))
        {
            token = string.Empty;
            return false;
        }

        RedisValue previous = db.StringGet(pointerKey);
        if (!previous.IsNullOrEmpty)
        {
            // 同一账号同时只保留最新链接，重发时旧链接立即失效
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

    /// <summary>查询 token 绑定的邮箱与剩余有效期，不消费.</summary>
    /// <param name="token">链接里的 token.</param>
    /// <param name="userName">token 绑定的原始邮箱.</param>
    /// <param name="remaining">剩余有效期，Redis 未给出 TTL 时为 null.</param>
    /// <returns>token 有效时返回 true.</returns>
    public bool TryPeekResetToken(string token, out string userName, out TimeSpan? remaining)
    {
        IDatabase db = _redis.GetDatabase(15);
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

    /// <summary>消费 token，改密成功后同一条链接立即失效.</summary>
    /// <param name="token">链接里的 token.</param>
    /// <param name="userName">token 绑定的原始邮箱.</param>
    /// <returns>token 有效时返回 true.</returns>
    public bool TryConsumeResetToken(string token, out string userName)
    {
        IDatabase db = _redis.GetDatabase(15);
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

    private static string ResetTokenKey(string token) => $"{_resetTokenPrefix}.{token}";

    private static string ResetTokenPointerKey(string normalizedUserName) => $"{normalizedUserName}.{ResetPassword}.token";

    private static string ResetTokenThrottleKey(string normalizedUserName) => $"{normalizedUserName}.{ResetPassword}.token.regenerate";
}
