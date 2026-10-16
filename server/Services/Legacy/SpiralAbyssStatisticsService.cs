using server.Extension;
using server.Models.Entity.SpiralAbyss;

namespace server.Services.Legacy
{
    // Scoped
    public sealed class SpiralAbyssStatisticsService(AppDbContext appDbContext, IDistributedCache cache)
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
        };

        public T? GetStatistics<T>(string name)
            where T : class
        {
            return GetStatistics<T>(name, SpiralAbyssScheduleId.GetForNow());
        }

        public T? GetStatistics<T>(string name, int scheduleId)
            where T : class
        {
            string key = $"{name}:{scheduleId}";
            if (cache.TryGetValue(key, out T? data))
            {
                return data;
            }

            LegacyStatistics? statistics = appDbContext.Statistics
                .Where(s => s.ScheduleId == scheduleId)
                .SingleOrDefault(s => s.Name == name);

            if (statistics is null)
            {
                return null;
            }

            T? typedData = JsonSerializer.Deserialize<T>(statistics.Data, JsonOptions);
            cache.Set(key, typedData, TimeSpan.FromDays(1));

            return typedData;
        }

        public void SaveStatistics<T>(int scheduleId, string name, T data)
        {
            LegacyStatistics? statistics = appDbContext.Statistics
                .Where(s => s.ScheduleId == scheduleId)
                .SingleOrDefault(s => s.Name == name);

            if (statistics == null)
            {
                statistics = LegacyStatistics.CreateWithNameAndScheduleId(name, scheduleId);
                appDbContext.Statistics.Add(statistics);
            }

            cache.Set($"{name}:{scheduleId}", data, TimeSpan.FromDays(1));
            statistics.Data = JsonSerializer.Serialize(data, JsonOptions);

            appDbContext.SaveChanges();
        }
    }
}