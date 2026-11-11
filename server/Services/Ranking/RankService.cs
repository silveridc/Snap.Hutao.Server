using server.Models.Entity.SpiralAbyss;
using server.Models.Legacy;
using server.Models.Upload;
using StackExchange.Redis;

namespace server.Services.Ranking;

// Singleton
internal sealed class RankService(IServiceScopeFactory scopeFactory, IConnectionMultiplexer redis) : IRankService
{
    private readonly IDatabase db = redis.GetDatabase(1);

    /// <summary>
    /// Saves the damage and damage-taken values of the specified user into the per-avatar and overall Redis leaderboards.
    /// </summary>
    /// <param name="uid">The user identifier.</param>
    /// <param name="damage">The damage dealt by the user.</param>
    /// <param name="takeDamage">The damage taken by the user, or <see langword="null"/> if it was not submitted.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public async Task SaveRankAsync(string uid, SimpleRank damage, SimpleRank? takeDamage)
    {
        // 保存使用过的键
        await db.SetAddAsync("Hutao.UsedKeys", "Hutao.Damage.Avatar.All").ConfigureAwait(false);
        await db.SetAddAsync("Hutao.UsedKeys", "Hutao.TakeDamage.Avatar.All").ConfigureAwait(false);

        // 造成伤害
        await db.SetAddAsync("Hutao.UsedKeys", $"Hutao.Damage.Avatar.{damage.AvatarId}").ConfigureAwait(false);
        await db.SortedSetAddAsync($"Hutao.Damage.Avatar.{damage.AvatarId}", uid, damage.Value).ConfigureAwait(false);
        await db.SortedSetAddAsync("Hutao.Damage.Avatar.All", uid, damage.Value).ConfigureAwait(false);

        // 受到伤害
        if (takeDamage != null)
        {
            await db.SetAddAsync("Hutao.UsedKeys", $"Hutao.TakeDamage.Avatar.{takeDamage.AvatarId}").ConfigureAwait(false);
            await db.SortedSetAddAsync($"Hutao.TakeDamage.Avatar.{takeDamage.AvatarId}", uid, takeDamage.Value).ConfigureAwait(false);
            await db.SortedSetAddAsync("Hutao.TakeDamage.Avatar.All", uid, takeDamage.Value).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Retrieves the leaderboard ranks of the specified user from Redis, based on the latest damage records.
    /// </summary>
    /// <param name="uid">The user identifier.</param>
    /// <returns>The damage and damage-taken rank information of the user.</returns>
    public async Task<Rank> RetriveRankAsync(string uid)
    {
        EntityDamageRank? damage;
        EntityTakeDamageRank? takeDamage;

        using (IServiceScope scope = scopeFactory.CreateScope())
        {
            AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            damage = appDbContext.DamageRanks.AsNoTracking().SingleOrDefault(r => r.Uid == uid);
            takeDamage = appDbContext.TakeDamageRanks.AsNoTracking().SingleOrDefault(r => r.Uid == uid);
        }

        if (damage == null)
        {
            return new(null, null);
        }

        long damageIndex = await db.SortedSetRankAsync("Hutao.Damage.Avatar.All", uid).ConfigureAwait(false) ?? 0;
        long damageTotal = await db.SortedSetLengthAsync("Hutao.Damage.Avatar.All").ConfigureAwait(false);
        long avatarDamageIndex = await db.SortedSetRankAsync($"Hutao.Damage.Avatar.{damage.AvatarId}", uid).ConfigureAwait(false) ?? 0;
        long avatarDamageTotal = await db.SortedSetLengthAsync($"Hutao.Damage.Avatar.{damage.AvatarId}").ConfigureAwait(false);

        RankValue damageValue = new(
            damage.AvatarId,
            damage.Value,
            GetRankValue(damageIndex, damageTotal),
            GetRankValue(avatarDamageIndex, avatarDamageTotal));

        if (takeDamage == null)
        {
            return new Rank(damageValue, null);
        }

        long takeDamageIndex = await db.SortedSetRankAsync("Hutao.TakeDamage.Avatar.All", uid).ConfigureAwait(false) ?? 0;
        long takeDamageTotal = await db.SortedSetLengthAsync("Hutao.TakeDamage.Avatar.All").ConfigureAwait(false);
        long avatarTakeDamageIndex = await db.SortedSetRankAsync($"Hutao.TakeDamage.Avatar.{damage.AvatarId}", uid).ConfigureAwait(false) ?? 0;
        long avatarTakeDamageTotal = await db.SortedSetLengthAsync($"Hutao.TakeDamage.Avatar.{damage.AvatarId}").ConfigureAwait(false);

        RankValue takeDamageValue = new(
            takeDamage.AvatarId,
            takeDamage.Value,
            GetRankValue(takeDamageIndex, takeDamageTotal),
            GetRankValue(avatarTakeDamageIndex, avatarTakeDamageTotal));

        return new Rank(damageValue, takeDamageValue);
    }

    /// <summary>
    /// Deletes every leaderboard key previously recorded by the service.
    /// </summary>
    /// <returns>The number of Redis keys that were removed.</returns>
    public async Task<long> ClearRanksAsync()
    {
        RedisValue[] keys = await db.SetMembersAsync("Hutao.UsedKeys").ConfigureAwait(false);
        RedisKey[] usedKeys = [.. keys.Select(k => new RedisKey(k))];

        return await db.KeyDeleteAsync(usedKeys).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the names of all leaderboard keys tracked in Redis, sorted by name.
    /// </summary>
    /// <returns>The sorted leaderboard key names.</returns>
    public async Task<List<string>> GetRankKeysAsync()
    {
        RedisValue[] keys = await db.SetMembersAsync("Hutao.UsedKeys").ConfigureAwait(false);
        return [.. keys.Select(k => k.ToString()).OrderBy(k => k, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Gets the top entries of the specified leaderboard in descending score order.
    /// </summary>
    /// <param name="key">The leaderboard key name.</param>
    /// <param name="count">The number of entries to return, clamped to the range 1-500.</param>
    /// <returns>The leaderboard entries ordered by score descending.</returns>
    public async Task<List<Models.Admin.RankEntry>> GetLeaderboardAsync(string key, int count)
    {
        count = Math.Clamp(count, 1, 500);
        SortedSetEntry[] entries = await db.SortedSetRangeByRankWithScoresAsync(key, 0, count - 1, Order.Descending).ConfigureAwait(false);
        return [.. entries.Select(e => new Models.Admin.RankEntry { Uid = e.Element.ToString(), Value = e.Score })];
    }

    /// <summary>
    /// Converts a zero-based sorted-set index into a 1-based rank ratio within the total number of entries.
    /// </summary>
    /// <param name="index">The zero-based index of the entry.</param>
    /// <param name="total">The total number of entries in the leaderboard.</param>
    /// <returns>The rank ratio in the range (0, 1], or 0 when the leaderboard is empty.</returns>
    private static double GetRankValue(long index, long total)
    {
        return total == 0 ? 0 : (index + 1) / (double)total;
    }
}
