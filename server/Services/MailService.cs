using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using server.Services.Admin;
using System.Globalization;
using System.Text.RegularExpressions;

namespace server.Services;

// Singleton
public sealed partial class MailService(ILogger<MailService> logger, SystemConfigService systemConfig)
{
    private readonly ILogger<MailService> _logger = logger;

    public Task SendRegistrationVerifyCodeAsync(string emailAddress, string code, string language = "CHS")
    {
        _logger.LogInformation("Send registration mail to [{Email}] in [{Language}]", emailAddress, language);
        return SendMailKindAsync(MailContents.Get("RegisterCode"), language, emailAddress, new Dictionary<string, string> { ["code"] = code });
    }

    public Task SendResetPasswordLinkAsync(string emailAddress, string token, string language = "CHS")
    {
        _logger.LogInformation("Send password reset mail to [{Email}] in [{Language}]", emailAddress, language);
        return SendMailKindAsync(MailContents.Get("ResetPasswordLink"), language, emailAddress, new Dictionary<string, string> { ["token"] = token });
    }

    public Task SendResetUsernameVerifyCodeAsync(string emailAddress, string code, string language = "CHS")
    {
        _logger.LogInformation("Send email change mail to [{Email}]", emailAddress);
        return SendMailKindAsync(MailContents.Get("ResetUsernameCode"), language, emailAddress, new Dictionary<string, string> { ["code"] = code });
    }

    public Task SendCancelRegistrationVerifyCodeAsync(string emailAddress, string code, string language = "CHS")
    {
        _logger.LogInformation("Send account deletion mail to [{Email}]", emailAddress);
        return SendMailKindAsync(MailContents.Get("CancelRegistrationCode"), language, emailAddress, new Dictionary<string, string> { ["code"] = code });
    }

    public Task SendPurchaseGachaLogStorageServiceNoSuchUserAsync(string emailAddress, string redeemCode, string tradeNumber, string language = "CHS")
    {
        _logger.LogInformation("Send GachaLog Mail to [{Email}] with [{Number}]", emailAddress, tradeNumber);
        return SendMailKindAsync(MailContents.Get("GachaStorageRedeem"), language, emailAddress, new Dictionary<string, string> { ["redeemCode"] = redeemCode, ["tradeNumber"] = tradeNumber });
    }

    public Task SendPurchaseCdnServiceNoSuchUserAsync(string emailAddress, string redeemCode, string tradeNumber, string language = "CHS")
    {
        _logger.LogInformation("Send CDN Mail to [{Email}] with [{Number}]", emailAddress, tradeNumber);
        return SendMailKindAsync(MailContents.Get("CdnRedeem"), language, emailAddress, new Dictionary<string, string> { ["redeemCode"] = redeemCode, ["tradeNumber"] = tradeNumber });
    }

    public Task SendDiagnosticMailAsync(string emailAddress, CancellationToken cancellationToken = default)
    {
        return SendMessageAsync(
            emailAddress,
            "SMTP configuration test",
            "<p>This is a test email sent from the Snap Hutao admin panel.</p><p>No verification code or password reset link was generated.</p>",
            systemConfig.Smtp,
            cancellationToken);
    }

