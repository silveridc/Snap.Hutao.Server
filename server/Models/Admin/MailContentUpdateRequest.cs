namespace server.Models.Admin;

public sealed class MailContentUpdateRequest
{
    public string? Language { get; set; }

    public string? Subject { get; set; }

    public string? Title { get; set; }

    public string? RawContent { get; set; }

    public string? Footer { get; set; }
}
