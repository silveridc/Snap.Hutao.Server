using Microsoft.EntityFrameworkCore.Storage;
using server.Extension;
using server.Models.Context;
using server.Models.Entity.RoleCombat;
using server.Models.Response;
using server.Models.RoleCombat;
using server.Services.RoleCombat;
using System.Collections.Concurrent;

namespace server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [ApiExplorerSettings(GroupName = "RoleCombat")]
    public class RoleCombatController(IServiceProvider serviceProvider)
    {
        private static readonly ConcurrentDictionary<string, UploadToken> UploadingUids = new();

        private readonly AppDbContext appDbContext = serviceProvider.GetRequiredService<AppDbContext>();
        private readonly IDistributedCache cache = serviceProvider.GetRequiredService<IDistributedCache>();

        /// <summary>
        /// 上传剧演记录。
        /// </summary>
        /// <param name="record">剧演记录数据。</param>
        /// <returns>上传结果。</returns>
        [HttpPost("Upload")]
        public async Task<IActionResult> Upload([FromBody] SimpleRoleCombatRecord record)
        {
            if (cache.HasFlag(RoleCombatService.Working))
            {
                return Response.Fail(ReturnCode.ComputingStatistics, "Computing statistics", ServerKeys.ServerRoleCombatComputingStatistics);
            }

            if (record.ScheduleId != RoleCombatScheduleId.GetForNow())
            {
                return Response.Fail(ReturnCode.NotCurrentSchedule, "Not current role combat schedule data", ServerKeys.ServerRoleCombatNotCurrentSchedule);
            }

            if (!record.Validate())
            {
                return Response.Fail(ReturnCode.InvalidUploadData, "Invalid upload data", ServerKeys.ServerRoleCombatInvalidData);
            }

            if (UploadingUids.TryGetValue(record.Uid, out _) || !UploadingUids.TryAdd(record.Uid, default))
            {
                return Response.Fail(ReturnCode.PreviousRequestNotCompleted, "A previous request for this Uid is still being processed", ServerKeys.ServerRoleCombatPreviousRequestNotCompleted);
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
                    UploadTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                };
                await appDbContext.RoleCombatRecords.AddAndSaveAsync(newRecord).ConfigureAwait(false);

                long recordId = newRecord.PrimaryId;
                List<RoleCombatAvatar> avatars = [.. record.BackupAvatars.Select(id => new RoleCombatAvatar() { AvatarId = id, RecordId = recordId })];
                await appDbContext.RoleCombatAvatars.AddRangeAndSaveAsync(avatars).ConfigureAwait(false);

                await transaction.CommitAsync().ConfigureAwait(false);
            }

            if (!UploadingUids.TryRemove(record.Uid, out _))
            {
                return Response.Fail(ReturnCode.InternalStateException, "Submission state error", ServerKeys.ServerRoleCombatInternalException);
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
            if (cache.TryGetValue(key, out RoleCombatStatisticsItem? data))
            {
                return Response<RoleCombatStatisticsItem>.Success("Role combat statistics acquired successfully", ServerKeys.ServerRoleCombatStatisticsSucceed, data!);
            }

            RoleCombatStatistics? statistics = appDbContext.RoleCombatStatistics
                .SingleOrDefault(s => s.ScheduleId == scheduleId);

            if (statistics is null)
            {
                return Response<RoleCombatStatisticsItem>.Success("Role combat statistics acquired successfully", ServerKeys.ServerRoleCombatStatisticsSucceed, default!);
            }

            RoleCombatStatisticsItem? typedData = JsonSerializer.Deserialize<RoleCombatStatisticsItem>(statistics.Data);
            cache.Set(key, typedData, TimeSpan.FromDays(1));

            return Response<RoleCombatStatisticsItem>.Success("Role combat statistics acquired successfully", ServerKeys.ServerRoleCombatStatisticsSucceed, typedData!);
        }

        private readonly struct UploadToken;
    }
}