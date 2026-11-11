namespace server.Models.Mail;

public sealed record MailContent
{
    public required string Subject
    {
        get; init;
    }

    public required string Title
    {
        get; init;
    }

    public required string RawContent
    {
        get; init;
    }

    public required string Footer
    {
        get; init;
    }
}
