using Quartz;
using Sentry;
using server.Extension;
using server.Models.Context;
using server.Services.GachaLog.Statistics;

namespace server.Job
{
    public sealed class RoleCombatRecordCleanJob(IServiceProvider serviceProvider) : IJob
    {
        private readonly AppDbContext appDbContext = serviceProvider.GetRequiredService<AppDbContext>();
        private readonly IDistributedCache cache = serviceProvider.GetRequiredService<IDistributedCache>();

        /// <inheritdoc/>
        public async ValueTask Execute(IJobExecutionContext _, CancellationToken _cancellationToken = default)
        {
            SentryId id = SentrySdk.CaptureCheckIn("RoleCombatRecordCleanJob", CheckInStatus.InProgress);
            try
            {
                try
                {
                    cache.SetFlag(GachaLogStatisticsService.Working, DistributedCacheExtension.WorkingExpiration);

                    await appDbContext.RoleCombatAvatars.ExecuteDeleteAsync(_cancellationToken).ConfigureAwait(false);

                    await appDbContext.RoleCombatRecords.ExecuteDeleteAsync(_cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    cache.Remove(GachaLogStatisticsService.Working);
                }

                SentrySdk.CaptureCheckIn("RoleCombatRecordCleanJob", CheckInStatus.Ok, id);
            }
            catch
            {
                SentrySdk.CaptureCheckIn("RoleCombatRecordCleanJob", CheckInStatus.Error, id);
                throw;
            }
        }
    }
}