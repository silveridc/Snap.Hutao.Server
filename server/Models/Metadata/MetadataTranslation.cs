namespace server.Models.Metadata;

/// <summary>
/// i18n 覆盖表：只记录「被人工改过」的字符串叶子，未改过的文档一行都不存。
/// 写入时同步落到 <see cref="MetadataDocument.Doc"/>，删除时用 <see cref="OriginalValue"/> 还原，
/// 因此重新导入整棵树之后仍然可以按本表把改动重新贴回去。
/// </summary>
[Table("metadata_translation")]
[PrimaryKey(nameof(Locale), nameof(Name), nameof(Pointer))]
public sealed class MetadataTranslation
{
    [MaxLength(8)]
    public string Locale { get; set; } = default!;

    /// <summary>与 <see cref="MetadataDocument.Name"/> 同义（不带 .json）。</summary>
    [MaxLength(128)]
    public string Name { get; set; } = default!;

    /// <summary>RFC 6901 JSON Pointer，必须指向一个已有的字符串叶子。</summary>
    [MaxLength(128)]
    public string Pointer { get; set; } = default!;

    /// <summary>人工指定的译文。</summary>
    [Column(TypeName = "longtext")]
    public string Value { get; set; } = default!;

    /// <summary>第一次覆盖时该指针上的原文，用于删除覆盖时还原。</summary>
    [Column(TypeName = "longtext")]
    public string OriginalValue { get; set; } = default!;

    [Column(TypeName = "datetime")]
    public DateTime UpdatedAt { get; set; }
}
