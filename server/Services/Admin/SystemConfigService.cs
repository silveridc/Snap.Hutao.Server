using server.Models.Entity;
using System.Collections.Concurrent;
using System.Globalization;

namespace server.Services.Admin;

// Singleton
public sealed class SystemConfigService(IServiceScopeFactory scopeFactory, AppOptions appOptions, ILogger<SystemConfigService> logger, TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _initialized;

    // Typed accessors with AppOptions fallback
    public string PizzaHelperUidSalt => GetOrDefault(Keys.PizzaHelperUidSalt, appOptions.GenshinPizzaHelper?.UidSalt ?? string.Empty);

    public string PizzaHelperApiSalt => GetOrDefault(Keys.PizzaHelperApiSalt, appOptions.GenshinPizzaHelper?.ApiSalt ?? string.Empty);

    public string PizzaHelperAvatarHoldingEndpoint => GetOrDefault(Keys.PizzaHelperAvatarHoldingEndpoint, appOptions.GenshinPizzaHelper?.EndPoints?.AvatarHolding ?? string.Empty);

    public string PizzaHelperSpiralAbyssEndpoint => GetOrDefault(Keys.PizzaHelperSpiralAbyssEndpoint, appOptions.GenshinPizzaHelper?.EndPoints?.SpiralAbyss ?? string.Empty);

    public string AfdianSkuGachaLog => GetOrDefault(Keys.AfdianSkuGachaLog, appOptions.Afdian?.SkuGachaLogUploadService ?? string.Empty);

    public string Afdian2SkuCdn => GetOrDefault(Keys.Afdian2SkuCdn, appOptions.Afdian2?.SkuCdnService ?? string.Empty);

    public string SmtpDiagnosticEmail => GetOrDefault(Keys.SmtpDiagnosticEmail, appOptions.Smtp?.DiagnosticEmailAddress ?? string.Empty);

    public bool MaintenanceMode => GetBool(Keys.MaintenanceMode, false);

    public bool PatchDisableFpsUnlock => GetBool(Keys.PatchDisableFpsUnlock, false);

    public bool PatchDisableIslandInjection => GetBool(Keys.PatchDisableIslandInjection, false);

    public string PatchDisableFpsUnlockReason => GetOrDefault(Keys.PatchDisableFpsUnlockReason, string.Empty).Trim();

    public string PatchDisableIslandInjectionReason => GetOrDefault(Keys.PatchDisableIslandInjectionReason, string.Empty).Trim();

    public string PatchS3Host => GetOrDefault(Keys.PatchS3Host, string.Empty).Trim();

    public string MailTemplateHtml => GetOrDefault(Keys.MailTemplateHtml, string.Empty);

    public string MailSystemName => GetOrDefault(Keys.MailSystemName, appOptions.Smtp.SystemName).Trim();

    public string MailPublicBaseUrl => GetOrDefault(Keys.MailPublicBaseUrl, appOptions.Smtp.PublicBaseUrl).Trim().TrimEnd('/');

    public int VerificationCodeExpireSeconds => GetInt(Keys.VerificationCodeExpireSeconds, 900, 60, 3600, 60);

    public int VerificationResendCooldownSeconds => GetInt(Keys.VerificationResendCooldownSeconds, 60, 10, 600);

    public int ResetPasswordLinkExpireSeconds => GetInt(Keys.ResetPasswordLinkExpireSeconds, 900, 60, 3600, 60);

    public int GachaLogMaxUidPerUser => GetInt(Keys.GachaLogMaxUidPerUser, 5, 1, 50);

    public int RegistrationGiftDays => GetInt(Keys.RegistrationGiftDays, 3, 1, 365);

    public int GachaLogExtendDays => GetInt(Keys.GachaLogExtendDays, 3, 1, 365);

    public int RedeemCodeExpireDays => GetInt(Keys.RedeemCodeExpireDays, 3, 1, 365);

    public int JobLogRetentionDays => GetInt(Keys.JobLogRetentionDays, 90, 7, 3650);

    public int SpiralAbyssRecordRetentionDays => GetInt(Keys.SpiralAbyssRecordRetentionDays, 30, 7, 3650);

    public string RecaptchaSiteKey => GetOrDefault(Keys.RecaptchaSiteKey, appOptions.ReCaptchaSiteKey ?? string.Empty).Trim();

    public string RecaptchaSecretKey => GetOrDefault(Keys.RecaptchaSecretKey, appOptions.ReCaptchaKey ?? string.Empty);

    public float RecaptchaMinScore => GetFloat(Keys.RecaptchaMinScore, 0.5f, 0f, 1f);

    public SmtpOptions Smtp => new()
    {
        SystemName = MailSystemName,
        PublicBaseUrl = MailPublicBaseUrl,
        DiagnosticEmailAddress = SmtpDiagnosticEmail.Trim(),
        Server = GetOrDefault(Keys.SmtpServer, appOptions.Smtp.Server ?? string.Empty).Trim(),
        Port = GetInt(Keys.SmtpPort, appOptions.Smtp.Port, 1, 65535),
        Security = GetOrDefault(Keys.SmtpSecurity, appOptions.Smtp.Security).Trim(),
        UserName = GetOrDefault(Keys.SmtpUserName, appOptions.Smtp.UserName ?? string.Empty).Trim(),
        Password = GetOrDefault(Keys.SmtpPassword, appOptions.Smtp.Password ?? string.Empty),
        FromName = GetOrDefault(Keys.SmtpFromName, appOptions.Smtp.FromName).Trim(),
        FromAddress = GetOrDefault(Keys.SmtpFromAddress, appOptions.Smtp.FromAddress).Trim(),
        TimeoutSeconds = GetInt(Keys.SmtpTimeoutSeconds, appOptions.Smtp.TimeoutSeconds, 1, 120),
    };

    public bool IsPatchS3SigningEnabled =>
        !string.IsNullOrWhiteSpace(PatchS3Host)
        && !string.IsNullOrWhiteSpace(GetOrDefault(Keys.PatchS3Region, string.Empty))
        && !string.IsNullOrWhiteSpace(GetOrDefault(Keys.PatchS3AccessKeyId, string.Empty))
        && !string.IsNullOrWhiteSpace(GetOrDefault(Keys.PatchS3SecretAccessKey, string.Empty));

    public bool RegistrationEnabled => GetBool(Keys.RegistrationEnabled, true);

    public bool CaptchaEnabled => GetBool(Keys.CaptchaEnabled, !string.IsNullOrWhiteSpace(RecaptchaSecretKey) && !string.IsNullOrWhiteSpace(RecaptchaSiteKey));

    public OidcOptions Oidc => new()
    {
        Enabled = GetBool(Keys.OidcEnabled, appOptions.Oidc.Enabled),
        Authority = GetOrDefault(Keys.OidcAuthority, appOptions.Oidc.Authority),
        ClientId = GetOrDefault(Keys.OidcClientId, appOptions.Oidc.ClientId),
        ClientSecret = GetOrDefault(Keys.OidcClientSecret, appOptions.Oidc.ClientSecret),
        RedirectUri = GetOrDefault(Keys.OidcRedirectUri, appOptions.Oidc.RedirectUri),
        ClientRedirectUri = GetOrDefault(Keys.OidcClientRedirectUri, appOptions.Oidc.ClientRedirectUri ?? string.Empty),
        Scope = GetOrDefault(Keys.OidcScope, appOptions.Oidc.Scope),
        DisplayName = GetOrDefault(Keys.OidcDisplayName, appOptions.Oidc.DisplayName ?? string.Empty),
        BootstrapClaim = GetOrDefault(Keys.OidcBootstrapClaim, appOptions.Oidc.BootstrapClaim),
        BootstrapValue = GetOrDefault(Keys.OidcBootstrapValue, appOptions.Oidc.BootstrapValue),
        RequireHttpsMetadata = GetBool(Keys.OidcRequireHttpsMetadata, appOptions.Oidc.RequireHttpsMetadata),
    };

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (!await db.Database.CanConnectAsync().ConfigureAwait(false))
            {
                logger.LogWarning("Database not available, SystemConfigService using defaults");
                _initialized = true;
                return;
            }

            List<SystemSetting> settings = await db.SystemSettings.AsNoTracking().ToListAsync().ConfigureAwait(false);
            foreach (SystemSetting setting in settings)
            {
                _cache[setting.Key] = setting.Value;
            }

            logger.LogInformation("SystemConfigService loaded {Count} settings from database", settings.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load system settings from database");
        }

        _initialized = true;
    }

    public string? Get(string key)
    {
        return _cache.TryGetValue(key, out string? value) ? value : null;
    }

    public string GetOrDefault(string key, string defaultValue)
    {
        string? value = Get(key);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        string? value = Get(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return bool.TryParse(value, out bool result) ? result : defaultValue;
    }

    public int GetInt(string key, int defaultValue, int minimum, int maximum, int step = 1)
    {
        return int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            && result >= minimum && result <= maximum && result % step == 0 ? result : defaultValue;
    }

    public float GetFloat(string key, float defaultValue, float minimum, float maximum)
    {
        return float.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float result)
            && result >= minimum && result <= maximum ? result : defaultValue;
    }

    /// <summary>读取某一种邮件的文案（键 MailContent.{kind}.{language}），未自定义或反序列化失败时回落内置默认.</summary>
    public MailContent GetMailContent(string kind, string language)
    {
        MailContentKind definition = MailContents.Get(kind);
        MailContent fallback = definition.For(language);
        string raw = GetOrDefault($"MailContent.{kind}.{language}", string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<MailContent>(raw) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    // Admin operations
    public async Task<Dictionary<string, string?>> GetAllAsync()
    {
        await InitializeAsync().ConfigureAwait(false);
        Dictionary<string, string?> settings = new(_cache, StringComparer.OrdinalIgnoreCase);
        SmtpOptions smtp = Smtp;
        settings[Keys.VerificationCodeExpireSeconds] = VerificationCodeExpireSeconds.ToString(CultureInfo.InvariantCulture);
        settings[Keys.VerificationResendCooldownSeconds] = VerificationResendCooldownSeconds.ToString(CultureInfo.InvariantCulture);
        settings[Keys.ResetPasswordLinkExpireSeconds] = ResetPasswordLinkExpireSeconds.ToString(CultureInfo.InvariantCulture);
        settings[Keys.GachaLogMaxUidPerUser] = GachaLogMaxUidPerUser.ToString(CultureInfo.InvariantCulture);
        settings[Keys.RegistrationGiftDays] = RegistrationGiftDays.ToString(CultureInfo.InvariantCulture);
        settings[Keys.GachaLogExtendDays] = GachaLogExtendDays.ToString(CultureInfo.InvariantCulture);
        settings[Keys.RedeemCodeExpireDays] = RedeemCodeExpireDays.ToString(CultureInfo.InvariantCulture);
        settings[Keys.JobLogRetentionDays] = JobLogRetentionDays.ToString(CultureInfo.InvariantCulture);
        settings[Keys.SpiralAbyssRecordRetentionDays] = SpiralAbyssRecordRetentionDays.ToString(CultureInfo.InvariantCulture);
        settings[Keys.RecaptchaSiteKey] = RecaptchaSiteKey;
        settings[Keys.RecaptchaSecretKey] = RecaptchaSecretKey;
        settings[Keys.RecaptchaMinScore] = RecaptchaMinScore.ToString(CultureInfo.InvariantCulture);
        settings[Keys.SmtpServer] = smtp.Server;
        settings[Keys.SmtpPort] = smtp.Port.ToString(CultureInfo.InvariantCulture);
        settings[Keys.SmtpSecurity] = smtp.Security;
        settings[Keys.SmtpUserName] = smtp.UserName;
        settings[Keys.SmtpPassword] = smtp.Password;
        settings[Keys.SmtpFromName] = smtp.FromName;
        settings[Keys.SmtpFromAddress] = smtp.FromAddress;
        settings[Keys.SmtpTimeoutSeconds] = smtp.TimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        settings[Keys.SmtpDiagnosticEmail] = smtp.DiagnosticEmailAddress;
        settings[Keys.MailSystemName] = MailSystemName;
        settings[Keys.MailPublicBaseUrl] = MailPublicBaseUrl;
        settings[Keys.MaintenanceMode] = MaintenanceMode ? "true" : "false";
        settings[Keys.RegistrationEnabled] = RegistrationEnabled ? "true" : "false";
        settings[Keys.CaptchaEnabled] = CaptchaEnabled ? "true" : "false";
        settings[Keys.PizzaHelperUidSalt] = PizzaHelperUidSalt;
        settings[Keys.PizzaHelperApiSalt] = PizzaHelperApiSalt;
        settings[Keys.OidcClientSecret] = Oidc.ClientSecret;

        // 管理接口只暴露敏感值是否配置，绝不把明文凭据送回浏览器
        foreach (string key in settings.Keys.ToArray())
        {
            if (SystemSettingSecurity.IsSensitiveKey(key))
            {
                settings[key] = string.IsNullOrWhiteSpace(settings[key]) ? null : SystemSettingSecurity.MaskedValue;
            }
        }

        return settings;
    }

    public async Task<(bool Success, string Message)> UpdateAsync(Dictionary<string, string?> settings, int userId)
    {
        await InitializeAsync().ConfigureAwait(false);
        Dictionary<string, string?> changes = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string? value) in settings)
        {
            string? error = SystemSettingValidation.Validate(key, value);
            if (error is not null)
            {
                return (false, error);
            }

            // 空密码和掩码都表示保持原值，恢复配置文件值由 DELETE 明确完成
            if (SystemSettingSecurity.IsSensitiveKey(key) && (string.IsNullOrWhiteSpace(value) || value == SystemSettingSecurity.MaskedValue))
            {
                continue;
            }

            if (!changes.TryAdd(key, value))
            {
                return (false, $"Duplicate configuration key: {key}");
            }
        }

        if (changes.Count == 0)
        {
            return (true, "Configuration unchanged");
        }

        using IServiceScope scope = scopeFactory.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        List<string> keys = [.. changes.Keys];
        List<SystemSetting> existing = await db.SystemSettings
            .Where(s => keys.Contains(s.Key))
            .ToListAsync()
            .ConfigureAwait(false);

        Dictionary<string, SystemSetting> existingMap = existing.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
        DateTimeOffset now = timeProvider.GetUtcNow();

        foreach ((string key, string? value) in changes)
        {
            if (existingMap.TryGetValue(key, out SystemSetting? entity))
            {
                entity.Value = value;
                entity.UpdatedAt = now;
                entity.UpdatedBy = userId;
            }
            else
            {
                db.SystemSettings.Add(new SystemSetting
                {
                    Key = key,
                    Value = value,
                    UpdatedAt = now,
                    UpdatedBy = userId,
                });
            }
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
        foreach ((string key, string? value) in changes)
        {
            _cache[key] = value;
        }

        logger.LogInformation("System settings updated by user {UserId}: {Keys}", userId, string.Join(", ", keys));
        return (true, "Configuration saved");
    }

    public async Task<(bool Success, string Message)> DeleteAsync(string key)
    {
        await InitializeAsync().ConfigureAwait(false);

        using IServiceScope scope = scopeFactory.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        int deleted = await db.SystemSettings
            .Where(s => s.Key == key)
            .ExecuteDeleteAsync()
            .ConfigureAwait(false);

        _cache.TryRemove(key, out _);

        return deleted > 0 ? (true, "Default value restored") : (false, "Configuration setting does not exist");
    }
}
