namespace server.Models.Admin;

public sealed class AuditLogItem
{
    public long Id
    {
        get; set;
    }

    public int AdminUserId
    {
        get; set;
    }

    /// <summary>Gets or sets 后台专属账号（SSO）身份；主站维护者账号执行时为空.</summary>
    public int? AdminAccountId
    {
        get; set;
    }

    public string? AdminUserName
    {
        get; set;
    }

    public string Action { get; set; } = default!;

    public string? Detail
    {
        get; set;
    }

    /// <summary>Gets or sets 操作来源 IP.</summary>
    public string? Ip
    {
        get; set;
    }

    /// <summary>Gets or sets iP 属地，IP2Region 的四段：国家/省份/城市/运营商，缺段为 "0"；按 IP 现查，查不到为 null.</summary>
    public string?[]? IpRegion
    {
        get; set;
    }

    public bool Success
    {
        get; set;
    }

    public long Timestamp
    {
        get; set;
    }
}
