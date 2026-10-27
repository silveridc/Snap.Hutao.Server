namespace server.Models.Admin;

/// <summary>卡池抽卡分布。全部来自 MySQL 明细，不依赖 Redis 快照。</summary>
/// <param name="TotalPulls">统计区间内的有效抽数。</param>
/// <param name="TotalStar5">其中五星数量。</param>
/// <param name="UntrustedPulls">被标记为不可信的抽数，未计入统计。</param>
/// <param name="Days">统计区间天数，0 表示不限。</param>
/// <param name="Pools">按卡池类型拆分的抽数。</param>
/// <param name="TopStar5Items">出得最多的五星物品。</param>
public sealed record GachaAnalytics(
    long TotalPulls,
    long TotalStar5,
    long UntrustedPulls,
    int Days,
    IReadOnlyList<GachaPoolAnalytics> Pools,
    IReadOnlyList<GachaItemRank> TopStar5Items);

public sealed record GachaPoolAnalytics(int GachaType, long Pulls, long Star5, double Star5Rate, double AveragePullsPerStar5);

public sealed record GachaItemRank(int ItemId, long Count);

/// <summary>两个已支持的深渊玩法：深境螺旋与幻想真境剧诗。</summary>
public sealed record AbyssAnalytics(AbyssModeAnalytics SpiralAbyss, AbyssModeAnalytics Theater);

public sealed record AbyssModeAnalytics(
    long Records,
    int UniqueUids,
    long FullStarRecords,
    IReadOnlyList<AbyssFloorAnalytics> Floors,
    IReadOnlyList<AvatarRank> TopAvatars);

public sealed record AbyssFloorAnalytics(int Index, long Records, double AverageStar);

public sealed record AvatarRank(int AvatarId, long Count);

/// <summary>伤害分布，直接聚合 damage_ranks 明细表。</summary>
public sealed record DamageAnalytics(long TotalDamage, long SampleRecords, IReadOnlyList<DamageRankItem> TopAvatars);

public sealed record DamageRankItem(int AvatarId, long TotalDamage, double Share);
