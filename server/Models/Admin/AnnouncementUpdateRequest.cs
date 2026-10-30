namespace server.Models.Admin;

public sealed class AnnouncementUpdateRequest
{
    public long Id
    {
        get; set;
    }

    public string Locale { get; set; } = "ALL";

    public string Title { get; set; } = default!;

    public string Content { get; set; } = default!;

    public int Severity
    {
        get; set;
    }

    public string Link { get; set; } = default!;

    public string? MaxPresentVersion
    {
        get; set;
    }
}
