using server.Models.Admin;
using server.Models.Entity.GachaLog;
using server.Models.Entity.RoleCombat;
using server.Models.Entity.SpiralAbyss;

namespace server.Services.Admin;

/// <summary>
/// Back-office data analytics. All figures are aggregated from the MySQL detail tables instead of Redis snapshots,
/// so a lost cache never empties the charts.
/// </summary>
public sealed class AnalyticsService(AppDbContext appDbContext, IDistributedCache cache, TimeProvider timeProvider)
{
    private const int _maxTopItems = 15;
    private const int _maxTopAvatars = 10;
    private const int _fullStarThreshold = 36;
    private const int _maxDays = 3650;

    // 聚合查询要扫 gacha_items / damage_ranks 明细，后台短时间反复翻页没必要重复算
    private const string _cacheKeyPrefix = "Admin:Analytics:";
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets the gacha pull distribution per pool type, including the five-star items pulled most often.
    /// </summary>
    /// <param name="days">The lookback window in days; 0 means all history.</param>
    /// <returns>The gacha analytics.</returns>
    public async Task<GachaAnalytics> GetGachaAsync(int days = 30)
    {
        days = Math.Clamp(days, 0, _maxDays);
        return (await cache.GetOrCreateAsync($"{_cacheKeyPrefix}Gacha:{days}", _cacheDuration, () => BuildGachaAsync(days)).ConfigureAwait(false))!;
    }

    /// <summary>
    /// Gets the analytics of both abyss modes: Spiral Abyss and Imaginarium Theater.
    /// </summary>
    /// <returns>The abyss analytics.</returns>
    public async Task<AbyssAnalytics> GetAbyssAsync()
    {
        return (await cache.GetOrCreateAsync($"{_cacheKeyPrefix}Abyss", _cacheDuration, BuildAbyssAsync).ConfigureAwait(false))!;
    }

    /// <summary>
    /// Gets the damage distribution aggregated directly from the damage-rank detail rows.
    /// </summary>
    /// <param name="top">The number of avatars to return, clamped to the range 1-50.</param>
    /// <returns>The damage analytics.</returns>
    public async Task<DamageAnalytics> GetDamageAsync(int top = _maxTopAvatars)
    {
        top = Math.Clamp(top, 1, 50);
        return (await cache.GetOrCreateAsync($"{_cacheKeyPrefix}Damage:{top}", _cacheDuration, () => BuildDamageAsync(top)).ConfigureAwait(false))!;
    }

    /// <summary>
    /// Builds the gacha analytics from the detail tables for the given lookback window.
    /// </summary>
    /// <param name="days">The lookback window in days; 0 means all history.</param>
    /// <returns>The gacha analytics.</returns>
    private async Task<GachaAnalytics> BuildGachaAsync(int days)
    {
        long cutoff = days is 0 ? 0 : timeProvider.GetUtcNow().AddDays(-days).ToUnixTimeSeconds();
        DateTimeOffset cutoffTime = DateTimeOffset.FromUnixTimeSeconds(cutoff);

        IQueryable<EntityGachaItem> trusted = appDbContext.GachaItems.AsNoTracking().Where(item => item.IsTrusted);
        if (cutoff is not 0)
        {
            trusted = trusted.Where(item => item.Time >= cutoffTime);
        }

        long totalPulls = await trusted.LongCountAsync().ConfigureAwait(false);
        long untrustedPulls = await appDbContext.GachaItems.AsNoTracking()
            .Where(item => !item.IsTrusted && (cutoff == 0 || item.Time >= cutoffTime))
            .LongCountAsync()
            .ConfigureAwait(false);

        // 五星判定依赖 known_items 的品质表.Id 是 uint、GachaItem.ItemId 是 int，
        // EF 翻不了这种跨类型联表，所以先把五星 id 取到内存再 Contains.
        HashSet<int> star5Ids = [.. (await appDbContext.KnownItems.AsNoTracking()
            .Where(known => known.Quality == 5)
            .Select(known => known.Id)
            .ToListAsync()
            .ConfigureAwait(false)).Select(id => (int)id)];

        IQueryable<EntityGachaItem> star5 = trusted.Where(item => star5Ids.Contains(item.ItemId));
        long totalStar5 = star5Ids.Count is 0 ? 0 : await star5.LongCountAsync().ConfigureAwait(false);

        List<GachaItemRank> topStar5Items = star5Ids.Count is 0
            ? []
            : [.. (await star5
                .GroupBy(item => item.ItemId)
                .Select(group => new { ItemId = group.Key, Count = group.LongCount() })
                .OrderByDescending(entry => entry.Count)
                .ThenBy(entry => entry.ItemId)
                .Take(_maxTopItems)
                .ToListAsync()
                .ConfigureAwait(false))
                .Select(entry => new GachaItemRank(entry.ItemId, entry.Count))];

        Dictionary<int, long> pullsByType = await trusted
            .GroupBy(item => item.GachaType)
            .Select(group => new { GachaType = (int)group.Key, Pulls = group.LongCount() })
            .ToDictionaryAsync(entry => entry.GachaType, entry => entry.Pulls)
            .ConfigureAwait(false);

        Dictionary<int, long> star5ByType = star5Ids.Count is 0
            ? []
            : await star5
                .GroupBy(item => item.GachaType)
                .Select(group => new { GachaType = (int)group.Key, Count = group.LongCount() })
                .ToDictionaryAsync(entry => entry.GachaType, entry => entry.Count)
                .ConfigureAwait(false);

        List<GachaPoolAnalytics> pools =
        [
            .. pullsByType
                .OrderBy(pair => pair.Key)
                .Select(pair =>
                {
                    long poolStar5 = star5ByType.GetValueOrDefault(pair.Key);
                    double rate = pair.Value is 0 ? 0 : (double)poolStar5 / pair.Value;
                    double average = poolStar5 is 0 ? 0 : Math.Round((double)pair.Value / poolStar5, 2);
                    return new GachaPoolAnalytics(pair.Key, pair.Value, poolStar5, rate, average);
                }),
        ];

        return new GachaAnalytics(totalPulls, totalStar5, untrustedPulls, days, pools, topStar5Items);
    }

