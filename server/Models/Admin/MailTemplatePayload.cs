namespace server.Models.Admin;

public sealed class MailTemplatePayload
{
    public required string Template
    {
        get; init;
    }

    public required string DefaultTemplate
    {
        get; init;
    }

    public bool Customized
    {
        get; init;
    }
}
