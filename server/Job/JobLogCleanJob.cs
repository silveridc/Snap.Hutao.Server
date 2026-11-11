using Quartz;
using server.Services.Admin;

namespace server.Job;

public class JobLogCleanJob(AppDbContext appDbContext, SystemConfigService systemConfigService, TimeProvider timeProvider) : IJob
{
    /// <inheritdoc/>
    public async ValueTask Execute(IJobExecutionContext _, CancellationToken _cancellationToken = default)
    {
        SentryId id = SentrySdk.CaptureCheckIn("JobLogCleanJob", CheckInStatus.InProgress);
        try
        {
            DateTimeOffset lastAllowed = timeProvider.GetLocalNow() - TimeSpan.FromDays(systemConfigService.JobLogRetentionDays);
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