    /// <summary>
    /// Builds the combined analytics of both abyss modes.
    /// </summary>
    /// <returns>The abyss analytics.</returns>
    private async Task<AbyssAnalytics> BuildAbyssAsync()
    {
        return new(await GetSpiralAbyssAsync().ConfigureAwait(false), await GetTheaterAsync().ConfigureAwait(false));
    }

    /// <summary>
    /// Builds the damage analytics from the damage-rank detail rows.
    /// </summary>
    /// <param name="top">The number of avatars to include.</param>
    /// <returns>The damage analytics.</returns>
    private async Task<DamageAnalytics> BuildDamageAsync(int top)
    {
        IQueryable<EntityDamageRank> ranks = appDbContext.DamageRanks.AsNoTracking();
        long totalDamage = await ranks.SumAsync(rank => (long)rank.Value).ConfigureAwait(false);
        long sampleRecords = await ranks.Select(rank => rank.SpiralAbyssId).Distinct().LongCountAsync().ConfigureAwait(false);

        List<(int AvatarId, long Total)> grouped = [.. (await ranks
            .GroupBy(rank => rank.AvatarId)
            .Select(group => new { AvatarId = group.Key, Total = group.Sum(rank => (long)rank.Value) })
            .OrderByDescending(entry => entry.Total)
            .ThenBy(entry => entry.AvatarId)
            .Take(top)
            .ToListAsync()
            .ConfigureAwait(false))
            .Select(entry => (entry.AvatarId, entry.Total))];

        List<DamageRankItem> items =
        [
            .. grouped.Select(entry => new DamageRankItem(entry.AvatarId, entry.Total, totalDamage is 0 ? 0 : (double)entry.Total / totalDamage)),
        ];

        return new DamageAnalytics(totalDamage, sampleRecords, items);
    }

    /// <summary>
    /// Builds the Spiral Abyss analytics.
    /// </summary>
    /// <returns>The Spiral Abyss analytics.</returns>
    private async Task<AbyssModeAnalytics> GetSpiralAbyssAsync()
    {
        IQueryable<EntityRecord> records = appDbContext.Records.AsNoTracking().Where(record => record.SpiralAbyss != null);
        long count = await records.LongCountAsync().ConfigureAwait(false);
        int uniqueUids = await records.Select(record => record.Uid).Distinct().CountAsync().ConfigureAwait(false);

        long fullStar = await appDbContext.SpiralAbyssFloors.AsNoTracking()
            .GroupBy(floor => floor.SpiralAbyssId)
            .Select(group => new { SpiralAbyssId = group.Key, Stars = group.Sum(floor => floor.Star) })
            .Where(entry => entry.Stars >= _fullStarThreshold)
            .CountAsync()
            .ConfigureAwait(false);

        List<AbyssFloorAnalytics> floors = [.. (await appDbContext.SpiralAbyssFloors.AsNoTracking()
            .GroupBy(floor => floor.Index)
            .Select(group => new { Index = group.Key, Records = group.LongCount(), AverageStar = group.Average(floor => (double)floor.Star) })
            .ToListAsync()
            .ConfigureAwait(false))
            .OrderBy(entry => entry.Index)
            .Select(entry => new AbyssFloorAnalytics(entry.Index, entry.Records, Math.Round(entry.AverageStar, 2)))];

        List<AvatarRank> topAvatars = [.. (await appDbContext.Avatars.AsNoTracking()
            .GroupBy(avatar => avatar.AvatarId)
            .Select(group => new { AvatarId = group.Key, Count = group.LongCount() })
            .OrderByDescending(entry => entry.Count)
            .ThenBy(entry => entry.AvatarId)
            .Take(_maxTopAvatars)
            .ToListAsync()
            .ConfigureAwait(false))
            .Select(entry => new AvatarRank(entry.AvatarId, entry.Count))];

        return new AbyssModeAnalytics(count, uniqueUids, fullStar, floors, topAvatars);
    }

    /// <summary>
    /// Builds the Imaginarium Theater analytics.
    /// </summary>
    /// <returns>The Imaginarium Theater analytics.</returns>
    private async Task<AbyssModeAnalytics> GetTheaterAsync()
    {
        IQueryable<RoleCombatRecord> records = appDbContext.RoleCombatRecords.AsNoTracking();
        long count = await records.LongCountAsync().ConfigureAwait(false);
        int uniqueUids = await records.Select(record => record.Uid).Distinct().CountAsync().ConfigureAwait(false);

        List<AvatarRank> topAvatars = [.. (await appDbContext.RoleCombatAvatars.AsNoTracking()
            .GroupBy(avatar => avatar.AvatarId)
            .Select(group => new { AvatarId = group.Key, Count = group.LongCount() })
            .OrderByDescending(entry => entry.Count)
            .ThenBy(entry => entry.AvatarId)
            .Take(_maxTopAvatars)
            .ToListAsync()
            .ConfigureAwait(false))
            .Select(entry => new AvatarRank((int)entry.AvatarId, entry.Count))];

        return new AbyssModeAnalytics(count, uniqueUids, 0, [], topAvatars);
    }
}
