using Quartz;

namespace server.Job;

public class JobLogCleanJob(AppDbContext appDbContext, TimeProvider timeProvider) : IJob
{
    private static readonly TimeSpan RetentionDays = new(90, 0, 0, 0);

    /// <inheritdoc/>
    public async ValueTask Execute(IJobExecutionContext _, CancellationToken _cancellationToken = default)
    {
        SentryId id = SentrySdk.CaptureCheckIn("JobLogCleanJob", CheckInStatus.InProgress);
        try
        {
            DateTimeOffset lastAllowed = timeProvider.GetLocalNow() - RetentionDays;
            await appDbContext.JobLogs
                .Where(l => l.StartedAt < lastAllowed)
                .ExecuteDeleteAsync(_cancellationToken)
                .ConfigureAwait(false);

            SentrySdk.CaptureCheckIn("JobLogCleanJob", CheckInStatus.Ok, id);
        }
        catch
        {
            SentrySdk.CaptureCheckIn("JobLogCleanJob", CheckInStatus.Error, id);
            throw;
        }
    }
}
