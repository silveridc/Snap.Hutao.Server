using Quartz;

namespace server.Job;

public sealed class GachaLogStatisticsRefreshJob(GachaLogStatisticsService statisticsService) : IJob
{
    /// <summary>
    /// Runs the gacha log statistics refresh and reports the Quartz check-in status to Sentry.
    /// </summary>
    /// <param name="context">The Quartz execution context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous execution.</returns>
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
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
