namespace server.Options;

public class JwtOptions
{
    public string Issuer { get; set; } = "zhaoyj";

    /// <summary>Gets or sets the audience expected in access tokens; issuance and validation must agree, and changing it invalidates every issued access token.</summary>
    public string Audience { get; set; } = "snap-hutao-api";

    public string Raw { get; set; } = default!;
}
