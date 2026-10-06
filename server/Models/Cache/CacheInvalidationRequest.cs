namespace server.Models.Cache
{
    public sealed class CacheInvalidationRequest
    {
        /// <summary>
        /// 需要精确移除的逻辑缓存键。
        /// </summary>
        public List<string> Keys { get; set; } = [];

        /// <summary>
        /// 需要按 glob 模式批量移除的逻辑缓存键模式（例如 <c>RoleCombatStatistics:*</c>、<c>User:IsMaintainer:*</c> 或 <c>*</c>）。
        /// </summary>
        public List<string> Patterns { get; set; } = [];
    }

    public sealed class CacheInvalidationResult
    {
        /// <summary>
        /// 已按精确键发出移除请求的数量。
        /// </summary>
        public int RemovedKeys { get; set; }

        /// <summary>
        /// 按模式实际删除的键数量。
        /// </summary>
        public long RemovedByPattern { get; set; }
    }
}
