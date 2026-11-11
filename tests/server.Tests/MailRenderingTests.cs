using MimeKit;
using server.Models.Mail;
using server.Services;
using server.Services.Admin;
using server.Services.Mail;
using server.Tests.Infrastructure;
using System.Text;
using System.Text.Json;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class MailRenderingTests(HutaoServerFactory factory)
{
    private const string ChsLanguage = "CHS";
    private const string Recipient = "mail-render@example.com";
    private const string DefaultSystemHost = "https://snap-api.sliveridc1.cn";

    private readonly HutaoServerFactory factory = factory;
    private readonly MailService mailService = factory.GetService<MailService>();
    private readonly SystemConfigService systemConfig = factory.GetService<SystemConfigService>();

    private Task SendAsync(string kind, string language, string value)
    {
        return kind switch
        {
            "RegisterCode" => mailService.SendRegistrationVerifyCodeAsync(Recipient, value, language),
            "ResetPasswordLink" => mailService.SendResetPasswordLinkAsync(Recipient, value, language),
            "ResetUsernameCode" => mailService.SendResetUsernameVerifyCodeAsync(Recipient, value, language),
            "CancelRegistrationCode" => mailService.SendCancelRegistrationVerifyCodeAsync(Recipient, value, language),
            "GachaStorageRedeem" => mailService.SendPurchaseGachaLogStorageServiceNoSuchUserAsync(Recipient, value, $"TN{value}", language),
            "CdnRedeem" => mailService.SendPurchaseCdnServiceNoSuchUserAsync(Recipient, value, $"TN{value}", language),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown mail kind"),
        };
    }

    private async Task<MimeMessage> WaitForMessageAsync()
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            string? raw = factory.Smtp.Messages.FirstOrDefault();
            if (raw is not null)
            {
                using MemoryStream stream = new(Encoding.UTF8.GetBytes(raw));
                return MimeMessage.Load(stream);
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("No mail was received by the fake SMTP server within 30 seconds.");
    }

    /// <summary>写入配置并在用例结束后恢复原值，避免污染其它用例.</summary>
    private async Task WithSettingsAsync(Dictionary<string, string?> settings, Func<Task> action)
    {
        Dictionary<string, string?> originals = settings.Keys.ToDictionary(key => key, key => systemConfig.Get(key));
        try
        {
            (bool success, _) = await systemConfig.UpdateAsync(settings, 0);
            Assert.True(success);
            await action();
        }
        finally
        {
            foreach ((string key, string? original) in originals)
            {
                if (original is null)
                {
                    await systemConfig.DeleteAsync(key);
                }
                else
                {
                    await systemConfig.UpdateAsync(new() { [key] = original }, 0);
                }
            }
        }
    }

    [Fact]
    public async Task BuiltInContent_RendersWithoutUnresolvedPlaceholders_ForEveryKindAndLanguage()
    {
        foreach (MailContentKind kind in MailContents.Kinds)
        {
            foreach (string language in MailContents.Languages)
            {
                string value = $"{kind.Key}-{language}-token";
                factory.Smtp.Clear();

                await SendAsync(kind.Key, language, value);
                MimeMessage message = await WaitForMessageAsync();

                string html = message.HtmlBody ?? string.Empty;
                Assert.NotEmpty(html);

                // 业务变量与自动注入的系统变量都必须已替换
                Assert.DoesNotContain("{{", message.Subject);
                Assert.DoesNotContain("{{", html);
                Assert.Contains(value, html);
                Assert.Contains("Snap Hutao", message.Subject);
                Assert.Contains("Snap Hutao", html);

                if (language == ChsLanguage)
                {
                    Assert.Contains("本邮件为系统自动发送邮件", html);
                }
                else
                {
                    Assert.Contains("This email was sent automatically by the system", html);
                }

                if (kind.Key is "GachaStorageRedeem" or "CdnRedeem")
                {
                    Assert.Contains($"TN{value}", html);

                    // 兑换码有效期天数由 Redeem.CodeExpireDays 注入，文案里不写死数字
                    Assert.Contains($"<span class=\"mail-hint\">{systemConfig.RedeemCodeExpireDays} ", html);
                }

                if (kind.Key is "ResetPasswordLink")
                {
                    // 中英文模板都必须给出可点击的绝对链接，而不是相对路径
                    Assert.Contains($"{DefaultSystemHost}/reset-password.html?token={value}", html);
                }
            }
        }
    }

    [Fact]
    public async Task CustomContent_InjectsSystemVariablesAndHtmlEncodesValues()
    {
        MailContent custom = new()
        {
            Subject = "{{sysname}} 重置",
            Title = "{{sysname}} 标题",
            RawContent = "<p>{{sysname}}</p><p>{{sysHost}}/reset-password.html?token={{token}}</p>",
            Footer = "{{sysname}} 页脚",
        };

        Dictionary<string, string?> settings = new()
        {
            [Keys.MailSystemName] = "Hutao & Co",
            [Keys.MailPublicBaseUrl] = "https://example.test/root/",
            ["MailContent.ResetPasswordLink.CHS"] = JsonSerializer.Serialize(custom),
        };

        await WithSettingsAsync(settings, async () =>
        {
            factory.Smtp.Clear();

            await mailService.SendResetPasswordLinkAsync(Recipient, "abc123DEF", ChsLanguage);
            MimeMessage message = await WaitForMessageAsync();

            string html = message.HtmlBody ?? string.Empty;

            // SMTP 主题是纯文本，不做 HTML 编码；正文与页脚按 HTML 上下文编码
            Assert.Equal("Hutao & Co 重置", message.Subject);
            Assert.Contains("Hutao &amp; Co", html);
            Assert.DoesNotContain("Hutao & Co", html);
            Assert.Contains("https://example.test/root/reset-password.html?token=abc123DEF", html);
            Assert.Contains("Hutao &amp; Co 页脚", html);
            Assert.DoesNotContain("{{", html);
        });
    }

    [Fact]
    public async Task CustomShellTemplate_ReceivesSystemVariablesAndContentSlots()
    {
        const string template = """
            <html>
            <body>
            <div>{{sysname}} shell</div>
            <div>{{options.Title}}</div>
            <div>{{options.RawContent}}</div>
            <div>{{options.Footer}}</div>
            <div>{{sysHost}}</div>
            </body>
            </html>
            """;

        await WithSettingsAsync(new() { [Keys.MailTemplateHtml] = template }, async () =>
        {
            factory.Smtp.Clear();

            await mailService.SendRegistrationVerifyCodeAsync(Recipient, "CODE1234", ChsLanguage);
            MimeMessage message = await WaitForMessageAsync();

            string html = message.HtmlBody ?? string.Empty;
            Assert.Contains("Snap Hutao shell", html);
            Assert.Contains("Snap Hutao 邮箱验证邮件", html);
            Assert.Contains("CODE1234", html);
            Assert.Contains("本邮件为系统自动发送邮件", html);
            Assert.Contains(DefaultSystemHost, html);
            Assert.DoesNotContain("{{", html);
        });
    }
}
