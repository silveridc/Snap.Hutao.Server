namespace server.Models.Admin;

/// <summary>伤害分布，直接聚合 damage_ranks 明细表.</summary>
public sealed record DamageAnalytics(long TotalDamage, long SampleRecords, IReadOnlyList<DamageRankItem> TopAvatars);
