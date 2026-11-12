namespace server.Options;

[SuppressMessage("", "SA1124")]
public sealed class AppOptions
{
    #region 指标数据 sentry上报
    public required string SentryDsn { get; set; } = default!;

    public required string OtlpToken { get; set; } = default!;

    public required string OtlpTracesEndpoint { get; set; } = default!;

    public required string OtlpLogsEndpoint { get; set; } = default!;
    #endregion

    public JwtOptions Jwt { get; set; } = new();

    public ReCaptchaOptions ReCaptcha { get; set; } = default!;

    public string RSAPrivateKey { get; set; } = default!;

    public CdnOptions Cdn { get; set; } = new();

    public byte[] OAuthStateEncryptKey { get; set; } = default!;

    public int DefaultAdminUserId { get; set; } = 1!;

    public SocketOptions Socket { get; set; } = default!;

    public AppPasswordOptions Password { get; set; } = new();

    public RealIpOptions RealIp { get; set; } = new();

    public SmtpOptions Smtp { get; set; } = new();

    public GithubOptions Github { get; set; } = new();
}
