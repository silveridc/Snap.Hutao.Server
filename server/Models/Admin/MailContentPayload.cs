namespace server.Models.Admin;

public sealed class MailContentPayload
{
    public required string Kind { get; init; }

    public required string Language { get; init; }

    // 正文必需的业务变量，系统变量由服务端自动补充
    public required string[] Variables { get; init; }

    public required string Subject { get; init; }

    public required string Title { get; init; }

    public required string RawContent { get; init; }

    public required string Footer { get; init; }

    public bool Customized { get; init; }
}
