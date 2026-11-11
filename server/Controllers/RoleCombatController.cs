using Microsoft.EntityFrameworkCore.Storage;
using server.Models.Entity.RoleCombat;
using server.Models.RoleCombat;
using System.Collections.Concurrent;

namespace server.Controllers;

[ApiController]
[Route("api/records/role-combat")]
[ApiExplorerSettings(GroupName = "RoleCombat")]
public class RoleCombatController(AppDbContext appDbContext, IDistributedCache cache, TimeProvider timeProvider)
{
    private static readonly ConcurrentDictionary<string, UploadToken> UploadingUids = new();

    /// <summary>
    /// Uploads an Imaginarium Theater record.
    /// </summary>
    /// <param name="record">The Imaginarium Theater record data.</param>
    /// <returns>The upload result.</returns>
    [HttpPost]
    public async Task<IActionResult> Upload([FromBody] SimpleRoleCombatRecord record)
    {
        if (cache.HasFlag(RoleCombatService.Working))
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

        await using (IDbContextTransaction transaction = await appDbContext.Database.BeginTransactionAsync().ConfigureAwait(false))
        {
            RoleCombatRecord? exist = await appDbContext.RoleCombatRecords.SingleOrDefaultAsync(r => r.Uid == record.Uid).ConfigureAwait(false);

            if (exist is not null)
            {
                await appDbContext.RoleCombatAvatars.Where(a => a.RecordId == exist.PrimaryId).ExecuteDeleteAsync().ConfigureAwait(false);
            }

            await appDbContext.RoleCombatRecords.Where(r => r.Uid == record.Uid).ExecuteDeleteAsync().ConfigureAwait(false);

            RoleCombatRecord newRecord = new()
            {
                Uid = record.Uid,
                Uploader = record.Identity,
                UploadTime = timeProvider.GetUnixTimeSeconds(),
            };
            await appDbContext.RoleCombatRecords.AddAndSaveAsync(newRecord).ConfigureAwait(false);

            long recordId = newRecord.PrimaryId;
            List<RoleCombatAvatar> avatars = [.. record.BackupAvatars.Select(id => new RoleCombatAvatar() { AvatarId = id, RecordId = recordId })];
            await appDbContext.RoleCombatAvatars.AddRangeAndSaveAsync(avatars).ConfigureAwait(false);

            await transaction.CommitAsync().ConfigureAwait(false);
        }

        if (!UploadingUids.TryRemove(record.Uid, out _))
        {
            return Fail("Submission state error", ReturnCode.InternalStateException, ServerKeys.ServerRoleCombatInternalException);
        }

        return Response.Success("Data uploaded successfully", ServerKeys.ServerRecordUploadSucceed);
    }

    /// <summary>
    /// Gets the statistics of the current Imaginarium Theater schedule.
    /// </summary>
    /// <returns>The Imaginarium Theater statistics.</returns>
    [HttpGet("statistics")]
    public IActionResult GetStatistics()
    {
        int scheduleId = RoleCombatScheduleId.GetForNow();

        string key = $"RoleCombatStatistics:{scheduleId}";
        if (cache.TryGetValue(key, out RoleCombatStatisticsItem? data))
        {
            return Success<RoleCombatStatisticsItem>("Role combat statistics acquired successfully", data!, ServerKeys.ServerRoleCombatStatisticsSucceed);
        }

        RoleCombatStatistics? statistics = appDbContext.RoleCombatStatistics
            .SingleOrDefault(s => s.ScheduleId == scheduleId);

        if (statistics is null)
        {
            return Success<RoleCombatStatisticsItem>("Role combat statistics acquired successfully", default!, ServerKeys.ServerRoleCombatStatisticsSucceed);
        }

        RoleCombatStatisticsItem? typedData = JsonSerializer.Deserialize<RoleCombatStatisticsItem>(statistics.Data);
        cache.Set(key, typedData, TimeSpan.FromDays(1));

        return Success<RoleCombatStatisticsItem>("Role combat statistics acquired successfully", typedData!, ServerKeys.ServerRoleCombatStatisticsSucceed);
    }

    private readonly struct UploadToken;
}
