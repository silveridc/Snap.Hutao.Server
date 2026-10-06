namespace server.Options
{
    public sealed class AppOptions
    {
        public string JWTIssuer { get; set; } = default!;

        public string JwtRaw { get; set; } = default!;

        public string MailerSecret { get; set; } = default!;

        public string ReCaptchaKey { get; set; } = default!;

        public string RedisAddress { get; set; } = default!;

        public string RSAPrivateKey { get; set; } = default!;

        public string CdnEndpoint { get; set; } = default!;

        public string CdnToken { get; set; } = default!;

        public byte[] OAuthStateEncryptKey { get; set; } = default!;

        public SocketOptions Socket { get; set; } = default!;
        public RealIpOptions RealIp { get; set; } = default!;
        public AfdianOptions Afdian { get; set; } = new();

        public Afdian2Options Afdian2 { get; set; } = default!;

        public GenshinPizzaHelperOptions GenshinPizzaHelper { get; set; } = default!;

        public SmtpOptions Smtp { get; set; } = default!;

        public GithubOptions Github { get; set; } = default!;

        public PatchOptions Patch { get; set; } = new();
    }
}