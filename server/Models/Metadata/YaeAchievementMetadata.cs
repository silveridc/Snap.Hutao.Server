namespace server.Models.Metadata;

[Table("yae_achievement_metadata")]
public sealed class YaeAchievementMetadata
{
    /// <summary>与客户端读取的 <c>config.ini</c> 里 <c>game_version=</c> 完全一致（如 <c>7.1.0</c>）。</summary>
    [Key]
    [MaxLength(16)]
    public string Version { get; set; } = default!;

    /// <summary>命中 <see cref="ChineseHash"/> 的那组 RVA 写入 payload 的 chinese 分支。</summary>
    public uint ChineseHash { get; set; }

    /// <summary>另一组 RVA 的 exe CRC32（主 exe 前 64 KiB），写入 oversea 分支。</summary>
    public uint OverseaHash { get; set; }

    public uint StoreCmdId { get; set; }

    public uint AchievementCmdId { get; set; }

    /// <summary>bucket 里带的成就名条目数，仅用于运维核对同步是否完整。</summary>
    public uint ItemCount { get; set; }

    /// <summary>bucket 里带的成就分组数，仅用于运维核对同步是否完整。</summary>
    public uint GroupCount { get; set; }

    [Column(TypeName = "longtext")]
    public string Payload { get; set; } = default!;

    [Column(TypeName = "datetime")]
    public DateTime SyncedAt { get; set; }
}
