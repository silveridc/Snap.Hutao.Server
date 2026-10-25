namespace server.Models.Metadata;

/// <summary>
/// 一个对象里随 locale 变化的那个叶子。骨架里对应位置是占位符，这里给值，
/// 装配时按 (Kind, GameId, SubId, Field) 取该 <c>Locale</c> 的那一行替进去。
/// CHS 也照样存一行，这样装配只有一条路径：不用区分「源 locale 可以省一层查询」。
/// </summary>
[Table("metadata_object_i18n")]
[PrimaryKey(nameof(Kind), nameof(GameId), nameof(SubId), nameof(Field), nameof(Locale))]
[Index(nameof(Kind), nameof(Locale), nameof(Field), Name = "IX_metadata_object_i18n_Kind_Locale_Field")]
public sealed class MetadataObjectI18n
{
    /// <summary>
    /// 整对象兜底时用的 Field：该对象在各 locale 间结构本身就不一致
    /// （实测 GachaEvent 有 238/300 个、Material 有 14 个：CN 的 <c>UpOrangeList</c> 长度跟海外不同），
    /// 骨架放不下，于是这个 locale 的整个对象原样存在 Value 里。
    /// </summary>
    public const string RawField = "$";

    [MaxLength(32)]
    public string Kind { get; set; } = default!;

    public ulong GameId { get; set; }

    public uint SubId { get; set; }

    /// <summary>叶子在对象内的相对路径，与骨架里的占位符一致（实测最长 59 个字符）。等于 <see cref="RawField"/> 时 Value 是整个对象。</summary>
    [MaxLength(128)]
    public string Field { get; set; } = default!;

    [MaxLength(8)]
    public string Locale { get; set; } = default!;

    /// <summary>叶子的 JSON 原文（含引号），不是去引号后的字符串——数字、布尔、数组、对象都按这个口径存，才能逐字节还原。</summary>
    [Column(TypeName = "mediumtext")]
    public string Value { get; set; } = default!;

    [Column(TypeName = "datetime")]
    public DateTime UpdatedAt { get; set; }
}
