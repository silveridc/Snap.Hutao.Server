using server.Models.Metadata;

namespace server.Services.Metadata;

/// <summary>
/// 每个分类「一个对象是谁」的取法。这份名单是实测出来的，不是从数据里推的：
/// <list type="bullet">
/// <item><description><c>Reliquary</c> 的 <c>SetId</c> 只有 66 个值却对应 890 个对象（一套圣遗物 5 个部位 × 5 个等级），
/// 用 <c>SetId</c> 当身份会把 13 个不同对象并成一个；改用 <c>Ids/0</c> 后 890 个身份互不相同。</description></item>
/// <item><description><c>Monster</c> 的 <c>Id</c> 有 88 个对象取同一个值（机关类都是 0），改用 <c>DescribeId</c> 后 562 个身份互不相同。</description></item>
/// <item><description><c>AvatarPromote</c>/<c>WeaponPromote</c>/<c>ReliquaryMainAffixLevel</c> 主键天然重复，
/// 必须带上次键 <c>Level</c>/<c>Rank</c> 才唯一。</description></item>
/// <item><description><c>GachaEvent</c> 里没有 ID 字段（<c>Version</c> 只有 53 个不同值对 301 个对象），只能按文档下标认。</description></item>
/// </list>
/// 全部路径都验证过：取出来的值都是非负整数（所以 <see cref="MetadataObject.GameId"/> 用 <c>bigint unsigned</c>），
/// 且在 15 个 locale 上身份数一致。改这里等于改主键，得重跑投影。
/// </summary>
public static class MetadataKindRegistry
{
    /// <summary>一个分类的定义。</summary>
    /// <param name="Kind">分类名，也是 <see cref="MetadataObject.Kind"/>。</param>
    /// <param name="DocumentName">数组类分类等于文档名本身；<c>Avatar</c> 是目录名，服务时展开成 <c>Avatar/{id}.json</c>。</param>
    /// <param name="RootShape">文档根是数组还是一个对象。</param>
    /// <param name="GameIdPath">主身份路径，<see cref="MetadataIdPaths.IndexPosition"/> 表示按文档下标认。</param>
    /// <param name="SubIdPath">次身份路径，空串表示主键已唯一。</param>
    public sealed record Definition(string Kind, string DocumentName, MetadataRootShape RootShape, string GameIdPath, string SubIdPath);

    private static readonly Definition[] Definitions =
    [
        Flat("Achievement"),
        Flat("AchievementGoal"),
        new("AvatarCurve", "AvatarCurve", MetadataRootShape.Array, "Level", string.Empty),
        new("AvatarPromote", "AvatarPromote", MetadataRootShape.Array, "Id", "Level"),
        Flat("BeyondItem"),
        Flat("Chapter"),
        Flat("Combine"),
        Flat("CookRecipe"),
        Flat("DisplayItem"),
        Flat("Furniture"),
        Flat("FurnitureMake"),
        Flat("FurnitureSuite"),
        Flat("FurnitureType"),
        new("GachaEvent", "GachaEvent", MetadataRootShape.Array, MetadataIdPaths.IndexPosition, string.Empty),
        Flat("HardChallengeSchedule"),
        Flat("HyperLinkName"),
        Flat("MainQuest"),
        Flat("Material"),
        new("Monster", "Monster", MetadataRootShape.Array, "DescribeId", string.Empty),
        new("MonsterCurve", "MonsterCurve", MetadataRootShape.Array, "Level", string.Empty),
        Flat("NameCard"),
        Flat("ProfilePicture"),
        new("Reliquary", "Reliquary", MetadataRootShape.Array, "Ids/0", string.Empty),
        new("ReliquaryAffixWeight", "ReliquaryAffixWeight", MetadataRootShape.Array, "AvatarId", string.Empty),
        Flat("ReliquaryMainAffix"),
        new("ReliquaryMainAffixLevel", "ReliquaryMainAffixLevel", MetadataRootShape.Array, "Rank", "Level"),
        new("ReliquarySet", "ReliquarySet", MetadataRootShape.Array, "SetId", string.Empty),
        Flat("ReliquarySubAffix"),
        Flat("RoleCombatSchedule"),
        Flat("TowerFloor"),
        Flat("TowerLevel"),
        Flat("TowerSchedule"),
        Flat("Weapon"),
        new("WeaponCurve", "WeaponCurve", MetadataRootShape.Array, "Level", string.Empty),
        new("WeaponPromote", "WeaponPromote", MetadataRootShape.Array, "Id", "Level"),
        new("Avatar", "Avatar", MetadataRootShape.Scattered, "Id", string.Empty),
    ];

    /// <summary>按定义顺序给出（<c>Meta.json</c> 之类的清单文档不在里面，它不是对象集合）。</summary>
    public static IReadOnlyList<Definition> All => Definitions;

    public static Definition? Find(string kind)
    {
        return Definitions.FirstOrDefault(definition => string.Equals(definition.Kind, kind, StringComparison.Ordinal));
    }

    /// <summary>
    /// 按 <c>metadata_document.Name</c> 找它属于哪个分类：平铺分类按全名，散文件按目录名前缀
    /// （<c>Avatar/10000002</c> 归 <c>Avatar</c>）。返回 <see langword="null"/> 表示这份文档不在读模型的覆盖范围内。
    /// </summary>
    public static Definition? FindByName(string name)
    {
        return Definitions.FirstOrDefault(definition => definition.RootShape is MetadataRootShape.Array
            ? string.Equals(definition.DocumentName, name, StringComparison.Ordinal)
            : name.StartsWith($"{definition.DocumentName}/", StringComparison.Ordinal));
    }

    private static Definition Flat(string kind)
    {
        return new(kind, kind, MetadataRootShape.Array, "Id", string.Empty);
    }
}
