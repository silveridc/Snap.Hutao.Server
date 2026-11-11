namespace server.Options;

public sealed class AppOptions
{
    public required string SentryDsn { get; set; } = default!;

    public required string OtlpToken { get; set; } = default!;

    public required string OtlpTracesEndpoint { get; set; } = default!;

    public required string OtlpLogsEndpoint { get; set; } = default!;

    public JwtOptions Jwt { get; set; } = new();

    public string MailerSecret { get; set; } = default!;

    public ReCaptchaOptions ReCaptcha { get; set; } = default!;

    public string RSAPrivateKey { get; set; } = default!;

    public string CdnEndpoint { get; set; } = default!;

    public string CdnToken { get; set; } = default!;

    public byte[] OAuthStateEncryptKey { get; set; } = default!;

    public int DefaultAdminUserId { get; set; } = 1!;

    public SocketOptions Socket { get; set; } = default!;

    public AppPasswordOptions Password { get; set; } = new();

    public RealIpOptions RealIp { get; set; } = new();

    public SmtpOptions Smtp { get; set; } = new();

    public GithubOptions Github { get; set; } = new();
}
