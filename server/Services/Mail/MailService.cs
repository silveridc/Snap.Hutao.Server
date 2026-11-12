using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using server.Models.Mail;
using server.Services.Admin;
using System.Globalization;
using System.Text.RegularExpressions;

namespace server.Services.Mail;

// Singleton
public sealed partial class MailService(ILogger<MailService> logger, SystemConfigService systemConfig)
{
    public static readonly string[] Placeholders = ["{{options.Title}}", "{{options.RawContent}}", "{{options.Footer}}"];
    private readonly ILogger<MailService> _logger = logger;

    /// <summary>
    /// Sends a registration verification code email.
    /// </summary>
    /// <param name="emailAddress">The recipient's email address.</param>
    /// <param name="code">The verification code.</param>
    /// <param name="language">The language/culture code of the email content (e.g., "CHS").</param>
    public async Task SendRegistrationVerifyCodeAsync(string emailAddress, string code, string language = "CHS")
    {
        LogSendMail(_logger, "registration", emailAddress, language);
        await SendMailKindAsync(MailContents.Get("RegisterCode"), language, emailAddress, new Dictionary<string, string> { ["code"] = code });
    }

    /// <summary>
    /// Sends a password reset link email.
    /// </summary>
    /// <param name="emailAddress">The recipient's email address.</param>
    /// <param name="token">The token used to verify the password reset request.</param>
    /// <param name="language">The language/culture code of the email content (e.g., "CHS").</param>
    public async Task SendResetPasswordLinkAsync(string emailAddress, string token, string language = "CHS")
    {
        LogSendMail(_logger, "password reset", emailAddress, language);
        await SendMailKindAsync(MailContents.Get("ResetPasswordLink"), language, emailAddress, new Dictionary<string, string> { ["token"] = token });
    }

    /// <summary>
    /// Sends a verification code email for resetting or changing the account username/email.
    /// </summary>
    /// <param name="emailAddress">The recipient's email address.</param>
    /// <param name="code">The verification code.</param>
    /// <param name="language">The language/culture code of the email content (e.g., "CHS").</param>
    public Task SendResetUsernameVerifyCodeAsync(string emailAddress, string code, string language = "CHS")
    {
        LogSendMail(_logger, "email change", emailAddress, language);
        return SendMailKindAsync(MailContents.Get("ResetUsernameCode"), language, emailAddress, new Dictionary<string, string> { ["code"] = code });
    }

    /// <summary>
    /// Sends an account deletion/cancellation verification code email.
    /// </summary>
    /// <param name="emailAddress">The recipient's email address.</param>
    /// <param name="code">The verification code.</param>
    /// <param name="language">The language/culture code of the email content (e.g., "CHS").</param>
    public Task SendCancelRegistrationVerifyCodeAsync(string emailAddress, string code, string language = "CHS")
    {
        LogSendMail(_logger, "account deletion", emailAddress, language);
        return SendMailKindAsync(MailContents.Get("CancelRegistrationCode"), language, emailAddress, new Dictionary<string, string> { ["code"] = code });
    }

    /// <summary>
    /// Sends a gacha log storage service redemption code email to an unregistered user after purchase.
    /// </summary>
    /// <param name="emailAddress">The recipient's email address.</param>
    /// <param name="redeemCode">The redemption code for the purchased storage service.</param>
    /// <param name="tradeNumber">The transaction/order identifier associated with the purchase.</param>
    /// <param name="language">The language/culture code of the email content (e.g., "CHS").</param>
    public Task SendPurchaseGachaLogStorageServiceNoSuchUserAsync(string emailAddress, string redeemCode, string tradeNumber, string language = "CHS")
    {
        LogSendtradeMail(_logger, "GachaLog Mail", emailAddress, tradeNumber);
        return SendMailKindAsync(MailContents.Get("GachaStorageRedeem"), language, emailAddress, new Dictionary<string, string> { ["redeemCode"] = redeemCode, ["tradeNumber"] = tradeNumber });
    }

    /// <summary>
    /// Sends a CDN service redemption code email to an unregistered user after purchase.
    /// </summary>
    /// <param name="emailAddress">The recipient's email address.</param>
    /// <param name="redeemCode">The redemption code for the purchased CDN service.</param>
    /// <param name="tradeNumber">The transaction/order identifier associated with the purchase.</param>
    /// <param name="language">The language/culture code of the email content (e.g., "CHS").</param>
    public Task SendPurchaseCdnServiceNoSuchUserAsync(string emailAddress, string redeemCode, string tradeNumber, string language = "CHS")
    {
        LogSendtradeMail(_logger, "CDN Mail", emailAddress, tradeNumber);
        return SendMailKindAsync(MailContents.Get("CdnRedeem"), language, emailAddress, new Dictionary<string, string> { ["redeemCode"] = redeemCode, ["tradeNumber"] = tradeNumber });
    }

    /// <summary>
    /// Sends a test email to verify SMTP server configuration.
    /// </summary>
    /// <param name="emailAddress">The recipient's email address.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task SendDiagnosticMailAsync(string emailAddress, CancellationToken cancellationToken = default)
    {
        LogSendMail(_logger, "DiagnosticMail", emailAddress, "EN");
        return SendMessageAsync(
            emailAddress,
            "SMTP configuration test",
            "<p>This is a test email sent from the Snap Hutao admin panel.</p>",
            systemConfig.Smtp,
            cancellationToken);
    }

