namespace server.Models.Entity;

[Table("admin_audit_logs")]
public sealed class AdminAuditLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public int AdminUserId { get; set; }

    /// <summary>后台专属账号（SSO）身份；主站维护者账号执行时为空。</summary>
    public int? AdminAccountId { get; set; }

    /// <summary>写入时的后台专属账号显示名快照，账号删除后仍可追溯。</summary>
    [StringLength(128)]
    public string? AdminAccountName { get; set; }

    public string Action { get; set; } = default!;

    public string? Detail { get; set; }

    /// <summary>操作来源 IP。取 RemoteIpAddress，已由 ForwardedHeaders 还原成真实客户端地址；属地不入库，读的时候按它现查。</summary>
    [StringLength(64)]
    public string? Ip { get; set; }

    public bool Success { get; set; }

    public long Timestamp { get; set; }
}
