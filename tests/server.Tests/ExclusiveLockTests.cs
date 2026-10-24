using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using server.Extension;
using server.Job;
using server.Models.Context;
using server.Models.Entity.RoleCombat;
using server.Models.Entity.SpiralAbyss;
using server.Services.GachaLog.Statistics;
using server.Services.Legacy;
using server.Services.RoleCombat;
using server.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace server.Tests;

/// <summary>
/// 统计互斥锁的守卫。历史 bug：两个记录清理 job 复制模板时把锁标志粘成了
/// <see cref="GachaLogStatisticsService.Working"/>——清理既不与各自的统计刷新互斥，
/// 又会在运行期间误挡、finally 误清抽卡统计的锁。这里的用例直接编码正确行为：
/// 清理 job 只认自己统计服务的 Working 标志，且对别的统计服务的标志视而不见。
/// </summary>
[Collection(HutaoServerCollection.Name)]
public sealed class ExclusiveLockTests
{
    private readonly HutaoServerFactory factory;

    public ExclusiveLockTests(HutaoServerFactory factory)
    {
        this.factory = factory;
    }

    private static readonly string[] AllWorkingFlags =
    [
        StatisticsService.Working,
        GachaLogStatisticsService.Working,
        RoleCombatService.Working,
    ];

    private async Task<IDistributedCache> CleanFlagsAsync(Func<Task> body)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IDistributedCache cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
        try
        {
            await body();
        }
        finally
        {
            foreach (string flag in AllWorkingFlags)
            {
                cache.Remove(flag);
            }
        }

