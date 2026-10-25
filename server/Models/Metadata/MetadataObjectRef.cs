namespace server.Models.Metadata;

/// <summary>
/// 对象之间的引用边，供「哪个任务奖励了这颗魔拉」「这套圣遗物有哪些部件」这类反查。
/// <para>
/// Role→RefKind 的对应关系是代码里白名单化的一张表（<c>Services/Metadata/MetadataRefs.cs</c>），
/// 不是从数据里猜的：实测靠「这个数在别的分类里也存在」来推断会得出
/// <c>Achievement.FinishReward.Id → FurnitureMake</c> 这种假阳性，
/// 而 <c>Avatar.SkillDepot.Talents/Id</c> 这类 ID 在 metadata 里根本没有对应分类。
/// </para>
/// <para>只从骨架里不随 locale 变的数字取边；变成占位符的路径（如 <c>Material.Id</c> 在海外多出的条目）宁可不建边。</para>
/// </summary>
[Table("metadata_object_ref")]
[PrimaryKey(nameof(Kind), nameof(GameId), nameof(SubId), nameof(Role), nameof(Ordinal))]
[Index(nameof(RefKind), nameof(RefId), nameof(Role), Name = "IX_metadata_object_ref_Target")]
public sealed class MetadataObjectRef
{
    [MaxLength(32)]
    public string Kind { get; set; } = default!;

    public ulong GameId { get; set; }

    public uint SubId { get; set; }

    /// <summary>引用角色的语义名，如 <c>RewardItem</c>、<c>CraftMaterial</c>、<c>Set</c>。</summary>
    [MaxLength(64)]
    public string Role { get; set; } = default!;

    /// <summary>同一角色下的第几条（<c>RewardList</c> 里的下标），单值角色固定 0。</summary>
    public uint Ordinal { get; set; }

    [MaxLength(32)]
    public string RefKind { get; set; } = default!;

    public ulong RefId { get; set; }
}
