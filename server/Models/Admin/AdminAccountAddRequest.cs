namespace server.Models.Admin;

public sealed class AdminAccountAddRequest
{
    /// <summary>Gets or sets 身份源中的主体标识（OIDC 的 sub）.</summary>
    public string? Subject
    {
        get; set;
    }

    /// <summary>Gets or sets 显示名，留空时使用主体标识.</summary>
    public string? UserName
    {
        get; set;
    }

    /// <summary>Gets or sets 备注邮箱，可空.</summary>
    public string? Email
    {
        get; set;
    }
}
