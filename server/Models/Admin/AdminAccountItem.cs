namespace server.Models.Admin;

public sealed class AdminAccountItem
{
    public int Id
    {
        get; set;
    }

    public string Provider { get; set; } = default!;

    public string Subject { get; set; } = default!;

    public string UserName { get; set; } = default!;

    public string? Email
    {
        get; set;
    }

    public bool IsEnabled
    {
        get; set;
    }

    public bool IsBootstrap
    {
        get; set;
    }

    public DateTimeOffset CreatedAt
    {
        get; set;
    }

    public DateTimeOffset? LastLoginAt
    {
        get; set;
    }

    /// <summary>Gets or sets 绑定的主站账号 ID，未绑定时为 null，启动器单点登录只允许已绑定的账号.</summary>
    public int? BoundUserId
    {
        get; set;
    }

    /// <summary>Gets or sets 绑定的主站账号邮箱.</summary>
    public string? BoundUserName
    {
        get; set;
    }

    /// <summary>Gets or sets a value indicating whether 是否为当前调用者本人，前端据此禁用「停用自己」.</summary>
    public bool IsCurrent
    {
        get; set;
    }
}
