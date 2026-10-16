using server.Extension;
using server.Models.Entity.RoleCombat;
using server.Models.RoleCombat;
using server.Services.Legacy.Primitive;
using System.Runtime.InteropServices;

namespace server.Services.RoleCombat
{
    public sealed class RoleCombatService(IServiceProvider serviceProvider)
    {
        public const string Working = "RoleCombatService.Working";

        private static readonly Func<AppDbContext, IEnumerable<RoleCombatRecord>> RecordsQuery = EF.CompileQuery((AppDbContext context) =>
            context.RoleCombatRecords.AsNoTracking());

        private static readonly Func<AppDbContext, long, IEnumerable<RoleCombatAvatar>> AvatarsQuery = EF.CompileQuery((AppDbContext context, long recordId) =>
            context.RoleCombatAvatars.AsNoTracking().Where(g => g.RecordId == recordId).AsQueryable());

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
                Map<uint, int> resultMap = [];

                int total = await Task.Run(() => RunCore(resultMap)).ConfigureAwait(false);
                RoleCombatStatisticsItem item = new()
                {
                    ScheduleId = RoleCombatScheduleId.GetForNow(),
                    RecordTotal = total,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    BackupAvatarRates = [.. resultMap.Select(kvp => new Models.Legacy.ItemRate<uint, double>(kvp.Key, kvp.Value / (double)total))],
                };

                int scheduleId = RoleCombatScheduleId.GetForNow();
                RoleCombatStatistics? statistics = appDbContext.RoleCombatStatistics.SingleOrDefault(s => s.ScheduleId == scheduleId);

                if (statistics == null)
                {
                    statistics = new()
                    {
                        ScheduleId = scheduleId,
                    };
                    appDbContext.RoleCombatStatistics.Add(statistics);
                }

                cache.Set($"RoleCombatStatistics:{scheduleId}", item);
                statistics.Data = JsonSerializer.Serialize(item);

                await appDbContext.SaveChangesAsync().ConfigureAwait(false);
            }
            finally
            {
                cache.Remove(Working);
            }
        }

        private int RunCore(Map<uint, int> resultMap)
        {
            int count = 0;
            List<RoleCombatRecord> reocrds = [.. RecordsQuery(appDbContext)];
            foreach (ref readonly RoleCombatRecord record in CollectionsMarshal.AsSpan(reocrds))
            {
                count++;
                List<RoleCombatAvatar> avatars = [.. AvatarsQuery(appDbContext, record.PrimaryId)];

                foreach (ref readonly RoleCombatAvatar avatar in CollectionsMarshal.AsSpan(avatars))
                {
                    resultMap.IncreaseOne(avatar.AvatarId);
                }
            }

            return count;
        }
    }
}