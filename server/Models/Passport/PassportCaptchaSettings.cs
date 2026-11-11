namespace server.Models.Passport;

public sealed class PassportCaptchaSettings
{
    public bool Enabled
    {
        get; set;
    }

    public string? SiteKey
    {
        get; set;
    }
}
