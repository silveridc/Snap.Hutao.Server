namespace server.Models.Entity;

[Table("client_versions")]
public sealed class ClientVersion
{
    /// <summary>胡桃客户端上报的设备 id，一台安装一行。</summary>
    [Key]
    [StringLength(32)]
    public string DeviceId { get; set; } = default!;

    /// <summary>该设备最近一次请求携带的客户端版本。</summary>
    [StringLength(32)]
    public string Version { get; set; } = default!;

    /// <summary>首次见到该设备的 unix 秒。</summary>
    public long FirstSeenAt { get; set; }

    /// <summary>最近一次见到该设备的 unix 秒，按节流窗口刷新。</summary>
    public long LastSeenAt { get; set; }
}
