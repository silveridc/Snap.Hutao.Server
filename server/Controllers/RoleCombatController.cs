using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore.Storage;
using server.Extension;
using server.Models.Entity.RoleCombat;
using server.Models.RoleCombat;

namespace server.Controllers;

[ApiController]
[Route("api/[controller]")]
[ApiExplorerSettings(GroupName = "RoleCombat")]
public class RoleCombatController(IServiceProvider serviceProvider, TimeProvider timeProvider)
{
    private static readonly ConcurrentDictionary<string, UploadToken> UploadingUids = new();

    private readonly AppDbContext _appDbContext = serviceProvider.GetRequiredService<AppDbContext>();
    private readonly IDistributedCache _cache = serviceProvider.GetRequiredService<IDistributedCache>();

    /// <summary>
    /// 上传剧演记录。
    /// </summary>
    /// <param name="record">剧演记录数据。</param>
    /// <returns>上传结果。</returns>
    [HttpPost("Upload")]
    public async Task<IActionResult> Upload([FromBody] SimpleRoleCombatRecord record)
    {
        if (_cache.HasFlag(RoleCombatService.Working))
        {
            return Fail("Computing statistics", ReturnCode.ComputingStatistics, ServerKeys.ServerRoleCombatComputingStatistics);
        }

        if (record.ScheduleId != RoleCombatScheduleId.GetForNow())
        {
            return Fail("Not current role combat schedule data", ReturnCode.NotCurrentSchedule, ServerKeys.ServerRoleCombatNotCurrentSchedule);
        }

        if (!record.Validate())
        {
            return Fail("Invalid upload data", ReturnCode.InvalidUploadData, ServerKeys.ServerRoleCombatInvalidData);
        }

        if (UploadingUids.TryGetValue(record.Uid, out _) || !UploadingUids.TryAdd(record.Uid, default))
        {
            return Fail("A previous request for this Uid is still being processed", ReturnCode.PreviousRequestNotCompleted, ServerKeys.ServerRoleCombatPreviousRequestNotCompleted);
        }

        await using (IDbContextTransaction transaction = await _appDbContext.Database.BeginTransactionAsync().ConfigureAwait(false))
        {
            RoleCombatRecord? exist = await _appDbContext.RoleCombatRecords.SingleOrDefaultAsync(r => r.Uid == record.Uid).ConfigureAwait(false);

            if (exist is not null)
            {
                await _appDbContext.RoleCombatAvatars.Where(a => a.RecordId == exist.PrimaryId).ExecuteDeleteAsync().ConfigureAwait(false);
            }

            await _appDbContext.RoleCombatRecords.Where(r => r.Uid == record.Uid).ExecuteDeleteAsync().ConfigureAwait(false);

            RoleCombatRecord newRecord = new()
            {
                Uid = record.Uid,
                Uploader = record.Identity,
                UploadTime = timeProvider.GetUnixTimeSeconds(),
            };
            await _appDbContext.RoleCombatRecords.AddAndSaveAsync(newRecord).ConfigureAwait(false);

            long recordId = newRecord.PrimaryId;
            List<RoleCombatAvatar> avatars = [.. record.BackupAvatars.Select(id => new RoleCombatAvatar() { AvatarId = id, RecordId = recordId })];
            await _appDbContext.RoleCombatAvatars.AddRangeAndSaveAsync(avatars).ConfigureAwait(false);

            await transaction.CommitAsync().ConfigureAwait(false);
        }

        if (!UploadingUids.TryRemove(record.Uid, out _))
        {
            return Fail("Submission state error", ReturnCode.InternalStateException, ServerKeys.ServerRoleCombatInternalException);
        }

        return Response.Success("Data uploaded successfully", ServerKeys.ServerRecordUploadSucceed);
    }

    /// <summary>
    /// 获取当前剧演的统计数据。
    /// </summary>
    /// <returns>剧演统计数据。</returns>
    [HttpGet("Statistics")]
    public IActionResult GetStatistics()
    {
        int scheduleId = RoleCombatScheduleId.GetForNow();

        string key = $"RoleCombatStatistics:{scheduleId}";
        if (_cache.TryGetValue(key, out RoleCombatStatisticsItem? data))
        {
            return Success<RoleCombatStatisticsItem>("Role combat statistics acquired successfully", data!, ServerKeys.ServerRoleCombatStatisticsSucceed);
        }

        RoleCombatStatistics? statistics = _appDbContext.RoleCombatStatistics
            .SingleOrDefault(s => s.ScheduleId == scheduleId);

        if (statistics is null)
        {
            return Success<RoleCombatStatisticsItem>("Role combat statistics acquired successfully", default!, ServerKeys.ServerRoleCombatStatisticsSucceed);
        }

        RoleCombatStatisticsItem? typedData = JsonSerializer.Deserialize<RoleCombatStatisticsItem>(statistics.Data);
        _cache.Set(key, typedData, TimeSpan.FromDays(1));

        return Success<RoleCombatStatisticsItem>("Role combat statistics acquired successfully", typedData!, ServerKeys.ServerRoleCombatStatisticsSucceed);
    }

    private readonly struct UploadToken;
}
