namespace server.Options
{
    public sealed class GithubOptions
    {
        public string ClientId { get; set; } = default!;

        public string ClientSecret { get; set; } = default!;

        public byte[] StateEncryptKey { get; set; } = default!;

        public string Token { get; set; } = default!;
    }
}