using System.Runtime.InteropServices;
using server.Core;
using server.Extension;
using server.Models.Entity.SpiralAbyss;

namespace server.Services.Legacy;

// Transient
public sealed class StatisticsService(IServiceProvider serviceProvider)
{
    /// <summary>
    /// 统计服务正在工作
    /// </summary>
    public const string Working = "StatisticsService.Working";
    private const int _partion = 512;

    // Compile queries that used multiple times to increase performance
    private static readonly Func<AppDbContext, long, IEnumerable<EntityRecord>> PartionQuery = EF.CompileQuery((AppDbContext context, long lastId) =>
        context.Records.AsNoTracking().OrderBy(r => r.PrimaryId).Where(r => r.PrimaryId > lastId).Take(_partion));

    private static readonly Func<AppDbContext, long, IEnumerable<EntityAvatar>> AvatarQuery = EF.CompileQuery((AppDbContext context, long recordId) =>
        context.Avatars.AsNoTracking().Where(a => a.RecordId == recordId));

    private static readonly Func<AppDbContext, long, EntitySpiralAbyss?> SpiralAbyssQuery = EF.CompileQuery((AppDbContext context, long recordId) =>
        context.SpiralAbysses.AsNoTracking().SingleOrDefault(s => s.RecordId == recordId));

    private static readonly Func<AppDbContext, long, IEnumerable<EntityFloor>> FloorQuery = EF.CompileQuery((AppDbContext context, long spiralAbyssId) =>
        context.SpiralAbyssFloors.AsNoTracking().Where(f => f.SpiralAbyssId == spiralAbyssId));

    private readonly SpiralAbyssStatisticsService _spiralAbyssStatisticsService = serviceProvider.GetRequiredService<SpiralAbyssStatisticsService>();
    private readonly ILogger<StatisticsService> _logger = serviceProvider.GetRequiredService<ILogger<StatisticsService>>();
    private readonly AppDbContext _appDbContext = serviceProvider.GetRequiredService<AppDbContext>();
    private readonly IDistributedCache _cache = serviceProvider.GetRequiredService<IDistributedCache>();

    public async Task RunAsync()
    {
        await _cache.RunExclusiveAsync(Working, async () =>
        {
            StatisticsTracker tracker = new();

            ValueStopwatch stopwatch = ValueStopwatch.StartNew();
            await Task.Run(() => RunCore(tracker)).ConfigureAwait(false);
            _ = tracker.CompleteTracking(_spiralAbyssStatisticsService, stopwatch);
        }).ConfigureAwait(false);
    }

    private void RunCore(StatisticsTracker tracker)
    {
        int count = 0;
        while (true)
        {
            _logger.LogInformation("StatisticsService: Processing {Count} records", count += _partion);

            List<EntityRecord> partialRecords = [.. PartionQuery(_appDbContext, tracker.LastId)];
            foreach (ref EntityRecord record in CollectionsMarshal.AsSpan(partialRecords))
            {
                record.Avatars = [.. AvatarQuery(_appDbContext, record.PrimaryId)];
                record.SpiralAbyss = SpiralAbyssQuery(_appDbContext, record.PrimaryId);

                record.SpiralAbyss?.Floors = [.. FloorQuery(_appDbContext, record.SpiralAbyss.PrimaryId)];

                tracker.Track(record);
            }

            if (partialRecords.Count < _partion)
            {
                break;
            }
        }
    }
}
