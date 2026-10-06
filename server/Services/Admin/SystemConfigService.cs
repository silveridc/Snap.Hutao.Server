using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using server.Models.Context;
using server.Models.Entity;
using server.Options;

namespace server.Services.Admin
{
    // Singleton
    public sealed class SystemConfigService(IServiceScopeFactory scopeFactory, AppOptions appOptions, ILogger<SystemConfigService> logger)
    {
        private readonly ConcurrentDictionary<string, string?> cache = new(StringComparer.OrdinalIgnoreCase);
        private volatile bool initialized;

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
        }

        public async Task InitializeAsync()
        {
            if (initialized) return;

            try
            {
                using IServiceScope scope = scopeFactory.CreateScope();
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                if (!await db.Database.CanConnectAsync().ConfigureAwait(false))
                {
                    logger.LogWarning("Database not available, SystemConfigService using defaults");
                    initialized = true;
                    return;
                }

                List<SystemSetting> settings = await db.SystemSettings.AsNoTracking().ToListAsync().ConfigureAwait(false);
                foreach (SystemSetting setting in settings)
                {
                    cache[setting.Key] = setting.Value;
                }

                logger.LogInformation("SystemConfigService loaded {Count} settings from database", settings.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load system settings from database");
            }

            initialized = true;
        }

        public string? Get(string key)
        {
            return cache.TryGetValue(key, out string? value) ? value : null;
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

        public bool RegistrationEnabled => GetBool(Keys.RegistrationEnabled, true);

        public bool CaptchaEnabled => GetBool(Keys.CaptchaEnabled, !string.IsNullOrWhiteSpace(appOptions.ReCaptchaKey));

        // Admin operations

        public async Task<Dictionary<string, string?>> GetAllAsync()
        {
            await InitializeAsync().ConfigureAwait(false);
            return new Dictionary<string, string?>(cache, StringComparer.OrdinalIgnoreCase);
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
            DateTimeOffset now = DateTimeOffset.UtcNow;

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

                cache[key] = value;
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

            cache.TryRemove(key, out _);

            return deleted > 0 ? (true, "Default value restored") : (false, "Configuration setting does not exist");
        }
    }
}
