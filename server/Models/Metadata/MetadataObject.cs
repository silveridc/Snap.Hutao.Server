namespace server.Models.Metadata;

/// <summary>
/// 一个游戏对象的中立骨架：全 locale 共用一份，随语言变化的叶子在骨架里只留占位符，
/// 真值在 <see cref="MetadataObjectI18n"/>。
/// <para>
/// 这张表是派生数据，任何时候都能从 <c>metadata_document</c> 重建；
/// 对外服务仍走原文档表，因为 <see cref="Doc"/> 是本服务自己序列化的，键序与数字写法跟上游一致，
/// 但整篇文档的字节要由 <c>MetadataObjectAssembler</c> 拼出来才能与上游逐字节相同。
/// </para>
/// </summary>
[Table("metadata_object")]
[PrimaryKey(nameof(Kind), nameof(GameId), nameof(SubId))]
[Index(nameof(Kind), nameof(Ordinal), Name = "IX_metadata_object_Kind_Ordinal")]
[Index(nameof(Kind), nameof(Quality), Name = "IX_metadata_object_Kind_Quality")]
[Index(nameof(Kind), nameof(Rank), Name = "IX_metadata_object_Kind_Rank")]
[Index(nameof(Kind), nameof(RankLevel), Name = "IX_metadata_object_Kind_RankLevel")]
[Index(nameof(Kind), nameof(Type), Name = "IX_metadata_object_Kind_Type")]
[Index(nameof(Kind), nameof(ItemType), Name = "IX_metadata_object_Kind_ItemType")]
[Index(nameof(Kind), nameof(MaterialType), Name = "IX_metadata_object_Kind_MaterialType")]
[Index(nameof(Kind), nameof(WeaponType), Name = "IX_metadata_object_Kind_WeaponType")]
[Index(nameof(Kind), nameof(SetId), Name = "IX_metadata_object_Kind_SetId")]
[Index(nameof(Kind), nameof(PromoteId), Name = "IX_metadata_object_Kind_PromoteId")]
[Index(nameof(Kind), nameof(ChapterId), Name = "IX_metadata_object_Kind_ChapterId")]
[Index(nameof(Kind), nameof(Sort), Name = "IX_metadata_object_Kind_Sort")]
public sealed class MetadataObject
{
    [MaxLength(32)]
    public string Kind { get; set; } = default!;

    /// <summary>主游戏 ID，取自 <see cref="MetadataKind.GameIdPath"/>；<c>GameIdPath</c> 为 <see cref="MetadataIdPaths.IndexPosition"/> 时就是文档下标。</summary>
    public ulong GameId { get; set; }

    /// <summary>次键（<c>AvatarPromote</c> 的等级等）；主键已唯一时为 0，所以身份是 (Kind, GameId, SubId) 而不是 (Kind, GameId)。</summary>
    public uint SubId { get; set; }

    /// <summary>骨架取自哪个 locale 的文档。CHS 不是超集（CHT 的 MainQuest 有 4412 个对象而 CHS 只有 2696 个），只有这个 locale 缺该对象时才轮到别的。</summary>
    [MaxLength(8)]
    public string SourceLocale { get; set; } = default!;

    /// <summary>对象在 <see cref="SourceLocale"/> 文档里的下标，用于展示顺序；各 locale 的真实顺序看 <see cref="MetadataObjectLayout"/>。</summary>
    public uint Ordinal { get; set; }

    /// <summary>骨架 JSON 文本（2 空格缩进、LF）。随 locale 变的叶子写成 <c>"\u0000i18n:/路径"</c>，空对象/空数组原样保留。</summary>
    [Column(TypeName = "mediumtext")]
    public string Doc { get; set; } = default!;

    /// <summary>以下都是 <see cref="Doc"/> 上的 STORED 生成列，只用于过滤和排序；键不存在或值是占位符时为 null。</summary>
    public uint? Quality { get; set; }

    public uint? Rank { get; set; }

    public uint? RankLevel { get; set; }

    public long? Type { get; set; }

    public uint? ItemType { get; set; }

    public uint? MaterialType { get; set; }

    public uint? WeaponType { get; set; }

    public ulong? SetId { get; set; }

    public ulong? PromoteId { get; set; }

    public ulong? ChapterId { get; set; }

    public long? Sort { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedAt { get; set; }
}
