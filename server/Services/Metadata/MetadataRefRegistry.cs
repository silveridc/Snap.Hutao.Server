namespace server.Services.Metadata;

/// <summary>
/// <see cref="Models.Metadata.MetadataObjectRef"/> 的唯一数据来源：哪些字段是「指向别的分类的引用」。
/// <para>
/// 这份名单是人写的，并且每条都用真实 Id 全集验过命中率。之所以不能让程序自己找：
/// 实测「这个数在别的分类里也存在」会给出 <c>Achievement.FinishReward.Id → FurnitureMake</c> 这种 100% 命中的假阳性
/// （两个分类恰好都用了 201 这种小数），而 <c>MainQuest.ChapterId</c> 里 184 个值又恰好能在 <c>Material</c> 里找到——
/// 数字撞车在这份数据里是常态，不是意外。
/// </para>
/// <para>
/// 明确丢弃的候选（不是漏了，是验过不成立）：<c>Chapter.BeginQuestId</c>/<c>Chapter.EndQuestId</c>（对 430 个值命中率 0，
/// 它们用的是 <c>36301</c> 这种 ShowQuestId 号段，<c>MainQuest.Id</c> 是 <c>351</c> 这种任务段号）、
/// <c>TowerLevel.GroupId</c>/<c>TowerFloor.LevelGroupId</c>（与 <c>TowerFloor.Id</c> 不同号段）、
/// <c>AvatarPromote.Id</c>（是升阶系列号不是角色 Id）、<c>Avatar.Costumes/Id</c>、<c>Avatar.SkillDepot/**/Id</c>（metadata 里没有对应分类）。
/// </para>
/// </summary>
public static class MetadataRefRegistry
{
    /// <param name="Kind">发起引用的分类。</param>
    /// <param name="Path">对象内的路径，<c>*</c> 表示数组下标通配，如 <c>RewardList/*/Id</c>。</param>
    /// <param name="Role">边的语义名，进 <see cref="Models.Metadata.MetadataObjectRef.Role"/>。</param>
    /// <param name="RefKinds">目标分类。单元素就是确定的语义目标；多元素表示号段混在一起（卡池 UP 池、任务奖励），
    /// 按数组顺序取第一个能解析到的分类，都解析不到就按第一个写。</param>
    public sealed record Edge(string Kind, string Path, string Role, string[] RefKinds);

    /// <summary>0 在这份数据里一律表示「没有这个引用」，不建边。</summary>
    public const ulong NoRef = 0;

    private static readonly Edge[] Edges =
    [
        new("Achievement", "Goal", "Goal", ["AchievementGoal"]),
        new("Achievement", "PreviousId", "Previous", ["Achievement"]),
        new("Achievement", "FinishReward/Id", "FinishReward", ["Material"]),
        new("AchievementGoal", "FinishReward/Id", "FinishReward", ["NameCard", "Material"]),
        new("MainQuest", "ChapterId", "Chapter", ["Chapter"]),
        new("MainQuest", "RewardList/*/Id", "RewardItem", ["Avatar", "Weapon", "NameCard", "Furniture", "BeyondItem", "DisplayItem", "Reliquary", "Material"]),
        new("Combine", "Materials/*/Id", "CraftMaterial", ["Material"]),
        new("Combine", "Result/Id", "CraftResult", ["Material"]),
        new("CookRecipe", "Input/*/Id", "CookInput", ["Material"]),
        new("CookRecipe", "QualityOutput/*/Id", "CookOutput", ["Material"]),
        new("FurnitureMake", "Materials/*/Id", "MakeMaterial", ["Material"]),
        new("FurnitureMake", "ItemId", "MakeOutput", ["Furniture"]),
        new("Reliquary", "SetId", "Set", ["ReliquarySet"]),
        new("ReliquarySet", "EquipAffixId", "MainAffix", ["ReliquaryMainAffix"]),
        new("Weapon", "PromoteId", "Promote", ["WeaponPromote"]),
        new("WeaponPromote", "Id", "Weapon", ["Weapon"]),
        new("Avatar", "PromoteId", "Promote", ["AvatarPromote"]),
        new("TowerSchedule", "FloorIds/*", "Floor", ["TowerFloor"]),
        new("RoleCombatSchedule", "SpecialAvatars/*", "SpecialAvatar", ["Avatar"]),
        new("RoleCombatSchedule", "InitialAvatars/*", "InitialAvatar", ["Avatar"]),
        new("ReliquaryAffixWeight", "AvatarId", "Avatar", ["Avatar"]),
        new("GachaEvent", "UpOrangeList/*", "UpOrange", ["Avatar", "Weapon"]),
        new("GachaEvent", "UpPurpleList/*", "UpPurple", ["Avatar", "Weapon", "Material"]),
    ];

    public static IReadOnlyList<Edge> All => Edges;

    public static IReadOnlyList<Edge> For(string kind)
    {
        return Edges.Where(edge => string.Equals(edge.Kind, kind, StringComparison.Ordinal)).ToList();
    }
}
