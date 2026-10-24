using Quartz;
using server.Extension;

namespace server.Job;

public sealed class RoleCombatRecordCleanJob(IServiceProvider serviceProvider) : IJob
{
    private readonly AppDbContext _appDbContext = serviceProvider.GetRequiredService<AppDbContext>();
    private readonly IDistributedCache _cache = serviceProvider.GetRequiredService<IDistributedCache>();

    /// <inheritdoc/>
    public async ValueTask Execute(IJobExecutionContext _, CancellationToken _cancellationToken = default)
    {
        SentryId id = SentrySdk.CaptureCheckIn("RoleCombatRecordCleanJob", CheckInStatus.InProgress);
        try
        {
            // 清理期间与幻牌统计刷新互斥。曾经粘成 GachaLogStatisticsService.Working（复制粘贴 bug）。
            await _cache.RunExclusiveAsync(RoleCombatService.Working, async () =>
            {
                await _appDbContext.RoleCombatAvatars.ExecuteDeleteAsync(_cancellationToken).ConfigureAwait(false);

                await _appDbContext.RoleCombatRecords.ExecuteDeleteAsync(_cancellationToken).ConfigureAwait(false);
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