        return cache;
    }

    private static async Task<IDistributedCache> GetCacheAsync(HutaoServerFactory factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDistributedCache>();
    }

    // ---------- RunExclusiveAsync 的契约 ----------

    [Fact]
    public async Task RunExclusiveAsync_SkipsBodyAndReturnsFalse_WhenFlagHeld()
    {
        await CleanFlagsAsync(async () =>
        {
            IDistributedCache cache = await GetCacheAsync(factory);
            string key = $"test:exclusive:{Guid.NewGuid():N}";
            cache.SetFlag(key, DistributedCacheExtension.WorkingExpiration);
            bool ran = false;

            bool result = await cache.RunExclusiveAsync(key, () =>
            {
                ran = true;
                return Task.CompletedTask;
            });

            Assert.False(result);
            Assert.False(ran, "持锁时不得执行主体");
        });
    }

    [Fact]
    public async Task RunExclusiveAsync_RunsBodyAndReleasesFlag()
    {
        await CleanFlagsAsync(async () =>
        {
            IDistributedCache cache = await GetCacheAsync(factory);
            string key = $"test:exclusive:{Guid.NewGuid():N}";
            bool flagVisibleInsideBody = false;

            bool result = await cache.RunExclusiveAsync(key, () =>
            {
                flagVisibleInsideBody = cache.HasFlag(key);
                return Task.CompletedTask;
            });

            Assert.True(result);
            Assert.True(flagVisibleInsideBody, "主体执行期间标志必须已置位");
            Assert.False(cache.HasFlag(key), "执行完必须摘除标志");
        });
    }

    [Fact]
    public async Task RunExclusiveAsync_ReleasesFlag_OnException()
    {
        await CleanFlagsAsync(async () =>
        {
            IDistributedCache cache = await GetCacheAsync(factory);
            string key = $"test:exclusive:{Guid.NewGuid():N}";

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                cache.RunExclusiveAsync(key, () => throw new InvalidOperationException()));

            Assert.False(cache.HasFlag(key), "异常路径也必须摘除标志");
        });
    }

    // ---------- 清理 job 的锁行为（历史 bug 回归） ----------

    [Fact]
    public async Task SpiralAbyssCleanJob_HonorsItsOwnLock_ButNotTheGachaOne()
    {
        await CleanFlagsAsync(async () =>
        {
            long uploadTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (long)TimeSpan.FromDays(31).TotalSeconds;
            IDistributedCache cache;
            long recordId;

            using (IServiceScope scope = factory.Services.CreateScope())
            {
                cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                EntityRecord record = new() { Uid = "lock-e2e", Uploader = "lock-e2e", UploadTime = uploadTime };
                db.Records.Add(record);
                await db.SaveChangesAsync();
                recordId = record.PrimaryId;
            }

            // 持深渊统计自己的锁：清理必须跳过，记录还在
            cache.SetFlag(StatisticsService.Working, DistributedCacheExtension.WorkingExpiration);
            using (IServiceScope scope = factory.Services.CreateScope())
            {
                SpiralAbyssRecordCleanJob job = new(scope.ServiceProvider, TimeProvider.System);
                await job.Execute(null!, TestContext.Current.CancellationToken);
            }

            using (IServiceScope scope = factory.Services.CreateScope())
            {
                Assert.True(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Records
                    .AsNoTracking().AnyAsync(r => r.PrimaryId == recordId), "持深渊统计锁时清理必须跳过");
            }

            // 持抽卡统计的锁：清理不得被它挡住（历史 bug 在这里跳过）——记录应被删除
            cache.Remove(StatisticsService.Working);
            cache.SetFlag(GachaLogStatisticsService.Working, DistributedCacheExtension.WorkingExpiration);
            using (IServiceScope scope = factory.Services.CreateScope())
            {
                SpiralAbyssRecordCleanJob job = new(scope.ServiceProvider, TimeProvider.System);
                await job.Execute(null!, TestContext.Current.CancellationToken);
            }

            using (IServiceScope scope = factory.Services.CreateScope())
            {
                Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Records
                    .AsNoTracking().AnyAsync(r => r.PrimaryId == recordId), "抽卡统计的锁不应挡住深渊清理");
            }

            // job 自己的锁必须摘除；别人（抽卡统计）的锁是本用例刻意持着的，job 不得误清——
            // 历史 bug 的 finally 会把抽卡的锁误摘掉，这里的 True 断言就是那条守卫
            Assert.False(cache.HasFlag(StatisticsService.Working), "自己的锁必须摘除");
            Assert.True(cache.HasFlag(GachaLogStatisticsService.Working), "别人的锁不得被 job 误清");
        });
    }

    [Fact]
    public async Task RoleCombatCleanJob_HonorsItsOwnLock_ButNotTheGachaOne()
    {
        await CleanFlagsAsync(async () =>
        {
            IDistributedCache cache;

            using (IServiceScope scope = factory.Services.CreateScope())
            {
                cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
                AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.RoleCombatRecords.Add(new RoleCombatRecord { Uid = "lock-e2e", Uploader = "lock-e2e", UploadTime = 1, Avatars = [] });
                await db.SaveChangesAsync();
            }

            // 持幻牌统计自己的锁：清理跳过
            cache.SetFlag(RoleCombatService.Working, DistributedCacheExtension.WorkingExpiration);
            using (IServiceScope scope = factory.Services.CreateScope())
            {
                RoleCombatRecordCleanJob job = new(scope.ServiceProvider);
                await job.Execute(null!, TestContext.Current.CancellationToken);
            }

            using (IServiceScope scope = factory.Services.CreateScope())
            {
                Assert.True(await scope.ServiceProvider.GetRequiredService<AppDbContext>().RoleCombatRecords
                    .AsNoTracking().AnyAsync(r => r.Uid == "lock-e2e"), "持幻牌统计锁时清理必须跳过");
            }

            // 持抽卡统计的锁：清理不得被它挡住
            cache.Remove(RoleCombatService.Working);
            cache.SetFlag(GachaLogStatisticsService.Working, DistributedCacheExtension.WorkingExpiration);
            using (IServiceScope scope = factory.Services.CreateScope())
            {
                RoleCombatRecordCleanJob job = new(scope.ServiceProvider);
                await job.Execute(null!, TestContext.Current.CancellationToken);
            }

            using (IServiceScope scope = factory.Services.CreateScope())
            {
                Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().RoleCombatRecords
                    .AsNoTracking().AnyAsync(r => r.Uid == "lock-e2e"), "抽卡统计的锁不应挡住幻牌清理");
            }
        });
    }
}
