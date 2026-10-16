using server.Core;
using server.Extension;
using server.Models.Entity.SpiralAbyss;
using System.Runtime.InteropServices;

namespace server.Services.Legacy
{
    // Transient
    public sealed class StatisticsService(IServiceProvider serviceProvider)
    {
        /// <summary>
        /// 统计服务正在工作
        /// </summary>
        public const string Working = "StatisticsService.Working";
        private const int Partion = 512;

        // Compile queries that used multiple times to increase performance
        private static readonly Func<AppDbContext, long, IEnumerable<EntityRecord>> PartionQuery = EF.CompileQuery((AppDbContext context, long lastId) =>
            context.Records.AsNoTracking().OrderBy(r => r.PrimaryId).Where(r => r.PrimaryId > lastId).Take(Partion));

        private static readonly Func<AppDbContext, long, IEnumerable<EntityAvatar>> AvatarQuery = EF.CompileQuery((AppDbContext context, long recordId) =>
            context.Avatars.AsNoTracking().Where(a => a.RecordId == recordId));

        private static readonly Func<AppDbContext, long, EntitySpiralAbyss?> SpiralAbyssQuery = EF.CompileQuery((AppDbContext context, long recordId) =>
            context.SpiralAbysses.AsNoTracking().SingleOrDefault(s => s.RecordId == recordId));

        private static readonly Func<AppDbContext, long, IEnumerable<EntityFloor>> FloorQuery = EF.CompileQuery((AppDbContext context, long spiralAbyssId) =>
            context.SpiralAbyssFloors.AsNoTracking().Where(f => f.SpiralAbyssId == spiralAbyssId));

        private readonly SpiralAbyssStatisticsService spiralAbyssStatisticsService = serviceProvider.GetRequiredService<SpiralAbyssStatisticsService>();
        private readonly ILogger<StatisticsService> logger = serviceProvider.GetRequiredService<ILogger<StatisticsService>>();
        private readonly AppDbContext appDbContext = serviceProvider.GetRequiredService<AppDbContext>();
        private readonly IDistributedCache cache = serviceProvider.GetRequiredService<IDistributedCache>();

        public async Task RunAsync()
        {
            if (cache.HasFlag(Working))
            {
                return;
            }

            try
            {
                cache.SetFlag(Working, DistributedCacheExtension.WorkingExpiration);

                StatisticsTracker tracker = new();

                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                await Task.Run(() => RunCore(tracker)).ConfigureAwait(false);
                _ = tracker.CompleteTracking(spiralAbyssStatisticsService, stopwatch);
            }
            finally
            {
                cache.Remove(Working);
            }
        }

        private void RunCore(StatisticsTracker tracker)
        {
            int count = 0;
            while (true)
            {
                logger.LogInformation("StatisticsService: Processing {Count} records", count += Partion);

                List<EntityRecord> partialRecords = [.. PartionQuery(appDbContext, tracker.LastId)];
                foreach (ref EntityRecord record in CollectionsMarshal.AsSpan(partialRecords))
                {
                    record.Avatars = [.. AvatarQuery(appDbContext, record.PrimaryId)];
                    record.SpiralAbyss = SpiralAbyssQuery(appDbContext, record.PrimaryId);

                    record.SpiralAbyss?.Floors = [.. FloorQuery(appDbContext, record.SpiralAbyss.PrimaryId)];

                    tracker.Track(record);
                }

                if (partialRecords.Count < Partion)
                {
                    break;
                }
            }
        }
    }
}