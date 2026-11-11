namespace server.Models.Admin;

/// <summary>卡池抽卡分布.全部来自 MySQL 明细，不依赖 Redis 快照.</summary>
/// <param name="TotalPulls">统计区间内的有效抽数.</param>
/// <param name="TotalStar5">其中五星数量.</param>
/// <param name="UntrustedPulls">被标记为不可信的抽数，未计入统计.</param>
/// <param name="Days">统计区间天数，0 表示不限.</param>
/// <param name="Pools">按卡池类型拆分的抽数.</param>
/// <param name="TopStar5Items">出得最多的五星物品.</param>
public sealed record GachaAnalytics(
    long TotalPulls,
    long TotalStar5,
    long UntrustedPulls,
    int Days,
    IReadOnlyList<GachaPoolAnalytics> Pools,
    IReadOnlyList<GachaItemRank> TopStar5Items);
