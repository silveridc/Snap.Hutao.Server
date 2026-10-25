using System.Collections.Concurrent;
using server.Models.Entity;

namespace server.Services.Admin;

// Singleton
public sealed class SystemConfigService(IServiceScopeFactory scopeFactory, AppOptions appOptions, ILogger<SystemConfigService> logger, TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _initialized;

    public static class Keys
    {
        public const string PizzaHelperUidSalt = "PizzaHelper.UidSalt";
        public const string PizzaHelperApiSalt = "PizzaHelper.ApiSalt";
        public const string PizzaHelperAvatarHoldingEndpoint = "PizzaHelper.EndPoints.AvatarHolding";
        public const string PizzaHelperSpiralAbyssEndpoint = "PizzaHelper.EndPoints.SpiralAbyss";

        public const string AfdianSkuGachaLog = "Afdian.SkuGachaLogUploadService";
        public const string Afdian2SkuCdn = "Afdian2.SkuCdnService";

        public const string SmtpDiagnosticEmail = "Smtp.DiagnosticEmailAddress";

        public const string MaintenanceMode = "System.MaintenanceMode";
        public const string RegistrationEnabled = "System.RegistrationEnabled";
        public const string CaptchaEnabled = "System.CaptchaEnabled";

        // 客户端功能熔断：后台即时开关，经 /Patch/hutao 下发；解除后客户端恢复用户本地设置。
        public const string PatchDisableFpsUnlock = "Patch.DisableFpsUnlock";
        public const string PatchDisableIslandInjection = "Patch.DisableIslandInjection";

        // 更新包 S3 直链签名：镜像主机名匹配时 /Patch/hutao 下发前现场签发限时 URL，桶可保持私有。
        public const string PatchS3Host = "Patch.S3.Host";
        public const string PatchS3Region = "Patch.S3.Region";
        public const string PatchS3AccessKeyId = "Patch.S3.AccessKeyId";
        public const string PatchS3SecretAccessKey = "Patch.S3.SecretAccessKey";
        public const string PatchS3ExpiresHours = "Patch.S3.ExpiresHours";

        // SSO（外部 OIDC 身份源）。任何键未在后台配置时回退到 appsettings/环境变量里的 App:Oidc，
        // 后台改动经 UpdateAsync 即时生效，无需重启。
        public const string OidcEnabled = "Oidc.Enabled";
        public const string OidcAuthority = "Oidc.Authority";
        public const string OidcClientId = "Oidc.ClientId";
        public const string OidcClientSecret = "Oidc.ClientSecret";
        public const string OidcRedirectUri = "Oidc.RedirectUri";
        public const string OidcClientRedirectUri = "Oidc.ClientRedirectUri";
        public const string OidcScope = "Oidc.Scope";
        public const string OidcDisplayName = "Oidc.DisplayName";
        public const string OidcBootstrapClaim = "Oidc.BootstrapClaim";
        public const string OidcBootstrapValue = "Oidc.BootstrapValue";
        public const string OidcRequireHttpsMetadata = "Oidc.RequireHttpsMetadata";
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

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
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        return bool.TryParse(value, out bool result) ? result : defaultValue;
    }

    // Typed accessors with AppOptions fallback

    public string PizzaHelperUidSalt => GetOrDefault(Keys.PizzaHelperUidSalt, appOptions.GenshinPizzaHelper?.UidSalt ?? string.Empty);

    public string PizzaHelperApiSalt => GetOrDefault(Keys.PizzaHelperApiSalt, appOptions.GenshinPizzaHelper?.ApiSalt ?? string.Empty);

    public string PizzaHelperAvatarHoldingEndpoint => GetOrDefault(Keys.PizzaHelperAvatarHoldingEndpoint, appOptions.GenshinPizzaHelper?.EndPoints?.AvatarHolding ?? string.Empty);

    public string PizzaHelperSpiralAbyssEndpoint => GetOrDefault(Keys.PizzaHelperSpiralAbyssEndpoint, appOptions.GenshinPizzaHelper?.EndPoints?.SpiralAbyss ?? string.Empty);

    public string AfdianSkuGachaLog => GetOrDefault(Keys.AfdianSkuGachaLog, appOptions.Afdian?.SkuGachaLogUploadService ?? string.Empty);

    public string Afdian2SkuCdn => GetOrDefault(Keys.Afdian2SkuCdn, appOptions.Afdian2?.SkuCdnService ?? string.Empty);

    public string SmtpDiagnosticEmail => GetOrDefault(Keys.SmtpDiagnosticEmail, appOptions.Smtp?.DiagnosticEmailAddress ?? string.Empty);

    public bool MaintenanceMode => GetBool(Keys.MaintenanceMode, false);

    /// <summary>熔断：禁用客户端解锁帧率，经 /Patch/hutao 下发。</summary>
    public bool PatchDisableFpsUnlock => GetBool(Keys.PatchDisableFpsUnlock, false);

    /// <summary>熔断：禁用客户端 Island 注入，经 /Patch/hutao 下发。</summary>
    public bool PatchDisableIslandInjection => GetBool(Keys.PatchDisableIslandInjection, false);

    /// <summary>S3 直链签名：要签名的主机名（如 my-bucket.s3.us-east-1.amazonaws.com）。空 = 不启用。</summary>
    public string PatchS3Host => GetOrDefault(Keys.PatchS3Host, string.Empty).Trim();

    /// <summary>四项配置齐全才启用签名。</summary>
    public bool IsPatchS3SigningEnabled =>
        !string.IsNullOrWhiteSpace(PatchS3Host)
        && !string.IsNullOrWhiteSpace(GetOrDefault(Keys.PatchS3Region, string.Empty))
        && !string.IsNullOrWhiteSpace(GetOrDefault(Keys.PatchS3AccessKeyId, string.Empty))
        && !string.IsNullOrWhiteSpace(GetOrDefault(Keys.PatchS3SecretAccessKey, string.Empty));

    public bool RegistrationEnabled => GetBool(Keys.RegistrationEnabled, true);

    public bool CaptchaEnabled => GetBool(Keys.CaptchaEnabled, !string.IsNullOrWhiteSpace(appOptions.ReCaptchaKey));

    /// <summary>
    /// SSO 当前生效配置：后台覆盖值优先，留空的键回退 App:Oidc（appsettings/环境变量）。
    /// 每次读取现拼——管理端保存后下一次调用即生效。
    /// </summary>
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

    // Admin operations

    public async Task<Dictionary<string, string?>> GetAllAsync()
    {
        await InitializeAsync().ConfigureAwait(false);
        return new Dictionary<string, string?>(_cache, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<(bool Success, string Message)> UpdateAsync(Dictionary<string, string?> settings, int userId)
    {
        await InitializeAsync().ConfigureAwait(false);

        using IServiceScope scope = scopeFactory.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        List<string> keys = [.. settings.Keys];
        List<SystemSetting> existing = await db.SystemSettings
            .Where(s => keys.Contains(s.Key))
            .ToListAsync()
            .ConfigureAwait(false);

        Dictionary<string, SystemSetting> existingMap = existing.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
        DateTimeOffset now = timeProvider.GetUtcNow();

        foreach ((string key, string? value) in settings)
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

            _cache[key] = value;
        }

        await db.SaveChangesAsync().ConfigureAwait(false);
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
