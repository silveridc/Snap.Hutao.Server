namespace server.Models.Admin;

public sealed class AdminSsoTokenResponse
{
    public string AccessToken { get; set; } = default!;

    public int ExpiresIn
    {
        get; set;
    }

    public AdminSsoAccount Principal { get; set; } = default!;
}
