using Quartz;

namespace server.Job;

public sealed class RoleCombatRecordCleanJob(
    AppDbContext appDbContext,
    IDistributedCache cache) : IJob
{
    /// <inheritdoc/>
    public async ValueTask Execute(IJobExecutionContext _, CancellationToken _cancellationToken = default)
    {
        SentryId id = SentrySdk.CaptureCheckIn("RoleCombatRecordCleanJob", CheckInStatus.InProgress);
        try
        {
            // 清理期间与幻牌统计刷新互斥.曾经粘成 GachaLogStatisticsService.Working（复制粘贴 bug）.
            await cache.RunExclusiveAsync(RoleCombatService.Working, async () =>
            {
                await appDbContext.RoleCombatAvatars.ExecuteDeleteAsync(_cancellationToken).ConfigureAwait(false);

                await appDbContext.RoleCombatRecords.ExecuteDeleteAsync(_cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);

            SentrySdk.CaptureCheckIn("RoleCombatRecordCleanJob", CheckInStatus.Ok, id);
        }
        catch
        {
            SentrySdk.CaptureCheckIn("RoleCombatRecordCleanJob", CheckInStatus.Error, id);
            throw;
        }
    }
}
