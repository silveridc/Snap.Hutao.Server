using server.Models.Entity;
using server.Models.Mail;
using server.Services.Mail;
using System.Collections.Concurrent;
using System.Globalization;

namespace server.Services.Admin;

// Singleton
public sealed class SystemConfigService(IServiceScopeFactory scopeFactory, AppOptions appOptions, ILogger<SystemConfigService> logger, TimeProvider timeProvider)
{
    private const string _defaultYaeBucketUrls = "https://ena-rin.holohat.work/schicksal/metadata,https://rin.holohat.work/schicksal/metadata";
    private const string _defaultYaeChineseGameHashes = "3851921252";
    private static readonly char[] _listSeparators = [',', ';', '\n', '\r', ' '];

    private readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _initialized;

    // Typed accessors
    public string PizzaHelperUidSalt => GetOrDefault(Keys.PizzaHelperUidSalt, string.Empty);

    public string PizzaHelperApiSalt => GetOrDefault(Keys.PizzaHelperApiSalt, string.Empty);

    public string PizzaHelperAvatarHoldingEndpoint => GetOrDefault(Keys.PizzaHelperAvatarHoldingEndpoint, string.Empty);

    public string PizzaHelperSpiralAbyssEndpoint => GetOrDefault(Keys.PizzaHelperSpiralAbyssEndpoint, string.Empty);

    public string AfdianSkuGachaLog => GetOrDefault(Keys.AfdianSkuGachaLog, string.Empty);

    public string Afdian2SkuCdn => GetOrDefault(Keys.Afdian2SkuCdn, string.Empty);

    public string AfdianUserId => GetOrDefault(Keys.AfdianUserId, string.Empty).Trim();

    public string AfdianUserToken => GetOrDefault(Keys.AfdianUserToken, string.Empty);

    public string Afdian2UserId => GetOrDefault(Keys.Afdian2UserId, string.Empty).Trim();

    public string Afdian2UserToken => GetOrDefault(Keys.Afdian2UserToken, string.Empty);

    public string[] YaeBucketUrls => [.. GetOrDefault(Keys.YaeBucketUrls, _defaultYaeBucketUrls).Split(_listSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    public uint[] YaeChineseGameHashes =>
    [
        .. GetOrDefault(Keys.YaeChineseGameHashes, _defaultYaeChineseGameHashes)
            .Split(_listSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint hash) ? hash : 0U)
            .Where(hash => hash is not 0U),
    ];

    public string SmtpDiagnosticEmail => GetOrDefault(Keys.SmtpDiagnosticEmail, string.Empty);

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

    public string RecaptchaSiteKey => GetOrDefault(Keys.RecaptchaSiteKey, appOptions.ReCaptcha.SiteKey ?? string.Empty).Trim();

    public string RecaptchaSecretKey => GetOrDefault(Keys.RecaptchaSecretKey, appOptions.ReCaptcha.Key ?? string.Empty);

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
        Enabled = GetBool(Keys.OidcEnabled, false),
        Authority = GetOrDefault(Keys.OidcAuthority, string.Empty),
        ClientId = GetOrDefault(Keys.OidcClientId, string.Empty),
        ClientSecret = GetOrDefault(Keys.OidcClientSecret, string.Empty),
        RedirectUri = GetOrDefault(Keys.OidcRedirectUri, string.Empty),
        ClientRedirectUri = GetOrDefault(Keys.OidcClientRedirectUri, string.Empty),
        Scope = GetOrDefault(Keys.OidcScope, "openid profile email"),
        DisplayName = GetOrDefault(Keys.OidcDisplayName, string.Empty),
        BootstrapClaim = GetOrDefault(Keys.OidcBootstrapClaim, "sub"),
        BootstrapValue = GetOrDefault(Keys.OidcBootstrapValue, string.Empty),
        RequireHttpsMetadata = GetBool(Keys.OidcRequireHttpsMetadata, true),
    };

    /// <summary>
    /// Loads the system settings from the database into the in-memory cache. Initialization runs at most once per process.
    /// </summary>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
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

    /// <summary>
    /// Gets the raw configured value of the specified setting key.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <returns>The configured value, or <see langword="null"/> when the key is not configured.</returns>
    public string? Get(string key)
    {
        return _cache.TryGetValue(key, out string? value) ? value : null;
    }

    /// <summary>
    /// Gets the configured value of the specified setting key, or the provided default when it is missing or blank.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="defaultValue">The value returned when the setting is missing or blank.</param>
    /// <returns>The configured value or the default value.</returns>
    public string GetOrDefault(string key, string defaultValue)
    {
        string? value = Get(key);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    /// <summary>
    /// Gets the boolean value of the specified setting key.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="defaultValue">The value returned when the setting is missing or cannot be parsed.</param>
    /// <returns>The parsed boolean value or the default value.</returns>
    public bool GetBool(string key, bool defaultValue = false)
    {
        string? value = Get(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return bool.TryParse(value, out bool result) ? result : defaultValue;
    }

    /// <summary>
    /// Gets the integer value of the specified setting key, enforcing the range and step constraints.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="defaultValue">The value returned when the setting is missing or out of range.</param>
    /// <param name="minimum">The inclusive minimum value.</param>
    /// <param name="maximum">The inclusive maximum value.</param>
    /// <param name="step">The required step; the value must be a multiple of it.</param>
    /// <returns>The parsed integer value or the default value.</returns>
    public int GetInt(string key, int defaultValue, int minimum, int maximum, int step = 1)
    {
        return int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            && result >= minimum && result <= maximum && result % step == 0 ? result : defaultValue;
    }

    /// <summary>
    /// Gets the floating-point value of the specified setting key, enforcing the range constraints.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="defaultValue">The value returned when the setting is missing or out of range.</param>
    /// <param name="minimum">The inclusive minimum value.</param>
    /// <param name="maximum">The inclusive maximum value.</param>
    /// <returns>The parsed floating-point value or the default value.</returns>
    public float GetFloat(string key, float defaultValue, float minimum, float maximum)
    {
        return float.TryParse(Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float result)
            && result >= minimum && result <= maximum ? result : defaultValue;
    }

    /// <summary>
    /// Gets the localized content of a mail kind (key format: MailContent.{kind}.{language}), falling back to the
    /// built-in default when it is not customized or cannot be deserialized.
    /// </summary>
    /// <param name="kind">The mail content kind.</param>
    /// <param name="language">The language code.</param>
    /// <returns>The customized mail content or the built-in default.</returns>
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

    /// <summary>
    /// Gets all settings with sensitive values masked, so plaintext credentials are never sent to the browser.
    /// </summary>
    /// <returns>The settings dictionary with masked sensitive values.</returns>
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

    /// <summary>
    /// Validates and persists the given settings, keeping the current value for blank or masked sensitive fields.
    /// </summary>
    /// <param name="settings">The settings to update.</param>
    /// <param name="userId">The identifier of the administrator performing the update.</param>
    /// <returns>A tuple containing whether the update succeeded and a status message.</returns>
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

    /// <summary>
    /// Deletes the stored value of the specified setting key so that the default value takes effect again.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <returns>A tuple containing whether a stored value was deleted and a status message.</returns>
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
