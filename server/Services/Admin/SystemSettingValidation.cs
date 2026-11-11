using System.Globalization;
using System.Net.Mail;

namespace server.Services.Admin;

public static class SystemSettingValidation
{
    private static readonly Dictionary<string, (int Minimum, int Maximum, int Step)> _integerRanges = new(StringComparer.OrdinalIgnoreCase)
    {
        [Keys.VerificationCodeExpireSeconds] = (60, 3600, 60),
        [Keys.VerificationResendCooldownSeconds] = (10, 600, 1),
        [Keys.ResetPasswordLinkExpireSeconds] = (60, 3600, 60),
        [Keys.GachaLogMaxUidPerUser] = (1, 50, 1),
        [Keys.RegistrationGiftDays] = (1, 365, 1),
        [Keys.GachaLogExtendDays] = (1, 365, 1),
        [Keys.RedeemCodeExpireDays] = (1, 365, 1),
        [Keys.JobLogRetentionDays] = (7, 3650, 1),
        [Keys.SpiralAbyssRecordRetentionDays] = (7, 3650, 1),
        [Keys.SmtpPort] = (1, 65535, 1),
        [Keys.SmtpTimeoutSeconds] = (1, 120, 1),
    };

    private static readonly Dictionary<string, (float Minimum, float Maximum)> _decimalRanges = new(StringComparer.OrdinalIgnoreCase)
    {
        [Keys.RecaptchaMinScore] = (0f, 1f),
    };

    /// <summary>
    /// Validates a setting value against the constraints of its key.
    /// </summary>
    /// <param name="key">The setting key.</param>
    /// <param name="value">The value to validate; blank values clear the override and are always accepted.</param>
    /// <returns>An English validation error, or <see langword="null"/> when the value is acceptable.</returns>
    public static string? Validate(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128 || key.Any(char.IsControl))
        {
            return "Configuration keys must contain 1 to 128 characters without control characters";
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (_integerRanges.TryGetValue(key, out (int Minimum, int Maximum, int Step) range)
            && (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
                || number < range.Minimum || number > range.Maximum || number % range.Step != 0))
        {
            return $"{key} must be an integer between {range.Minimum} and {range.Maximum}, in steps of {range.Step}";
        }

        if (_decimalRanges.TryGetValue(key, out (float Minimum, float Maximum) decimalRange)
            && (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float score)
                || score < decimalRange.Minimum || score > decimalRange.Maximum))
        {
            return $"{key} must be a number between {decimalRange.Minimum.ToString(CultureInfo.InvariantCulture)} and {decimalRange.Maximum.ToString(CultureInfo.InvariantCulture)}";
        }

        if (key.Equals(Keys.RecaptchaSiteKey, StringComparison.OrdinalIgnoreCase)
            && !value.Trim().All(static ch => char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-'))
        {
            return $"{key} must contain only letters, digits, underscores and hyphens";
        }

        if (key.Equals(Keys.SmtpSecurity, StringComparison.OrdinalIgnoreCase)
            && !new[] { "Auto", "StartTls", "SslOnConnect", "None" }.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return $"{key} must be Auto, StartTls, SslOnConnect or None";
        }

        if (key.Equals(Keys.SmtpServer, StringComparison.OrdinalIgnoreCase) && Uri.CheckHostName(value.Trim()) == UriHostNameType.Unknown)
        {
            return $"{key} must be a host name or IP address without a scheme or port";
        }

        if ((key.Equals(Keys.SmtpFromAddress, StringComparison.OrdinalIgnoreCase) || key.Equals(Keys.SmtpDiagnosticEmail, StringComparison.OrdinalIgnoreCase)) && !IsEmailAddress(value.Trim()))
        {
            return $"{key} must be a single email address";
        }

        if (key.Equals(Keys.MailPublicBaseUrl, StringComparison.OrdinalIgnoreCase)
            && (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)))
        {
            return $"{key} must be an absolute HTTP(S) URL without credentials, query or fragment";
        }

        if ((key.StartsWith("Smtp.", StringComparison.OrdinalIgnoreCase) || key.Equals(Keys.MailSystemName, StringComparison.OrdinalIgnoreCase))
            && !SystemSettingSecurity.IsSensitiveKey(key) && (value.Length > 1024 || value.Any(char.IsControl)))
        {
            return $"{key} must not exceed 1024 characters or contain control characters";
        }

        return null;
    }

    /// <summary>
    /// Determines whether the value is a single, well-formed email address.
    /// </summary>
    /// <param name="address">The candidate address.</param>
    /// <returns><see langword="true"/> if the address is valid; otherwise, <see langword="false"/>.</returns>
    public static bool IsEmailAddress(string address)
    {
        return address.Length <= 254 && MailAddress.TryCreate(address, out MailAddress? mailbox) && string.Equals(mailbox.Address, address, StringComparison.OrdinalIgnoreCase);
    }
}
