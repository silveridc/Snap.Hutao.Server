using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using server.Models.Admin;
using server.Models.Entity;
using server.Models.GachaLog;
using server.Options;
using server.Services;
using server.Services.Admin;
using server.Services.Authorization;
using server.Services.Mail;
using server.Tests.Infrastructure;
using StackExchange.Redis;
using System.Globalization;
using System.Net;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class OperationalSettingsTests(HutaoServerFactory factory)
{
    private const string Mask = "********";
    private const string DiagnosticSubject = "SMTP configuration test";
    private const string DiagnosticHtml = "<p>This is a test email sent from the Snap Hutao admin panel.</p><p>No verification code or password reset link was generated.</p>";

    private static readonly string[] SmtpKeys =
    [
        Keys.SmtpServer, Keys.SmtpPort, Keys.SmtpSecurity, Keys.SmtpUserName,
        Keys.SmtpPassword, Keys.SmtpFromName, Keys.SmtpFromAddress,
        Keys.SmtpTimeoutSeconds, Keys.SmtpDiagnosticEmail,
    ];

    private static readonly string[] TimingKeys =
    [
        Keys.VerificationCodeExpireSeconds, Keys.VerificationResendCooldownSeconds,
        Keys.ResetPasswordLinkExpireSeconds,
    ];

    private static readonly string[] SecretKeys =
    [
        Keys.SmtpPassword, Keys.OidcClientSecret, Keys.PatchS3SecretAccessKey, Keys.RecaptchaSecretKey,
    ];

    private static readonly string[] DayKeys =
    [
        Keys.RegistrationGiftDays, Keys.GachaLogExtendDays, Keys.RedeemCodeExpireDays,
        Keys.JobLogRetentionDays, Keys.SpiralAbyssRecordRetentionDays,
    ];

    private readonly HutaoServerFactory factory = factory;
    private readonly HttpClient client = factory.CreateApiClient();
    private readonly SystemConfigService config = factory.GetService<SystemConfigService>();
    private static readonly bool[] booleanArray = [false, true];
    private static readonly string[] stringArray = ["Auto", "StartTls", "SslOnConnect", "None"];
    private static readonly string[] stringArray0 = ["not-an-email", "Name <recipient@example.com>", "a@example.com,b@example.com", "a@example.com\r\nBcc: b@example.com"];
    private static readonly bool[] booleanArray0 = [false, true];
    private static readonly string[] stringArray1 = ["http://localhost:8080/root/", "https://mail.example.test/path/"];

    [Fact]
    public async Task Get_MaterializesEffectiveDefaultsAndMasksAppConfigSecrets()
    {
        string[] keys = [.. SmtpKeys, .. TimingKeys, .. SecretKeys, .. DayKeys, Keys.GachaLogMaxUidPerUser,
            Keys.MaintenanceMode, Keys.RegistrationEnabled, Keys.CaptchaEnabled,
            Keys.RecaptchaSiteKey, Keys.RecaptchaMinScore,
            Keys.OidcEnabled, Keys.OidcRequireHttpsMetadata];
        await WithSettingsAsync(keys, async () =>
        {
            await DeleteSettingsAsync(keys);
            TestUser maintainer = await factory.Users.MaintainerAsync();
            TestResponse response = await SendAsync("GET", "/api/admin/settings", null, maintainer.AccessToken);
            Assert.Equal(ReturnCodes.Success, response.Retcode);
            Dictionary<string, string?> settings = response.DeserializeData<Dictionary<string, string?>>()!;
            AppOptions app = factory.GetService<AppOptions>();

            Assert.Equal("900", settings[Keys.VerificationCodeExpireSeconds]);
            Assert.Equal("60", settings[Keys.VerificationResendCooldownSeconds]);
            Assert.Equal("900", settings[Keys.ResetPasswordLinkExpireSeconds]);
            Assert.Equal("5", settings[Keys.GachaLogMaxUidPerUser]);
            Assert.Equal("3", settings[Keys.RegistrationGiftDays]);
            Assert.Equal("3", settings[Keys.GachaLogExtendDays]);
            Assert.Equal("3", settings[Keys.RedeemCodeExpireDays]);
            Assert.Equal("90", settings[Keys.JobLogRetentionDays]);
            Assert.Equal("30", settings[Keys.SpiralAbyssRecordRetentionDays]);
            Assert.Equal("0.5", settings[Keys.RecaptchaMinScore]);
            Assert.Equal(config.RecaptchaSiteKey, settings[Keys.RecaptchaSiteKey]);
            Assert.Equal(app.Smtp.Server, settings[Keys.SmtpServer]);
            Assert.Equal(app.Smtp.Port.ToString(CultureInfo.InvariantCulture), settings[Keys.SmtpPort]);
            Assert.Equal(app.Smtp.Security, settings[Keys.SmtpSecurity]);
            Assert.Equal(app.Smtp.UserName, settings[Keys.SmtpUserName]);
            Assert.Equal(app.Smtp.FromName, settings[Keys.SmtpFromName]);
            Assert.Equal(app.Smtp.FromAddress, settings[Keys.SmtpFromAddress]);
            Assert.Equal(app.Smtp.TimeoutSeconds.ToString(CultureInfo.InvariantCulture), settings[Keys.SmtpTimeoutSeconds]);
            Assert.Equal("false", settings[Keys.MaintenanceMode]);
            Assert.Equal("true", settings[Keys.RegistrationEnabled]);
            Assert.Equal(config.CaptchaEnabled ? "true" : "false", settings[Keys.CaptchaEnabled]);
            Assert.Equal("false", settings[Keys.OidcEnabled]);
            Assert.Equal("true", settings[Keys.OidcRequireHttpsMetadata]);
            Assert.False(string.IsNullOrWhiteSpace(app.Smtp.Password));
            Assert.Equal(Mask, settings[Keys.SmtpPassword]);
            Assert.Null(settings.GetValueOrDefault(Keys.OidcClientSecret));
            Assert.Null(settings.GetValueOrDefault(Keys.PatchS3SecretAccessKey));
            Assert.DoesNotContain(app.Smtp.Password, response.Data.GetRawText());
            if (!string.IsNullOrWhiteSpace(app.ReCaptcha.Key))
            {
                Assert.Equal(Mask, settings[Keys.RecaptchaSecretKey]);
                Assert.DoesNotContain(app.ReCaptcha.Key, response.Data.GetRawText(), StringComparison.Ordinal);
            }
            Assert.All(response.Data.EnumerateObject(), property => Assert.True(property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null));
            Assert.Equal(900, config.VerificationCodeExpireSeconds);
            Assert.Equal(60, config.VerificationResendCooldownSeconds);
            Assert.Equal(900, config.ResetPasswordLinkExpireSeconds);
            Assert.Equal(5, config.GachaLogMaxUidPerUser);
            Assert.Equal(3, config.RegistrationGiftDays);
            Assert.Equal(3, config.GachaLogExtendDays);
            Assert.Equal(3, config.RedeemCodeExpireDays);
            Assert.Equal(90, config.JobLogRetentionDays);
            Assert.Equal(30, config.SpiralAbyssRecordRetentionDays);
            Assert.Equal(0.5f, config.RecaptchaMinScore);
        });
    }

    [Fact]
    public async Task SecretPatch_BlankNullAndMaskKeepSavedValues_AndDeleteRestoresFallback()
    {
        await WithSettingsAsync(SecretKeys, async () =>
        {
            TestUser maintainer = await factory.Users.MaintainerAsync();
            Dictionary<string, string?> secrets = SecretKeys.ToDictionary(key => key, key => (string?)$"secret-{key}-\"\\-密钥-{Guid.NewGuid():N}");
            Assert.Equal(ReturnCodes.Success, (await PatchAsync(secrets, maintainer)).Retcode);
            Dictionary<string, SystemSetting> before = await ReadStoredAsync(SecretKeys);

            foreach (string? keep in new string?[] { string.Empty, "   ", null, Mask })
            {
                Assert.Equal(ReturnCodes.Success, (await PatchAsync(SecretKeys.ToDictionary(key => key, _ => keep), maintainer)).Retcode);
                Dictionary<string, SystemSetting> after = await ReadStoredAsync(SecretKeys);
                foreach (string key in SecretKeys)
                {
                    Assert.Equal(secrets[key], config.Get(key));
                    Assert.Equal(secrets[key], after[key].Value);
                    Assert.Equal(before[key].UpdatedAt, after[key].UpdatedAt);
                }
            }

            Dictionary<string, string?> displayed = await GetSettingsAsync(maintainer);
            foreach (string key in SecretKeys)
            {
                Assert.Equal(Mask, displayed[key]);
            }

            TestResponse deleted = await SendAsync("DELETE", $"/api/admin/settings/{Keys.SmtpPassword}", null, maintainer.AccessToken);
            Assert.Equal(ReturnCodes.Success, deleted.Retcode);
            Assert.Null(config.Get(Keys.SmtpPassword));
            Assert.Equal(factory.GetService<AppOptions>().Smtp.Password, config.Smtp.Password);
            Assert.Equal(Mask, (await GetSettingsAsync(maintainer))[Keys.SmtpPassword]);
            Assert.DoesNotContain(Keys.SmtpPassword, (await ReadStoredAsync(SecretKeys)).Keys);

            await DeleteSettingsAsync([Keys.OidcClientSecret, Keys.PatchS3SecretAccessKey]);
            displayed = await GetSettingsAsync(maintainer);
            Assert.Null(displayed.GetValueOrDefault(Keys.PatchS3SecretAccessKey));
            // OIDC 凭据只存在 system_config，删除后不再有配置文件兜底
            Assert.Null(displayed.GetValueOrDefault(Keys.OidcClientSecret));
        });
    }

    [Fact]
    public async Task IntegerSettings_AcceptRangeEndpointsAndImmediatelyUpdateTypedGetters()
    {
        (string Key, int Minimum, int Maximum)[] ranges =
        [
            (Keys.VerificationCodeExpireSeconds, 60, 3600),
            (Keys.VerificationResendCooldownSeconds, 10, 600),
            (Keys.ResetPasswordLinkExpireSeconds, 60, 3600),
            (Keys.GachaLogMaxUidPerUser, 1, 50),
            (Keys.RegistrationGiftDays, 1, 365),
            (Keys.GachaLogExtendDays, 1, 365),
            (Keys.RedeemCodeExpireDays, 1, 365),
            (Keys.JobLogRetentionDays, 7, 3650),
            (Keys.SpiralAbyssRecordRetentionDays, 7, 3650),
            (Keys.SmtpPort, 1, 65535),
            (Keys.SmtpTimeoutSeconds, 1, 120),
        ];
        await WithSettingsAsync(ranges.Select(range => range.Key), async () =>
        {
            TestUser maintainer = await factory.Users.MaintainerAsync();
            foreach (bool maximum in booleanArray)
            {
                Dictionary<string, string?> values = ranges.ToDictionary(range => range.Key, range => (string?)(maximum ? range.Maximum : range.Minimum).ToString(CultureInfo.InvariantCulture));
                Assert.Equal(ReturnCodes.Success, (await PatchAsync(values, maintainer)).Retcode);
                Dictionary<string, string?> displayed = await GetSettingsAsync(maintainer);
                Dictionary<string, SystemSetting> stored = await ReadStoredAsync(values.Keys);
                foreach ((string key, string? value) in values)
                {
                    Assert.Equal(value, displayed[key]);
                    Assert.Equal(value, config.Get(key));
                    Assert.Equal(value, stored[key].Value);
                }

                Assert.Equal(maximum ? 3600 : 60, config.VerificationCodeExpireSeconds);
                Assert.Equal(maximum ? 600 : 10, config.VerificationResendCooldownSeconds);
                Assert.Equal(maximum ? 3600 : 60, config.ResetPasswordLinkExpireSeconds);
                Assert.Equal(maximum ? 50 : 1, config.GachaLogMaxUidPerUser);
                Assert.Equal(maximum ? 365 : 1, config.RegistrationGiftDays);
                Assert.Equal(maximum ? 365 : 1, config.GachaLogExtendDays);
                Assert.Equal(maximum ? 365 : 1, config.RedeemCodeExpireDays);
                Assert.Equal(maximum ? 3650 : 7, config.JobLogRetentionDays);
                Assert.Equal(maximum ? 3650 : 7, config.SpiralAbyssRecordRetentionDays);
                Assert.Equal(maximum ? 65535 : 1, config.Smtp.Port);
                Assert.Equal(maximum ? 120 : 1, config.Smtp.TimeoutSeconds);
            }
        });
    }

    [Fact]
    public async Task SecurityAndPublicBaseUrl_AcceptSupportedModesAndHttpSchemes()
    {
        await WithSettingsAsync([Keys.SmtpSecurity, Keys.MailPublicBaseUrl], async () =>
        {
            TestUser maintainer = await factory.Users.MaintainerAsync();
            foreach (string security in stringArray)
            {
                Assert.Equal(ReturnCodes.Success, (await PatchAsync(new() { [Keys.SmtpSecurity] = security }, maintainer)).Retcode);
                Assert.Equal(security, config.Smtp.Security);
            }

            foreach (string url in stringArray1)
            {
                Assert.Equal(ReturnCodes.Success, (await PatchAsync(new() { [Keys.MailPublicBaseUrl] = url }, maintainer)).Retcode);
                Assert.Equal(url.TrimEnd('/'), config.MailPublicBaseUrl);
            }
        });
    }

    [Fact]
    public async Task InvalidBatch_IsRejectedWithoutPartialDatabaseOrCacheWrites()
    {
        List<(string Key, string Value)> invalid = [];
        foreach ((string key, string[] values) in new (string, string[])[]
        {
            (Keys.VerificationCodeExpireSeconds, ["59", "3601", "61", "3599", "abc", "999999999999999"]),
            (Keys.ResetPasswordLinkExpireSeconds, ["59", "3601", "61", "3599"]),
            (Keys.VerificationResendCooldownSeconds, ["9", "601", "1.5"]),
            (Keys.GachaLogMaxUidPerUser, ["0", "51", "NaN"]),
            (Keys.RegistrationGiftDays, ["0", "366", "1.5"]),
            (Keys.GachaLogExtendDays, ["0", "366"]),
            (Keys.RedeemCodeExpireDays, ["0", "366"]),
            (Keys.JobLogRetentionDays, ["6", "3651"]),
            (Keys.SpiralAbyssRecordRetentionDays, ["6", "3651"]),
            (Keys.RecaptchaMinScore, ["-0.1", "1.1", "abc"]),
            (Keys.RecaptchaSiteKey, ["bad key", "<script>", "site-key?x=1"]),
            (Keys.SmtpPort, ["0", "65536", "abc"]),
            (Keys.SmtpTimeoutSeconds, ["0", "121", "1.5"]),
            (Keys.SmtpSecurity, ["StartTlsWhenAvailable", "bogus"]),
            (Keys.SmtpServer, ["smtp://localhost", "localhost:25", "bad host"]),
            (Keys.SmtpFromAddress, ["not-an-email", "Name <sender@example.com>", "a@example.com,b@example.com"]),
            (Keys.SmtpDiagnosticEmail, ["not-an-email"]),
            (Keys.SmtpFromName, ["injected\r\nheader"]),
            (Keys.MailPublicBaseUrl, ["/relative", "ftp://example.com", "https://example.com/?x=1", "https://example.com/#fragment", "https://user:password@example.com"]),
        })
        {
            invalid.AddRange(values.Select(value => (key, value)));
        }

        string markerKey = $"HutaoTest.Atomic.{Guid.NewGuid():N}";
        string[] keys = [.. invalid.Select(item => item.Key), Keys.MailSystemName, markerKey];
        await WithSettingsAsync(keys, async () =>
        {
            TestUser maintainer = await factory.Users.MaintainerAsync();
            await SaveAsync(new() { [Keys.MailSystemName] = "Atomic baseline" });
            await config.DeleteAsync(markerKey);
            Dictionary<string, string?> cached = keys.Distinct().ToDictionary(key => key, key => config.Get(key));
            Dictionary<string, SystemSetting> stored = await ReadStoredAsync(keys);

            foreach ((string key, string value) in invalid)
            {
                TestResponse rejected = await PatchAsync(new()
                {
                    [Keys.MailSystemName] = "must not change",
                    [markerKey] = "must not insert",
                    [key] = value,
                }, maintainer);
                Assert.True(rejected.Retcode == ReturnCodes.InvalidUploadData, $"{key}={value} was accepted: {rejected.Message}");
                Dictionary<string, SystemSetting> after = await ReadStoredAsync(keys);
                Assert.Equal(stored.Count, after.Count);
                foreach (string savedKey in cached.Keys)
                {
                    Assert.Equal(cached[savedKey], config.Get(savedKey));
                    Assert.Equal(stored.GetValueOrDefault(savedKey)?.Value, after.GetValueOrDefault(savedKey)?.Value);
                    Assert.Equal(stored.GetValueOrDefault(savedKey)?.UpdatedAt, after.GetValueOrDefault(savedKey)?.UpdatedAt);
                }
            }
        });
    }

    [Fact]
    public async Task Audit_RedactsDottedSecretsWithEscapesAndUnicodeBeforeTruncation()
    {
        string markerKey = $"HutaoTest.Audit.{Guid.NewGuid():N}";
        await WithSettingsAsync([.. SecretKeys, markerKey], async () =>
        {
            TestUser maintainer = await factory.Users.MaintainerAsync();
            string marker = Guid.NewGuid().ToString("N");
            Dictionary<string, string?> values = new() { [markerKey] = marker };
            foreach (string key in SecretKeys)
            {
                values[key] = $"secret-start-{key}-\"\\-密钥-{new string('x', 3500)}-secret-end";
            }

            Assert.Equal(ReturnCodes.Success, (await PatchAsync(values, maintainer)).Retcode);
            AdminAuditLog? audit = await factory.WithDbContextAsync(db => db.AdminAuditLogs.AsNoTracking()
                .Where(log => log.Action == "/api/admin/settings" && log.Detail != null && log.Detail.Contains(marker))
                .OrderByDescending(log => log.Id).FirstOrDefaultAsync());
            Assert.NotNull(audit);
            Assert.NotNull(audit.Detail);
            Assert.True(audit.Detail.Length <= 2000);
            using JsonDocument detail = JsonDocument.Parse(audit.Detail);
            JsonElement settings = detail.RootElement.GetProperty("settings");
            Assert.Equal(marker, settings.GetProperty(markerKey).GetString());
            Dictionary<string, SystemSetting> stored = await ReadStoredAsync(SecretKeys);
            foreach (string key in SecretKeys)
            {
                Assert.Equal("***", settings.GetProperty(key).GetString());
                Assert.DoesNotContain($"secret-start-{key}", audit.Detail);
                Assert.Equal(values[key], stored[key].Value);
                Assert.Equal(values[key], config.Get(key));
            }
        });
    }

    [Fact]
    public async Task MailService_UsesANewSavedTransportAndSenderSnapshotForTheNextSend()
    {
        MailService mail = factory.GetService<MailService>();
        int alternatePort = TestEnvironment.FindFreePort();
        using FakeSmtpServer alternate = new(alternatePort);
        await WithSettingsAsync(SmtpKeys, async () =>
        {
            try
            {
                await DeleteSettingsAsync(SmtpKeys);
                SmtpOptions fallback = config.Smtp;
                Assert.Equal("127.0.0.1", fallback.Server);
                Assert.Equal(TestEnvironment.SmtpPort, fallback.Port);
                factory.Smtp.Clear();
                await mail.SendRegistrationVerifyCodeAsync("first-send@example.com", "FIRST123", "EN");
                MimeMessage first = ReadMessage(factory.Smtp);
                Assert.Equal("first-send@example.com", Assert.Single(first.To.Mailboxes).Address);
                Assert.Equal(fallback.FromName, Assert.Single(first.From.Mailboxes).Name);
                Assert.Equal(string.IsNullOrWhiteSpace(fallback.FromAddress) ? fallback.UserName : fallback.FromAddress, Assert.Single(first.From.Mailboxes).Address);

                TestUser maintainer = await factory.Users.MaintainerAsync();
                Dictionary<string, string?> overrides = new()
                {
                    [Keys.SmtpServer] = "localhost",
                    [Keys.SmtpPort] = alternatePort.ToString(CultureInfo.InvariantCulture),
                    [Keys.SmtpSecurity] = "None",
                    [Keys.SmtpUserName] = "different-login@example.com",
                    [Keys.SmtpPassword] = "different-local-password",
                    [Keys.SmtpFromName] = "运营邮件 Hutao",
                    [Keys.SmtpFromAddress] = "different-sender@example.com",
                    [Keys.SmtpTimeoutSeconds] = "3",
                };
                Assert.Equal(ReturnCodes.Success, (await PatchAsync(overrides, maintainer)).Retcode);
                await mail.SendRegistrationVerifyCodeAsync("second-send@example.com", "NEXT1234", "EN");
                MimeMessage second = ReadMessage(alternate);
                Assert.Single(factory.Smtp.Messages);
                Assert.Equal("second-send@example.com", Assert.Single(second.To.Mailboxes).Address);
                Assert.Equal(overrides[Keys.SmtpFromName], Assert.Single(second.From.Mailboxes).Name);
                Assert.Equal(overrides[Keys.SmtpFromAddress], Assert.Single(second.From.Mailboxes).Address);
                Assert.Equal("localhost", config.Smtp.Server);
                Assert.Equal(alternatePort, config.Smtp.Port);
                Assert.Equal(3, config.Smtp.TimeoutSeconds);
            }
            finally
            {
                factory.Smtp.Clear();
            }
        });
    }

    [Fact]
    public async Task DiagnosticMail_UsesSavedSettingsExplicitOrFallbackRecipient_WithoutAuthStateOrCustomShell()
    {
        int port = TestEnvironment.FindFreePort();
        using FakeSmtpServer smtp = new(port);
        await WithSettingsAsync([.. SmtpKeys, Keys.MailTemplateHtml], async () =>
        {
            TestUser maintainer = await factory.Users.MaintainerAsync();
            Dictionary<string, string?> settings = LocalSmtpSettings(port);
            settings[Keys.SmtpDiagnosticEmail] = "diagnostic-fallback@example.com";
            settings[Keys.MailTemplateHtml] = "<html>custom-shell-marker {{options.Title}} {{options.RawContent}} {{options.Footer}} {{sysname}} {{sysHost}}</html>";
            Assert.Equal(ReturnCodes.Success, (await PatchAsync(settings, maintainer)).Retcode);
            using ConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync($"127.0.0.1:{factory.RedisPort}");
            Dictionary<string, string?> before = await ReadAuthStateAsync(redis);

            foreach (string? address in new string?[] { "diagnostic-explicit@example.com", null })
            {
                smtp.Clear();
                TestResponse sent = await SendAsync("POST", "/api/admin/mail/test", new MailTestRequest { Address = address }, maintainer.AccessToken);
                Assert.Equal(ReturnCodes.Success, sent.Retcode);
                Assert.Equal(ServerKeys.ServerAdminMailTestSucceed, sent.L10nKey);
                MimeMessage message = ReadMessage(smtp);
                Assert.Equal(address ?? settings[Keys.SmtpDiagnosticEmail], Assert.Single(message.To.Mailboxes).Address);
                Assert.Equal(settings[Keys.SmtpFromAddress], Assert.Single(message.From.Mailboxes).Address);
                Assert.Equal(settings[Keys.SmtpFromName], Assert.Single(message.From.Mailboxes).Name);
                Assert.Equal(DiagnosticSubject, message.Subject);
                Assert.Equal(DiagnosticHtml, message.HtmlBody?.Trim());
                Assert.DoesNotContain("custom-shell-marker", message.HtmlBody ?? string.Empty);
                Dictionary<string, string?> after = await ReadAuthStateAsync(redis);
                Assert.Equal(before.OrderBy(pair => pair.Key), after.OrderBy(pair => pair.Key));
            }
        });
    }

    [Fact]
    public async Task DiagnosticMail_InvalidAddressDoesNotSend_AndSmtpFailureReturnsGenericError()
    {
        await WithSettingsAsync(SmtpKeys, async () =>
        {
            TestUser maintainer = await factory.Users.MaintainerAsync();
            await SaveAsync(LocalSmtpSettings(TestEnvironment.SmtpPort));
            factory.Smtp.Clear();
            try
            {
                foreach (string address in stringArray0)
                {
                    TestResponse invalid = await SendAsync("POST", "/api/admin/mail/test", new MailTestRequest { Address = address }, maintainer.AccessToken);
                    Assert.Equal(ReturnCodes.InvalidUploadData, invalid.Retcode);
                    Assert.Equal(ServerKeys.ServerAdminMailTestAddressInvalid, invalid.L10nKey);
                    Assert.Empty(factory.Smtp.Messages);
                }

                const string secret = "failure-password-should-not-escape";
                await SaveAsync(new()
                {
                    [Keys.SmtpPort] = TestEnvironment.FindFreePort().ToString(CultureInfo.InvariantCulture),
                    [Keys.SmtpPassword] = secret,
                    [Keys.SmtpTimeoutSeconds] = "1",
                });
                TestResponse failed = await SendAsync("POST", "/api/admin/mail/test", new MailTestRequest { Address = "failure@example.com" }, maintainer.AccessToken);
                Assert.Equal(ReturnCodes.InternalStateException, failed.Retcode);
                Assert.Equal(ServerKeys.ServerAdminMailTestFailed, failed.L10nKey);
                Assert.DoesNotContain(secret, failed.Message ?? string.Empty);
                Assert.Empty(factory.Smtp.Messages);
            }
            finally
            {
                factory.Smtp.Clear();
            }
        });
    }

    [Fact]
    public async Task DiagnosticMail_IsWriteGatedWhenSsoIsEnabled()
    {
        await WithSettingsAsync([.. SmtpKeys, Keys.OidcEnabled], async () =>
        {
            TestUser legacy = await factory.Users.CreateUniqueAsync("diagnostic-readonly", user => user.IsMaintainer = true);
            Dictionary<string, string?> settings = LocalSmtpSettings(TestEnvironment.SmtpPort);
            settings[Keys.OidcEnabled] = "true";
            await SaveAsync(settings);
            factory.Smtp.Clear();
            try
            {
                TestResponse rejected = await SendAsync("POST", "/api/admin/mail/test", new MailTestRequest { Address = "never-sent@example.com" }, legacy.AccessToken);
                Assert.Equal(ReturnCodes.WriteRequiresSsoAdmin, rejected.Retcode);
                Assert.Empty(factory.Smtp.Messages);
            }
            finally
            {
                factory.Smtp.Clear();
            }
        });
    }

    [Fact]
    public async Task VerificationAndResetTokenTtls_UseDefaultsThenSavedSettingsInNewScopes()
    {
        await WithSettingsAsync(TimingKeys, async () =>
        {
            await DeleteSettingsAsync(TimingKeys);
            using ConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync($"127.0.0.1:{factory.RedisPort}");
            IDatabase db = redis.GetDatabase(15);
            List<RedisKey> created = [];
            try
            {
                foreach (bool customized in booleanArray0)
                {
                    if (customized)
                    {
                        TestUser maintainer = await factory.Users.MaintainerAsync();
                        Assert.Equal(ReturnCodes.Success, (await PatchAsync(new()
                        {
                            [Keys.VerificationCodeExpireSeconds] = "120",
                            [Keys.VerificationResendCooldownSeconds] = "30",
                            [Keys.ResetPasswordLinkExpireSeconds] = "240",
                        }, maintainer)).Retcode);
                    }

                    using IServiceScope scope = factory.Services.CreateScope();
                    PassportVerificationService verification = scope.ServiceProvider.GetRequiredService<PassportVerificationService>();
                    string userName = $"operational-ttl-{Guid.NewGuid():N}@example.com";
                    string normalized = userName.ToUpperInvariant();
                    RedisKey codeKey = $"{normalized}.{PassportVerificationService.Registration}";
                    RedisKey codeCooldown = $"{codeKey}.regenerate";
                    RedisKey pointer = $"{normalized}.{PassportVerificationService.ResetPassword}.token";
                    RedisKey resetCooldown = $"{pointer}.regenerate";
                    created.AddRange([codeKey, codeCooldown, pointer, resetCooldown]);
                    string code = verification.GenerateVerifyCode(normalized, PassportVerificationService.Registration);
                    Assert.Equal(code, (string?)await db.StringGetAsync(codeKey));
                    await AssertTtlAsync(db, codeKey, customized ? 120 : 900);
                    await AssertTtlAsync(db, codeCooldown, customized ? 30 : 60);
                    Assert.True(verification.TryGetNonExpiredVerifyCode(normalized, PassportVerificationService.Registration, out string? unchanged));
                    Assert.Equal(code, unchanged);

                    Assert.True(verification.TryGenerateResetToken(normalized, userName, out string token));
                    RedisKey tokenKey = $"ResetToken.{token}";
                    created.Add(tokenKey);
                    Assert.Equal(userName, (string?)await db.StringGetAsync(tokenKey));
                    Assert.Equal(token, (string?)await db.StringGetAsync(pointer));
                    await AssertTtlAsync(db, tokenKey, customized ? 240 : 900);
                    await AssertTtlAsync(db, pointer, customized ? 240 : 900);
                    await AssertTtlAsync(db, resetCooldown, customized ? 30 : 60);
                    Assert.False(verification.TryGenerateResetToken(normalized, userName, out string blocked));
                    Assert.Empty(blocked);
                    Assert.True(verification.TryPeekResetToken(token, out string owner, out TimeSpan? remaining));
                    Assert.Equal(userName, owner);
                    Assert.NotNull(remaining);
                }
            }
            finally
            {
                await db.KeyDeleteAsync([.. created]);
            }
        });
    }

    [Fact]
    public async Task LoweredGachaCap_BlocksOnlyNewUids_AndRaisingItTakesEffectImmediately()
    {
        await WithSettingsAsync([Keys.GachaLogMaxUidPerUser], async () =>
        {
            TestUser maintainer = await factory.Users.MaintainerAsync();
            TestUser owner = await factory.Users.CreateUniqueAsync("operational-gacha", user => user.GachaLogExpireAt = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds());
            string[] uids = [.. Enumerable.Range(0, 3).Select(_ => Random.Shared.Next(700_000_000, 799_999_999).ToString(CultureInfo.InvariantCulture))];
            try
            {
                Assert.Equal(ReturnCodes.Success, (await PatchAsync(new() { [Keys.GachaLogMaxUidPerUser] = "2" }, maintainer)).Retcode);
                foreach (string uid in uids.Take(2))
                {
                    Assert.Equal(ReturnCodes.Success, (await SendAsync("POST", "/api/gacha-logs", TestData.GachaItems(uid, 1), owner.AccessToken)).Retcode);
                }

                Assert.Equal(ReturnCodes.Success, (await PatchAsync(new() { [Keys.GachaLogMaxUidPerUser] = "1" }, maintainer)).Retcode);
                Assert.Equal(ReturnCodes.Success, (await SendAsync("POST", "/api/gacha-logs", TestData.GachaItems(uids[0], 1), owner.AccessToken)).Retcode);
                TestResponse rejected = await SendAsync("POST", "/api/gacha-logs", TestData.GachaItems(uids[2], 1), owner.AccessToken);
                Assert.Equal(ReturnCodes.TooManyGachaLogUids, rejected.Retcode);
                Assert.Equal("A Snap Hutao account can store gacha logs of at most 1 Uids", rejected.Message);
                Assert.Equal(ServerKeys.ServerGachaLogServiceInsufficientRecordSlot, rejected.L10nKey);
                TestResponse entries = await SendAsync("GET", "/api/gacha-logs/entries", null, owner.AccessToken);
                Assert.Equal(2, entries.DeserializeData<List<GachaEntry>>()!.Count);
                Assert.DoesNotContain(entries.DeserializeData<List<GachaEntry>>()!, entry => entry.Uid == uids[2]);

                Assert.Equal(ReturnCodes.Success, (await PatchAsync(new() { [Keys.GachaLogMaxUidPerUser] = "3" }, maintainer)).Retcode);
                Assert.Equal(ReturnCodes.Success, (await SendAsync("POST", "/api/gacha-logs", TestData.GachaItems(uids[2], 1), owner.AccessToken)).Retcode);
            }
            finally
            {
                foreach (string uid in uids)
                {
                    await SendAsync("DELETE", $"/api/gacha-logs/{uid}", null, owner.AccessToken);
                }
            }
        });
    }

    private async Task<TestResponse> SendAsync(string method, string url, object? body, string? token)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), url);
        request.WithToken(token).WithDevice().WithClientVersion();
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode is not HttpStatusCode.InternalServerError, $"{method} {url} returned 500: {json}");
        return TestResponse.Parse(json);
    }

    private Task<TestResponse> PatchAsync(Dictionary<string, string?> settings, TestUser maintainer)
    {
        return SendAsync("PATCH", "/api/admin/settings", settings, maintainer.AccessToken);
    }

    private async Task<Dictionary<string, string?>> GetSettingsAsync(TestUser maintainer)
    {
        TestResponse response = await SendAsync("GET", "/api/admin/settings", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, response.Retcode);
        return response.DeserializeData<Dictionary<string, string?>>()!;
    }

    private async Task SaveAsync(Dictionary<string, string?> settings)
    {
        (bool success, string message) = await config.UpdateAsync(settings, 0);
        Assert.True(success, message);
    }

    private Task<Dictionary<string, SystemSetting>> ReadStoredAsync(IEnumerable<string> keys)
    {
        string[] selected = [.. keys.Distinct()];
        return factory.WithDbContextAsync(db => db.SystemSettings.AsNoTracking()
            .Where(setting => selected.Contains(setting.Key)).ToDictionaryAsync(setting => setting.Key));
    }

    private async Task WithSettingsAsync(IEnumerable<string> keys, Func<Task> action)
    {
        await config.InitializeAsync();
        Dictionary<string, string?> originals = keys.Distinct().ToDictionary(key => key, key => config.Get(key));
        try
        {
            await action();
        }
        finally
        {
            foreach ((string key, string? original) in originals)
            {
                if (original is null || (SecretKeys.Contains(key) && string.IsNullOrWhiteSpace(original)))
                {
                    await config.DeleteAsync(key);
                }
                else
                {
                    await SaveAsync(new() { [key] = original });
                }
            }
        }
    }

    private async Task DeleteSettingsAsync(IEnumerable<string> keys)
    {
        foreach (string key in keys.Distinct())
        {
            await config.DeleteAsync(key);
        }
    }

    private static Dictionary<string, string?> LocalSmtpSettings(int port)
    {
        return new()
        {
            [Keys.SmtpServer] = "127.0.0.1",
            [Keys.SmtpPort] = port.ToString(CultureInfo.InvariantCulture),
            [Keys.SmtpSecurity] = "None",
            [Keys.SmtpUserName] = "local-login@example.com",
            [Keys.SmtpPassword] = "local-test-password",
            [Keys.SmtpFromName] = "Local diagnostic sender",
            [Keys.SmtpFromAddress] = "local-sender@example.com",
            [Keys.SmtpTimeoutSeconds] = "3",
        };
    }

    private static MimeMessage ReadMessage(FakeSmtpServer smtp)
    {
        string raw = Assert.Single(smtp.Messages);
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(raw));
        return MimeMessage.Load(stream);
    }

    private static async Task AssertTtlAsync(IDatabase db, RedisKey key, int seconds)
    {
        TimeSpan? ttl = await db.KeyTimeToLiveAsync(key);
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value.TotalSeconds, seconds - 10, seconds);
    }

    private static async Task<Dictionary<string, string?>> ReadAuthStateAsync(ConnectionMultiplexer redis)
    {
        IDatabase db = redis.GetDatabase(15);
        IServer server = redis.GetServer(redis.GetEndPoints()[0]);
        Dictionary<string, string?> values = [];
        foreach (RedisKey key in server.Keys(database: 15))
        {
            values[key.ToString()] = (string?)await db.StringGetAsync(key);
        }

        return values;
    }
}
