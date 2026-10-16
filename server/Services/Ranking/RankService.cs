using server.Models.Entity.SpiralAbyss;
using server.Models.Legacy;
using server.Models.Upload;
using StackExchange.Redis;

namespace server.Services.Ranking
{
    // Singleton
    internal sealed class RankService(IServiceScopeFactory scopeFactory, AppOptions appOptions, ILogger<RankService> logger) : IRankService, IDisposable
    {
        private readonly ConnectionMultiplexer redis = Connect(appOptions.RedisAddress, logger);

        /// <summary>
        /// 将排行存入 Redis
        /// </summary>
        /// <param name="uid">uid</param>
        /// <param name="damage">造成伤害</param>
        /// <param name="takeDamage">受到伤害</param>
        /// <returns>任务</returns>
        public async Task SaveRankAsync(string uid, SimpleRank damage, SimpleRank? takeDamage)
        {
            IDatabase db = redis.GetDatabase(12);

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
        /// 异步获取排行
        /// </summary>
        /// <param name="uid">uid</param>
        /// <returns>排行数据</returns>
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

            IDatabase db = redis.GetDatabase(12);

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
        /// 异步清除排行
        /// </summary>
        /// <returns>任务</returns>
        public async Task<long> ClearRanksAsync()
        {
            IDatabase db = redis.GetDatabase(12);

            RedisValue[] keys = await db.SetMembersAsync("Hutao.UsedKeys").ConfigureAwait(false);
            RedisKey[] usedKeys = [.. keys.Select(k => new RedisKey(k))];

            return await db.KeyDeleteAsync(usedKeys).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            redis.Dispose();
        }

        public async Task<List<string>> GetRankKeysAsync()
        {
            IDatabase db = redis.GetDatabase(12);
            RedisValue[] keys = await db.SetMembersAsync("Hutao.UsedKeys").ConfigureAwait(false);
            return [.. keys.Select(k => k.ToString()).OrderBy(k => k, StringComparer.Ordinal)];
        }

        public async Task<List<Models.Admin.RankEntry>> GetLeaderboardAsync(string key, int count)
        {
            IDatabase db = redis.GetDatabase(12);
            count = Math.Clamp(count, 1, 500);
            SortedSetEntry[] entries = await db.SortedSetRangeByRankWithScoresAsync(key, 0, count - 1, Order.Descending).ConfigureAwait(false);
            return [.. entries.Select(e => new Models.Admin.RankEntry { Uid = e.Element.ToString(), Value = e.Score })];
        }

        private static double GetRankValue(long index, long total)
        {
            return total == 0 ? 0 : (index + 1) / (double)total;
        }

        private static ConnectionMultiplexer Connect(string redisAddress, ILogger<RankService> logger)
        {
            logger.LogInformation("Using Redis: {config}", redisAddress);
            return ConnectionMultiplexer.Connect(redisAddress);
        }
    }
}