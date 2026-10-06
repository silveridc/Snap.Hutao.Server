using Quartz;
using Sentry;
using server.Services.GachaLog.Statistics;

namespace server.Job
{
    public sealed class GachaLogStatisticsRefreshJob(GachaLogStatisticsService statisticsService) : IJob
    {
        public async ValueTask Execute(IJobExecutionContext _, CancellationToken _cancellationToken = default)
        {
            // cron受检
            SentryId id = SentrySdk.CaptureCheckIn("GachaLogStatisticsRefreshJob", CheckInStatus.InProgress);
            try
            {
                await statisticsService.RunAsync().ConfigureAwait(false);
                SentrySdk.CaptureCheckIn("GachaLogStatisticsRefreshJob", CheckInStatus.Ok, id);
            }
            catch (Exception)
            {
                SentrySdk.CaptureCheckIn("GachaLogStatisticsRefreshJob", CheckInStatus.Error, id);
                throw;
            }
        }
    }
}