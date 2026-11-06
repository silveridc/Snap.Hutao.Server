namespace server.Models.Admin;

public sealed class MailContentSummary
{
    public required string Kind { get; init; }

    public required string[] Languages { get; init; }

    // 正文必需的业务变量，系统变量由服务端自动补充
    public required string[] Variables { get; init; }

    // 每种语言是否已有后台自定义内容，索引与 Languages 对应
    public required bool[] Customized { get; init; }
}
