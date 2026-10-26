namespace server.Models.Entity;

[Table("admin_accounts")]
[Index(nameof(Provider), nameof(Subject), IsUnique = true)]
public sealed class AdminAccount
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>身份源类型，当前固定为 <c>oidc</c>。</summary>
    [Required]
    [StringLength(32)]
    public string Provider { get; set; } = default!;

    /// <summary>身份源中的稳定主体标识（OIDC 的 <c>sub</c>）。</summary>
    [Required]
    [StringLength(256)]
    public string Subject { get; set; } = default!;

    /// <summary>显示名，来自身份源的 <c>preferred_username</c> 或 <c>name</c>。</summary>
    [Required]
    [StringLength(128)]
    public string UserName { get; set; } = default!;

    [StringLength(256)]
    public string? Email { get; set; }

    /// <summary>
    /// 绑定的主站账号。启动器的单点登录只认这个绑定：身份源证明「你是谁」，这里决定「你登录成哪个主站账号」。
    /// 为空表示这个后台账号只能进后台，不能用于启动器登录。
    /// </summary>
    public int? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public HutaoUser? User { get; set; }

    public bool IsEnabled { get; set; }

    /// <summary>是否由配置里的种子声明建立。种子账号只能存在一个。</summary>
    public bool IsBootstrap { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}
