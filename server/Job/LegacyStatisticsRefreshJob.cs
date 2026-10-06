using Quartz;
using Sentry;
using server.Services.Legacy;

namespace server.Job
{
    public class LegacyStatisticsRefreshJob(StatisticsService statisticsService) : IJob
    {
        public async ValueTask Execute(IJobExecutionContext _, CancellationToken _cancellationToken = default)
        {
            SentryId id = SentrySdk.CaptureCheckIn("LegacyStatisticsRefreshJob", CheckInStatus.InProgress);
            try
            {
                await statisticsService.RunAsync().ConfigureAwait(false);
                SentrySdk.CaptureCheckIn("LegacyStatisticsRefreshJob", CheckInStatus.Ok, id);
            }
            catch
            {
                SentrySdk.CaptureCheckIn("LegacyStatisticsRefreshJob", CheckInStatus.Error, id);
                throw;
            }
        }
    }
}