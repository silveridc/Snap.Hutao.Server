using server.Extension;
using StackExchange.Redis;

namespace server.Services.Cache
{
    // Singleton
    public sealed class CacheService(IConnectionMultiplexer multiplexer, IDistributedCache cache, ILogger<CacheService> logger)
    {
        private const int ScanPageSize = 512;

        /// <summary>
        /// 按精确键批量移除缓存（逻辑键，前缀由 <see cref="IDistributedCache"/> 自动补齐）。
        /// </summary>
        public async Task<int> RemoveKeysAsync(IEnumerable<string> keys)
        {
            int count = 0;
            foreach (string key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                await cache.RemoveAsync(key).ConfigureAwait(false);
                count++;
            }

            return count;
        }

        /// <summary>
        /// 按 glob 模式批量移除缓存（逻辑模式，前缀自动补齐，例如 <c>RoleCombatStatistics:*</c> 或 <c>*</c>）。
        /// </summary>
        public async Task<long> RemoveByPatternsAsync(IEnumerable<string> patterns)
        {
            long removed = 0;
            foreach (string pattern in patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                removed += await RemoveMatchingAsync($"{DistributedCacheExtension.InstanceName}{pattern}").ConfigureAwait(false);
            }

            return removed;
        }

        /// <summary>
        /// 列出匹配指定 glob 模式的逻辑缓存键（已去除实例前缀），供管理界面查看。
        /// </summary>
        public List<string> ListKeys(string pattern, int limit = 500)
        {
            string effectivePattern = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern;
            string fullPattern = $"{DistributedCacheExtension.InstanceName}{effectivePattern}";

            List<string> result = [];
            IDatabase db = multiplexer.GetDatabase();
            foreach (IServer server in GetWritableServers())
            {
                foreach (RedisKey key in server.Keys(db.Database, pattern: fullPattern, pageSize: ScanPageSize))
                {
                    result.Add(StripPrefix(key!));
                    if (result.Count >= limit)
                    {
                        return result;
                    }
                }
            }

            return result;
        }

        private async Task<long> RemoveMatchingAsync(string fullPattern)
        {
            IDatabase db = multiplexer.GetDatabase();
            long removed = 0;
            List<RedisKey> batch = [];

            foreach (IServer server in GetWritableServers())
            {
                await foreach (RedisKey key in server.KeysAsync(db.Database, pattern: fullPattern, pageSize: ScanPageSize).ConfigureAwait(false))
                {
                    batch.Add(key);
                    if (batch.Count >= ScanPageSize)
                    {
                        removed += await db.KeyDeleteAsync([.. batch]).ConfigureAwait(false);
                        batch.Clear();
                    }
                }

                if (batch.Count > 0)
                {
                    removed += await db.KeyDeleteAsync([.. batch]).ConfigureAwait(false);
                    batch.Clear();
                }
            }

            logger.LogInformation("Cache invalidation removed {Removed} key(s) matching {Pattern}", removed, fullPattern);
            return removed;
        }

        private IEnumerable<IServer> GetWritableServers()
        {
            foreach (EndPoint endPoint in multiplexer.GetEndPoints())
            {
                IServer server = multiplexer.GetServer(endPoint);
                if (server.IsConnected && !server.IsReplica)
                {
                    yield return server;
                }
            }
        }

        private static string StripPrefix(string key)
        {
            return key.StartsWith(DistributedCacheExtension.InstanceName, StringComparison.Ordinal)
                ? key[DistributedCacheExtension.InstanceName.Length..]
                : key;
        }
    }
}
