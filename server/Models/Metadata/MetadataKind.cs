namespace server.Models.Metadata;

/// <summary>
/// 一个 metadata 分类（<c>Achievement</c>、<c>Material</c>、<c>Avatar</c>…）的投影注册表。
/// 身份键的取法写在这里，是为了把「上游每个分类的键长得不一样」这件事集中记录，
/// 而不是让每个查询方各自猜——实测 <c>Reliquary</c> 的 <c>SetId</c> 有 66 个值却对应 890 个对象，
/// <c>Monster</c> 的 <c>Id</c> 有 88 个对象共用同一个值，猜错就会把两个不同的对象当同一个。
/// </summary>
[Table("metadata_kind")]
[PrimaryKey(nameof(Kind))]
[Index(nameof(DocumentName), Name = "IX_metadata_kind_DocumentName")]
public sealed class MetadataKind
{
    /// <summary>分类名，等于文档名去掉 <c>.json</c>；<c>Avatar</c> 是散文件目录。</summary>
    [MaxLength(32)]
    public string Kind { get; set; } = default!;

    /// <summary><c>metadata_document.Name</c> 里的对应名字，Avatar 为 <c>Avatar</c>（服务时展开成 <c>Avatar/{id}.json</c>）。</summary>
    [MaxLength(128)]
    public string DocumentName { get; set; } = default!;

    /// <summary>文档根是数组还是「一个文件一个对象」。</summary>
    public MetadataRootShape RootShape { get; set; }

    /// <summary>主身份键在对象内的相对路径，<see cref="MetadataIdPaths.IndexPosition"/> 表示只能按位置认。</summary>
    [MaxLength(64)]
    public string GameIdPath { get; set; } = default!;

    /// <summary>次身份键路径，空串表示主键已经唯一。</summary>
    [MaxLength(64)]
    public string SubIdPath { get; set; } = default!;

    /// <summary>本次投影里的对象总数（跨 locale 去重后的身份数，不是某个 locale 的行数）。</summary>
    public uint ObjectCount { get; set; }

    /// <summary>参与投影的 locale 数。</summary>
    public ushort LocaleCount { get; set; }

    /// <summary>结构在各 locale 间不一致、退化成「整对象按 locale 存」的对象数，见 <see cref="MetadataObjectI18n.RawField"/>。</summary>
    public uint RawCount { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime ProjectedAt { get; set; }

    /// <summary>投影之后又动过这个分类的文档（翻译覆盖、重新导入），读模型已经落后；为 null 表示是最新的。</summary>
    [Column(TypeName = "datetime")]
    public DateTime? StaleSince { get; set; }
}
