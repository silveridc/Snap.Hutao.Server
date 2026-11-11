namespace server.Options;

public class ReCaptchaOptions
{
    public required string Key { get; set; } = default!;

    public required string SiteKey { get; set; } = default!;
}