    /// <summary>
    /// Gets a regular expression used to match placeholder variables in templates (e.g., <c>{{variable.name}}</c>).
    /// </summary>
    /// <returns>A compiled <see cref="Regex"/> instance for matching placeholder variables.</returns>
    [GeneratedRegex(@"\{\{([\w.]+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex VariablePattern();

    /// <summary>
    /// Replaces placeholder variables in the given template string with their corresponding values.
    /// </summary>
    /// <param name="template">The template string containing placeholders.</param>
    /// <param name="variables">A dictionary mapping variable names to their replacement values.</param>
    /// <returns>The interpolated string with all matched placeholders replaced.</returns>
    private static string Interpolate(string template, Dictionary<string, string> variables)
    {
        return VariablePattern().Replace(template, match => variables.TryGetValue(match.Groups[1].Value, out string? value) ? value : match.Value);
    }

    /// <summary>
    /// Populates template variables, prepares email content based on kind and language, and dispatches the email.
    /// </summary>
    /// <param name="kind">The kind of email content to send.</param>
    /// <param name="language">The language/culture code used to look up localized content.</param>
    /// <param name="emailAddress">The recipient's email address.</param>
    /// <param name="variables">A collection of custom variables to be injected into the email templates.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task SendMailKindAsync(MailContentKind kind, string language, string emailAddress, Dictionary<string, string> variables)
    {
        int expireSeconds = kind.Key == "ResetPasswordLink" ? systemConfig.ResetPasswordLinkExpireSeconds : systemConfig.VerificationCodeExpireSeconds;
        variables["sysname"] = systemConfig.MailSystemName;
        variables["sysHost"] = systemConfig.MailPublicBaseUrl;
        variables["expireMinutes"] = (expireSeconds / 60).ToString(CultureInfo.InvariantCulture);
        variables["time"] = variables["expireMinutes"];
        variables["expireDays"] = systemConfig.RedeemCodeExpireDays.ToString(CultureInfo.InvariantCulture);

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

        await SendMimeMailAsync(options, htmlVariables);
    }

    /// <summary>
    /// Renders the complete HTML body of the email by injecting title, content, footer, and variables into the master HTML template.
    /// </summary>
    /// <param name="options">The mail options containing the interpolated title, body content, and footer.</param>
    /// <param name="variables">The dictionary of HTML-encoded variables used for template placeholder substitution.</param>
    /// <returns>The rendered HTML email body.</returns>
    private string ComposeMailBody(MailOptions options, IReadOnlyDictionary<string, string> variables)
    {
        string template = systemConfig.MailTemplateHtml;
        if (string.IsNullOrWhiteSpace(template))
        {
            template = MailContents.MailTemplateHtml;
        }

        Dictionary<string, string> values = new(variables, StringComparer.Ordinal)
        {
            ["options.Title"] = options.Title,
            ["options.RawContent"] = options.RawContent,
            ["options.Footer"] = options.Footer,
        };
        return Interpolate(template, values);
    }

    /// <summary>
    /// Assembles the message body and sends the email using the configured SMTP settings.
    /// </summary>
    /// <param name="options">The mail options containing recipient, subject, and content blocks.</param>
    /// <param name="variables">The dictionary of HTML-encoded variables used for template rendering.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task SendMimeMailAsync(MailOptions options, IReadOnlyDictionary<string, string> variables)
    {
        await SendMessageAsync(options.Address, options.Subject, ComposeMailBody(options, variables), systemConfig.Smtp);
    }

    /// <summary>
    /// Validates SMTP configuration, establishes a secure connection, and delivers a MIME email message via SMTP.
    /// </summary>
    /// <param name="emailAddress">The recipient's email address.</param>
    /// <param name="subject">The email subject line.</param>
    /// <param name="html">The HTML content of the email body.</param>
    /// <param name="smtp">The SMTP server and credential options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when SMTP configuration settings fail validation, email addresses are invalid, or an unsupported security mode is specified.
    /// </exception>
    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1204:StaticElementsMustAppearBeforeInstanceElements", Justification = "aaa")]
    private static async Task SendMessageAsync(string emailAddress, string subject, string html, SmtpOptions smtp, CancellationToken cancellationToken = default)
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

        using SmtpClient client = new()
        {
            Timeout = smtp.TimeoutSeconds * 1000,
        };
        await client.ConnectAsync(smtp.Server, smtp.Port, security, timeout.Token).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(smtp.UserName))
        {
            await client.AuthenticateAsync(smtp.UserName, smtp.Password, timeout.Token).ConfigureAwait(false);
        }

        await client.SendAsync(mimeMessage, timeout.Token).ConfigureAwait(false);
        await client.DisconnectAsync(true, timeout.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// 邮件日志记录
    /// </summary>
    /// <param name="logger">_logger</param>
    /// <param name="type">类型</param>
    /// <param name="email">邮箱地址</param>
    /// <param name="language">语言</param>
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Send [{type}] mail to [{Email}] in [{Language}]")]
    static partial void LogSendMail(ILogger logger, string type, string email, string language);

    /// <summary>
    /// 带订单号的邮件日志记录
    /// </summary>
    /// <param name="logger">_logger</param>
    /// <param name="type">类型</param>
    /// <param name="email">邮箱地址</param>
    /// <param name="tradeNumber">订单号</param>
    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Send [{type}] mail to [{Email}] with [{tradeNumber}]")]
    static partial void LogSendtradeMail(ILogger logger, string type, string email, string tradeNumber);
}
