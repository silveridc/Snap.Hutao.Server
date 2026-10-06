namespace server.Extension
{
    /// <summary>
    /// <see cref="IDistributedCache"/> 的类型化扩展，使用 JSON 序列化承载对象。
    /// </summary>
    public static class DistributedCacheExtension
    {
        /// <summary>
        /// 分布式缓存键前缀（<see cref="Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions.InstanceName"/>）。
        /// </summary>
        public const string InstanceName = "SnapHutaoServer:";

        /// <summary>
        /// 工作标志的兜底过期时间：进程崩溃未执行 finally 时，避免分布式锁永久残留。
        /// </summary>
        public static readonly TimeSpan WorkingExpiration = TimeSpan.FromHours(6);

        private static readonly byte[] FlagPayload = [1];

        private static readonly JsonSerializerOptions DefaultOptions = new()
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        };

        public static bool TryGetValue<T>(this IDistributedCache cache, string key, out T? value)
        {
            byte[]? bytes = cache.Get(key);
            if (bytes is null)
            {
                value = default;
                return false;
            }

            value = JsonSerializer.Deserialize<T>(bytes, DefaultOptions);
            return true;
        }

        public static void Set<T>(this IDistributedCache cache, string key, T value, TimeSpan? absoluteExpirationRelativeToNow = null)
        {
            DistributedCacheEntryOptions options = new();
            if (absoluteExpirationRelativeToNow is { } ttl)
            {
                options.AbsoluteExpirationRelativeToNow = ttl;
            }

            cache.Set(key, JsonSerializer.SerializeToUtf8Bytes(value, DefaultOptions), options);
        }

        public static void Set<T>(this IDistributedCache cache, string key, T value, DateTimeOffset absoluteExpiration)
        {
            cache.Set(key, JsonSerializer.SerializeToUtf8Bytes(value, DefaultOptions), new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = absoluteExpiration,
            });
        }

        public static async Task<T?> GetOrCreateAsync<T>(this IDistributedCache cache, string key, TimeSpan absoluteExpirationRelativeToNow, Func<Task<T>> factory)
        {
            byte[]? bytes = await cache.GetAsync(key).ConfigureAwait(false);
            if (bytes is not null)
            {
                return JsonSerializer.Deserialize<T>(bytes, DefaultOptions);
            }

            T value = await factory().ConfigureAwait(false);
            await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value, DefaultOptions), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow,
            }).ConfigureAwait(false);

            return value;
        }

        /// <summary>
        /// 设置一个仅用于存在性判断的标志键（带过期时间）。
        /// </summary>
        public static void SetFlag(this IDistributedCache cache, string key, TimeSpan expiration)
        {
            cache.Set(key, FlagPayload, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration,
            });
        }

        /// <summary>
        /// 判断标志键是否存在。
        /// </summary>
        public static bool HasFlag(this IDistributedCache cache, string key)
        {
            return cache.Get(key) is not null;
        }
    }
}
