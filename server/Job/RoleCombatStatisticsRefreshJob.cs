using Quartz;
using Sentry;
using server.Services.RoleCombat;

namespace server.Job
{
    public sealed class RoleCombatStatisticsRefreshJob(RoleCombatService statisticsService) : IJob
    {
        public async ValueTask Execute(IJobExecutionContext _, CancellationToken _cancellationToken = default)
        {
            SentryId id = SentrySdk.CaptureCheckIn("RoleCombatStatisticsRefreshJob", CheckInStatus.InProgress);
            try
            {
                await statisticsService.RunAsync().ConfigureAwait(false);
                SentrySdk.CaptureCheckIn("RoleCombatStatisticsRefreshJob", CheckInStatus.Ok, id);
            }
            catch
            {
                SentrySdk.CaptureCheckIn("RoleCombatStatisticsRefreshJob", CheckInStatus.Error, id);
                throw;
            }
        }
    }
}