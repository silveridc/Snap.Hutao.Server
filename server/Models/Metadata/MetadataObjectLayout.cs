namespace server.Models.Metadata;

/// <summary>
/// 某个 locale 的某个文档里「有哪些对象、按什么顺序」。
/// 不能从 <see cref="MetadataObject"/> 推断：实测各 locale 的成员并不相同
/// （CHS 的 MainQuest 缺 CHT 独有的那些，共 23,892 处差异；Material 也有 624 处），
/// 顺序同样不能假设一致，所以装配时必须按这张表的 Position 逐 locale 走。
/// </summary>
[Table("metadata_object_layout")]
[PrimaryKey(nameof(Locale), nameof(Name), nameof(Position))]
[Index(nameof(Kind), nameof(GameId), nameof(SubId), nameof(Locale), Name = "IX_metadata_object_layout_Kind_Id")]
public sealed class MetadataObjectLayout
{
    [MaxLength(8)]
    public string Locale { get; set; } = default!;

    /// <summary>文档名，与 <see cref="MetadataDocument.Name"/> 同口径（散文件形如 <c>Avatar/10000002</c>）。</summary>
    [MaxLength(128)]
    public string Name { get; set; } = default!;

    /// <summary>在这个 locale 的文档里排第几个，从 0 开始。</summary>
    public uint Position { get; set; }

    /// <summary>指向 <see cref="MetadataObject"/> 的身份三元组。</summary>
    [MaxLength(32)]
    public string Kind { get; set; } = default!;

    public ulong GameId { get; set; }

    public uint SubId { get; set; }
}
