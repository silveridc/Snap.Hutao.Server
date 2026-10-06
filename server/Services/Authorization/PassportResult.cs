using server.Models.Passport;

namespace server.Services.Authorization
{
    public class PassportResult(bool success, string message, string? key, TokenResponse? token = null)
    {
        [MemberNotNullWhen(true, nameof(Token))]
        public bool Success { get; set; } = success;

        public string Message { get; set; } = message;

        public TokenResponse? Token { get; set; } = token;

        public string? LocalizationKey { get; set; } = key;
    }
}