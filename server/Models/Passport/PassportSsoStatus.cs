namespace server.Models.Passport;

public sealed class PassportSsoStatus
{
    public bool Enabled
    {
        get; set;
    }

    /// <summary>Gets or sets 身份源显示名，为空时由启动器使用自己的文案.</summary>
    public string? DisplayName
    {
        get; set;
    }
}
