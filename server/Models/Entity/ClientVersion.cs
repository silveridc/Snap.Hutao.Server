namespace server.Models.Entity;

[Table("client_versions")]
public sealed class ClientVersion
{
    /// <summary>Gets or sets 胡桃客户端上报的设备 id，一台安装一行.</summary>
    [Key]
    [StringLength(32)]
    public string DeviceId { get; set; } = default!;

    /// <summary>Gets or sets 该设备最近一次请求携带的客户端版本.</summary>
    [StringLength(32)]
    public string Version { get; set; } = default!;

    /// <summary>Gets or sets 首次见到该设备的 unix 秒.</summary>
    public long FirstSeenAt
    {
        get; set;
    }

    /// <summary>Gets or sets 最近一次见到该设备的 unix 秒，按节流窗口刷新.</summary>
    public long LastSeenAt
    {
        get; set;
    }
}