    [GeneratedRegex(@"\{\{([\w.]+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex VariablePattern();

    private static string Interpolate(string template, IReadOnlyDictionary<string, string> variables)
    {
        return VariablePattern().Replace(template, match => variables.TryGetValue(match.Groups[1].Value, out string? value) ? value : match.Value);
    }

    /// <summary>按种类读取文案（后台可覆盖），自动注入系统变量后组装邮件.</summary>
    private Task SendMailKindAsync(MailContentKind kind, string language, string emailAddress, Dictionary<string, string> variables)
    {
        variables["sysname"] = systemConfig.MailSystemName;
        variables["sysHost"] = systemConfig.MailPublicBaseUrl;
        int expireSeconds = kind.Key == "ResetPasswordLink" ? systemConfig.ResetPasswordLinkExpireSeconds : systemConfig.VerificationCodeExpireSeconds;
        variables["expireMinutes"] = (expireSeconds / 60).ToString(CultureInfo.InvariantCulture);
        variables["time"] = variables["expireMinutes"];
        Dictionary<string, string> htmlVariables = variables.ToDictionary(pair => pair.Key, pair => HtmlEncoder.Default.Encode(pair.Value), StringComparer.Ordinal);

        MailContent content = systemConfig.GetMailContent(kind.Key, language);
        MailOptions options = new()
        {
            Subject = Interpolate(content.Subject, variables),
            Address = emailAddress,
            Title = Interpolate(content.Title, htmlVariables),
            RawContent = Interpolate(content.RawContent, htmlVariables),
            Footer = Interpolate(content.Footer, htmlVariables),
        };

        return SendMimeMailAsync(options, htmlVariables);
    }

    private string ComposeMailBody(MailOptions options, IReadOnlyDictionary<string, string> variables)
    {
        string template = systemConfig.MailTemplateHtml;
        if (string.IsNullOrWhiteSpace(template))
        {
            template = MailTemplates.DefaultHtml;
        }

        Dictionary<string, string> values = new(variables, StringComparer.Ordinal)
        {
            ["options.Title"] = options.Title,
            ["options.RawContent"] = options.RawContent,
            ["options.Footer"] = options.Footer,
        };
        return Interpolate(template, values);
    }

    private Task SendMimeMailAsync(MailOptions options, IReadOnlyDictionary<string, string> variables)
    {
        return SendMessageAsync(options.Address, options.Subject, ComposeMailBody(options, variables), systemConfig.Smtp);
    }

    private async Task SendMessageAsync(string emailAddress, string subject, string html, SmtpOptions smtp, CancellationToken cancellationToken = default)
    {
        foreach ((string key, string? value) in new Dictionary<string, string?>
        {
            [Keys.SmtpServer] = smtp.Server,
            [Keys.SmtpPort] = smtp.Port.ToString(CultureInfo.InvariantCulture),
            [Keys.SmtpSecurity] = smtp.Security,
            [Keys.SmtpFromAddress] = smtp.FromAddress,
            [Keys.SmtpFromName] = smtp.FromName,
            [Keys.SmtpUserName] = smtp.UserName,
            [Keys.SmtpTimeoutSeconds] = smtp.TimeoutSeconds.ToString(CultureInfo.InvariantCulture),
        })
        {
            string? error = SystemSettingValidation.Validate(key, value);
            if (error is not null)
            {
                throw new InvalidOperationException(error);
            }
        }

        string fromAddress = string.IsNullOrWhiteSpace(smtp.FromAddress) ? smtp.UserName : smtp.FromAddress;
        if (string.IsNullOrWhiteSpace(smtp.Server) || !SystemSettingValidation.IsEmailAddress(fromAddress) || !SystemSettingValidation.IsEmailAddress(emailAddress))
        {
            throw new InvalidOperationException("SMTP server, sender and recipient must be configured correctly");
        }

        SecureSocketOptions security = smtp.Security.ToUpperInvariant() switch
        {
            "AUTO" => SecureSocketOptions.Auto,
            "STARTTLS" => SecureSocketOptions.StartTls,
            "SSLONCONNECT" => SecureSocketOptions.SslOnConnect,
            "NONE" => SecureSocketOptions.None,
            _ => throw new InvalidOperationException("Unsupported SMTP security mode"),
        };
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(smtp.TimeoutSeconds));
        using MimeMessage mimeMessage = new()
        {
            Subject = subject,
            From =
            {
                new MailboxAddress(smtp.FromName, fromAddress),
            },
            To =
            {
                new MailboxAddress(emailAddress, emailAddress),
            },
            Body = new TextPart("html")
            {
                Text = html,
            },
        };

        using SmtpClient client = new() { Timeout = smtp.TimeoutSeconds * 1000 };
        await client.ConnectAsync(smtp.Server, smtp.Port, security, timeout.Token).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(smtp.UserName))
        {
            await client.AuthenticateAsync(smtp.UserName, smtp.Password, timeout.Token).ConfigureAwait(false);
        }

        await client.SendAsync(mimeMessage, timeout.Token).ConfigureAwait(false);
        await client.DisconnectAsync(true, timeout.Token).ConfigureAwait(false);
    }
}
