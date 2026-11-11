namespace server.Options;

public sealed class SmtpOptions
{
    public string SystemName { get; set; } = "Snap Hutao";

    public string PublicBaseUrl { get; set; } = "https://snap-api.sliveridc1.cn";

    public string DiagnosticEmailAddress { get; set; } = default!;

    public string Password { get; set; } = default!;

    public string Server { get; set; } = default!;

    public int Port { get; set; } = 25;

    public string Security { get; set; } = "Auto";

    public string FromName { get; set; } = "Snap.Hutao.Rev";

    public string FromAddress { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    public string UserName { get; set; } = default!;
}
