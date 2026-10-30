using Quartz;

namespace server.Job;

public class SpiralAbyssRecordCleanJob(IServiceProvider serviceProvider, TimeProvider timeProvider) : IJob
{
    private static readonly TimeSpan LongestDaysAllowed = new(30, 0, 0, 0);

    private readonly AppDbContext _appDbContext = serviceProvider.GetRequiredService<AppDbContext>();
    private readonly IRankService _rankService = serviceProvider.GetRequiredService<IRankService>();
    private readonly IDistributedCache _cache = serviceProvider.GetRequiredService<IDistributedCache>();

    /// <inheritdoc/>
    public async ValueTask Execute(IJobExecutionContext _, CancellationToken _cancellationToken = default)
    {
        SentryId id = SentrySdk.CaptureCheckIn("SpiralAbyssRecordCleanJob", CheckInStatus.InProgress);
        try
        {
            // 清理期间与深渊统计刷新（StatisticsService.Working）、RecordController 上传门互斥。
            // 曾经粘成 GachaLogStatisticsService.Working：清理既不与深渊统计互斥，又误挡误清抽卡统计的锁。
            await _cache.RunExclusiveAsync(StatisticsService.Working, async () =>
            {
                DateTimeOffset lastAllowed = timeProvider.GetLocalNow() - LongestDaysAllowed;
                long lastAllowedTimestamp = lastAllowed.ToUnixTimeSeconds();

                List<long> recordIds = await _appDbContext.Records
                    .Where(r => r.UploadTime < lastAllowedTimestamp)
                    .Select(r => r.PrimaryId)
                    .ToListAsync(_cancellationToken)
                    .ConfigureAwait(false);

                await _appDbContext.Avatars
                    .Where(a => recordIds.Contains(a.RecordId))
                    .ExecuteDeleteAsync(_cancellationToken)
                    .ConfigureAwait(false);

                List<long> spiralAbyssIds = await _appDbContext.SpiralAbysses
                    .Select(s => s.PrimaryId)
                    .ToListAsync(_cancellationToken)
                    .ConfigureAwait(false);

                await _appDbContext.DamageRanks
                    .Where(d => spiralAbyssIds.Contains(d.SpiralAbyssId))
                    .ExecuteDeleteAsync(_cancellationToken)
                    .ConfigureAwait(false);

                await _appDbContext.TakeDamageRanks
                    .Where(t => spiralAbyssIds.Contains(t.SpiralAbyssId))
                    .ExecuteDeleteAsync(_cancellationToken)
                    .ConfigureAwait(false);

                await _appDbContext.SpiralAbyssFloors
                    .Where(s => spiralAbyssIds.Contains(s.SpiralAbyssId))
                    .ExecuteDeleteAsync(_cancellationToken)
                    .ConfigureAwait(false);

                await _appDbContext.SpiralAbysses.ExecuteDeleteAsync(_cancellationToken).ConfigureAwait(false);
                await _appDbContext.Records.Where(r => r.UploadTime < lastAllowedTimestamp).ExecuteDeleteAsync(_cancellationToken).ConfigureAwait(false);

                await _rankService.ClearRanksAsync().ConfigureAwait(false);
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
