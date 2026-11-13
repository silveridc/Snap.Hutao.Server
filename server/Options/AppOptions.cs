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

    public int DefaultAdminUserId { get; set; } = 1!;

    public EncryptOptions Encrypt { get; set; } = new();

    public ReCaptchaOptions ReCaptcha { get; set; } = default!;

    public JwtOptions Jwt { get; set; } = new();

    public CdnOptions Cdn { get; set; } = new();

    public SocketOptions Socket { get; set; } = new();

    public AppPasswordOptions Password { get; set; } = new();

    public RealIpOptions RealIp { get; set; } = new();

    public SmtpOptions Smtp { get; set; } = new();

    public GithubOptions Github { get; set; } = new();
}
