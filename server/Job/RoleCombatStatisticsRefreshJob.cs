using Quartz;

namespace server.Job;

public sealed class RoleCombatStatisticsRefreshJob(RoleCombatService statisticsService) : IJob
{
    /// <summary>
    /// Runs the Imaginarium Theater statistics refresh and reports the Quartz check-in status to Sentry.
    /// </summary>
    /// <param name="context">The Quartz execution context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous execution.</returns>
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
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
