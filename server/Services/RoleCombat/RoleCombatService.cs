using System.Runtime.InteropServices;
using server.Extension;
using server.Models.Entity.RoleCombat;
using server.Models.RoleCombat;
using server.Services.Legacy.Primitive;

namespace server.Services.RoleCombat;

public sealed class RoleCombatService(IServiceProvider serviceProvider, TimeProvider timeProvider)
{
    public const string Working = "RoleCombatService.Working";

    private static readonly Func<AppDbContext, IEnumerable<RoleCombatRecord>> RecordsQuery = EF.CompileQuery((AppDbContext context) =>
        context.RoleCombatRecords.AsNoTracking());

    private static readonly Func<AppDbContext, long, IEnumerable<RoleCombatAvatar>> AvatarsQuery = EF.CompileQuery((AppDbContext context, long recordId) =>
        context.RoleCombatAvatars.AsNoTracking().Where(g => g.RecordId == recordId).AsQueryable());

    private readonly AppDbContext _appDbContext = serviceProvider.GetRequiredService<AppDbContext>();
    private readonly IDistributedCache _cache = serviceProvider.GetRequiredService<IDistributedCache>();

    public async Task RunAsync()
    {
        await _cache.RunExclusiveAsync(Working, async () =>
        {
            Map<uint, int> resultMap = [];

            int total = await Task.Run(() => RunCore(resultMap)).ConfigureAwait(false);
            RoleCombatStatisticsItem item = new()
            {
                ScheduleId = RoleCombatScheduleId.GetForNow(),
                RecordTotal = total,
                Timestamp = timeProvider.GetUnixTimeMilliseconds(),
                BackupAvatarRates = [.. resultMap.Select(kvp => new Models.Legacy.ItemRate<uint, double>(kvp.Key, kvp.Value / (double)total))],
            };

            int scheduleId = RoleCombatScheduleId.GetForNow();
            RoleCombatStatistics? statistics = _appDbContext.RoleCombatStatistics.SingleOrDefault(s => s.ScheduleId == scheduleId);

            if (statistics == null)
            {
                statistics = new()
                {
                    ScheduleId = scheduleId,
                };
                _appDbContext.RoleCombatStatistics.Add(statistics);
            }

            _cache.Set($"RoleCombatStatistics:{scheduleId}", item);
            statistics.Data = JsonSerializer.Serialize(item);

            await _appDbContext.SaveChangesAsync().ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private int RunCore(Map<uint, int> resultMap)
    {
        int count = 0;
        List<RoleCombatRecord> reocrds = [.. RecordsQuery(_appDbContext)];
        foreach (ref readonly RoleCombatRecord record in CollectionsMarshal.AsSpan(reocrds))
        {
            count++;
            List<RoleCombatAvatar> avatars = [.. AvatarsQuery(_appDbContext, record.PrimaryId)];

            foreach (ref readonly RoleCombatAvatar avatar in CollectionsMarshal.AsSpan(avatars))
            {
                resultMap.IncreaseOne(avatar.AvatarId);
            }
        }

        return count;
    }
}
