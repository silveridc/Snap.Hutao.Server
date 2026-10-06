namespace server.Models.Passport
{
    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = default!;

        public string DeviceId { get; set; } = default!;
    }
}