using Quartz;
using server.Services.Admin;

namespace server.Job;

public class SpiralAbyssRecordCleanJob(
    AppDbContext appDbContext,
    IRankService rankService,
    IDistributedCache cache,
    SystemConfigService systemConfigService,
    TimeProvider timeProvider) : IJob
{
    /// <inheritdoc/>
    public async ValueTask Execute(IJobExecutionContext _, CancellationToken _cancellationToken = default)
    {
        SentryId id = SentrySdk.CaptureCheckIn("SpiralAbyssRecordCleanJob", CheckInStatus.InProgress);
        try
        {
            // 清理期间与深渊统计刷新（StatisticsService.Working）、RecordController 上传门互斥.
            // 曾经粘成 GachaLogStatisticsService.Working：清理既不与深渊统计互斥，又误挡误清抽卡统计的锁.
            await cache.RunExclusiveAsync(StatisticsService.Working, async () =>
            {
                DateTimeOffset lastAllowed = timeProvider.GetLocalNow() - TimeSpan.FromDays(systemConfigService.SpiralAbyssRecordRetentionDays);
                long lastAllowedTimestamp = lastAllowed.ToUnixTimeSeconds();

                List<long> recordIds = await appDbContext.Records
                    .Where(r => r.UploadTime < lastAllowedTimestamp)
                    .Select(r => r.PrimaryId)
                    .ToListAsync(_cancellationToken)
                    .ConfigureAwait(false);

                await appDbContext.Avatars
                    .Where(a => recordIds.Contains(a.RecordId))
                    .ExecuteDeleteAsync(_cancellationToken)
                    .ConfigureAwait(false);

                List<long> spiralAbyssIds = await appDbContext.SpiralAbysses
                    .Select(s => s.PrimaryId)
                    .ToListAsync(_cancellationToken)
                    .ConfigureAwait(false);

                await appDbContext.DamageRanks
                    .Where(d => spiralAbyssIds.Contains(d.SpiralAbyssId))
                    .ExecuteDeleteAsync(_cancellationToken)
                    .ConfigureAwait(false);

                await appDbContext.TakeDamageRanks
                    .Where(t => spiralAbyssIds.Contains(t.SpiralAbyssId))
                    .ExecuteDeleteAsync(_cancellationToken)
                    .ConfigureAwait(false);

                await appDbContext.SpiralAbyssFloors
                    .Where(s => spiralAbyssIds.Contains(s.SpiralAbyssId))
                    .ExecuteDeleteAsync(_cancellationToken)
                    .ConfigureAwait(false);

                await appDbContext.SpiralAbysses.ExecuteDeleteAsync(_cancellationToken).ConfigureAwait(false);
                await appDbContext.Records.Where(r => r.UploadTime < lastAllowedTimestamp).ExecuteDeleteAsync(_cancellationToken).ConfigureAwait(false);

                await rankService.ClearRanksAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            SentrySdk.CaptureCheckIn("SpiralAbyssRecordCleanJob", CheckInStatus.Ok, id);
        }
        catch
        {
            SentrySdk.CaptureCheckIn("SpiralAbyssRecordCleanJob", CheckInStatus.Error, id);
            throw;
        }
    }
}
