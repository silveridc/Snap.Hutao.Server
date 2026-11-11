namespace server.Models.Admin;

public sealed class UserLoginLogItem
{
    public long Id
    {
        get; set;
    }

    public int? UserId
    {
        get; set;
    }

    public string? UserName
    {
        get; set;
    }

    /// <summary>Gets or sets 登录方式：password / sso.</summary>
    public string Method { get; set; } = default!;

    public bool Success
    {
        get; set;
    }

    /// <summary>Gets or sets 失败原因对应的本地化词条键，成功时为空.</summary>
    public string? FailureKey
    {
        get; set;
    }

    public string? Ip
    {
        get; set;
    }

    /// <summary>Gets or sets iP 属地，IP2Region 的四段：国家/省份/城市/运营商，缺段为 "0"；按 IP 现查，查不到为 null.</summary>
    public string?[]? IpRegion
    {
        get; set;
    }

    public string? DeviceId
    {
        get; set;
    }

    public string? DeviceName
    {
        get; set;
    }

    public string? OperatingSystem
    {
        get; set;
    }

    public DateTimeOffset LoginAt
    {
        get; set;
    }
}
