using server.Models.Entity;
using System.Collections.Concurrent;

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

    public bool IsPatchS3SigningEnabled =>
        !string.IsNullOrWhiteSpace(PatchS3Host)
        && !string.IsNullOrWhiteSpace(GetOrDefault(Keys.PatchS3Region, string.Empty))
        && !string.IsNullOrWhiteSpace(GetOrDefault(Keys.PatchS3AccessKeyId, string.Empty))
        && !string.IsNullOrWhiteSpace(GetOrDefault(Keys.PatchS3SecretAccessKey, string.Empty));

    public bool RegistrationEnabled => GetBool(Keys.RegistrationEnabled, true);

    public bool CaptchaEnabled => GetBool(Keys.CaptchaEnabled, !string.IsNullOrWhiteSpace(appOptions.ReCaptchaKey) && !string.IsNullOrWhiteSpace(appOptions.ReCaptchaSiteKey));

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
