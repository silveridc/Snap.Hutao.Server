using Quartz;

namespace server.Job;

public class LegacyStatisticsRefreshJob(StatisticsService statisticsService) : IJob
{
    /// <summary>
    /// Runs the legacy Spiral Abyss statistics refresh and reports the Quartz check-in status to Sentry.
    /// </summary>
    /// <param name="context">The Quartz execution context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous execution.</returns>
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
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
