namespace server.Models.Passport;

public sealed class PassportSsoLoginResult
{
    public string AccessToken { get; set; } = default!;

    public string RefreshToken { get; set; } = default!;

    public int ExpiresIn
    {
        get; set;
    }

    public string UserName { get; set; } = default!;
}
