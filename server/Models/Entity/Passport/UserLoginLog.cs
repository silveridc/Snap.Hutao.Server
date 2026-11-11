namespace server.Models.Entity.Passport;

[Table("user_login_logs")]
[Index(nameof(LoginAt))]
[Index(nameof(UserId))]
[Index(nameof(Ip))]
public sealed class UserLoginLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id
    {
        get; set;
    }

    /// <summary>Gets or sets 尝试登录的邮箱（按输入原样）；SSO 失败等拿不到用户名的场景为空.</summary>
    [MaxLength(256)]
    public string? UserName
    {
        get; set;
    }

    /// <summary>Gets or sets 登录成功时关联的用户；失败或账号不存在时为空.</summary>
    public int? UserId
    {
        get; set;
    }

    /// <summary>Gets or sets 登录方式：password / sso.</summary>
    [MaxLength(16)]
    public string Method { get; set; } = default!;

    public bool Success
    {
        get; set;
    }

    /// <summary>Gets or sets 失败原因对应的本地化词条键，成功时为空.</summary>
    [MaxLength(64)]
    public string? FailureKey
    {
        get; set;
    }

    [MaxLength(64)]
    public string? Ip
    {
        get; set;
    }

    [MaxLength(64)]
    public string? DeviceId
    {
        get; set;
    }

    [MaxLength(128)]
    public string? DeviceName
    {
        get; set;
    }

    [MaxLength(64)]
    public string? OperatingSystem
    {
        get; set;
    }

    public DateTimeOffset LoginAt
    {
        get; set;
    }
}
