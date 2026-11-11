using server.Services.Mail;

namespace server.Models.Mail;

public sealed record MailContentKind
{
    public required string Key
    {
        get; init;
    }

    /// <summary>Gets 模板正文必需的业务变量，不包含自动注入的系统变量.</summary>
    public required string[] Variables
    {
        get; init;
    }

    public required MailContent Chs
    {
        get; init;
    }

    public required MailContent En
    {
        get; init;
    }

    /// <summary>按语言取默认文案，仅识别 CHS/EN 两种.</summary>
    public MailContent For(string language) => language == MailContents.Languages[1] ? En : Chs;
}
