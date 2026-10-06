using Quartz;
using Sentry;
using server.Extension;

namespace server.Job
{
    public class SpiralAbyssRecordCleanJob(IServiceProvider serviceProvider) : IJob
    {
        private static readonly TimeSpan LongestDaysAllowed = new(30, 0, 0, 0);

        private readonly AppDbContext appDbContext = serviceProvider.GetRequiredService<AppDbContext>();
        private readonly IRankService rankService = serviceProvider.GetRequiredService<IRankService>();
        private readonly IDistributedCache cache = serviceProvider.GetRequiredService<IDistributedCache>();

        /// <inheritdoc/>
        public async ValueTask Execute(IJobExecutionContext _, CancellationToken _cancellationToken = default)
        {
            SentryId id = SentrySdk.CaptureCheckIn("SpiralAbyssRecordCleanJob", CheckInStatus.InProgress);
            try
            {
                try
                {
                    cache.SetFlag(GachaLogStatisticsService.Working, DistributedCacheExtension.WorkingExpiration);
                    DateTimeOffset lastAllowed = DateTimeOffset.Now - LongestDaysAllowed;
                    long lastAllowedTimestamp = lastAllowed.ToUnixTimeSeconds();

                    List<long> recordIds = await appDbContext.Records
                        .Where(r => r.UploadTime < lastAllowedTimestamp)
                        .Select(r => r.PrimaryId)
                        .ToListAsync(_cancellationToken)
                        .ConfigureAwait(false);

                    await appDbContext.Avatars
                        .Where(a => recordIds.Contains(a.RecordId))
                        .ExecuteDeleteAsync(_cancellationToken)
                        .ConfigureAwait(false);

                    List<long> spiralAbyssIds = await appDbContext.SpiralAbysses
                        .Select(s => s.PrimaryId)
                        .ToListAsync(_cancellationToken)
                        .ConfigureAwait(false);

                    await appDbContext.DamageRanks
                        .Where(d => spiralAbyssIds.Contains(d.SpiralAbyssId))
                        .ExecuteDeleteAsync(_cancellationToken)
                        .ConfigureAwait(false);

                    await appDbContext.TakeDamageRanks
                        .Where(t => spiralAbyssIds.Contains(t.SpiralAbyssId))
                        .ExecuteDeleteAsync(_cancellationToken)
                        .ConfigureAwait(false);

                    await appDbContext.SpiralAbyssFloors
                        .Where(s => spiralAbyssIds.Contains(s.SpiralAbyssId))
                        .ExecuteDeleteAsync(_cancellationToken)
                        .ConfigureAwait(false);

                    await appDbContext.SpiralAbysses.ExecuteDeleteAsync(_cancellationToken).ConfigureAwait(false);
                    await appDbContext.Records.Where(r => r.UploadTime < lastAllowedTimestamp).ExecuteDeleteAsync(_cancellationToken).ConfigureAwait(false);

                    await rankService.ClearRanksAsync().ConfigureAwait(false);
                }
                finally
                {
                    cache.Remove(GachaLogStatisticsService.Working);
                }

                SentrySdk.CaptureCheckIn("SpiralAbyssRecordCleanJob", CheckInStatus.Ok, id);
            }
            catch
            {
                SentrySdk.CaptureCheckIn("SpiralAbyssRecordCleanJob", CheckInStatus.Error, id);
                throw;
            }
        }
    }
}