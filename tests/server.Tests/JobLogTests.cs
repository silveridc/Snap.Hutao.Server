using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Extensibility;
using server.Job;
using server.Models.Admin;
using server.Tests.Infrastructure;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class JobLogTests
{
    private readonly HutaoServerFactory factory;
    private readonly HttpClient client;
    private readonly TestUsers users;

    public JobLogTests(HutaoServerFactory factory)
    {
        this.factory = factory;
        client = factory.CreateApiClient();
        users = new TestUsers(factory);
    }

    [Fact]
    public async Task JobLog_RecordsSuccessAndFailure_AndQueryableThroughAdminRoute()
    {
        DateTimeOffset started = DateTimeOffset.UtcNow.AddMinutes(-1);
        IJobExecutionContext successContext = BuildContext(started);
        IJobExecutionContext failureContext = BuildContext(started);

        JobLogListener listener = factory.Services.GetRequiredService<JobLogListener>();
        await listener.JobWasExecuted(successContext, null, TestContext.Current.CancellationToken);

        string longMessage = new('x', 5000);
        await listener.JobWasExecuted(failureContext, new JobExecutionException(longMessage), TestContext.Current.CancellationToken);

        // Quartz 4.6 的 IJobDetail.JobType 不是 System.Type，它的 FullName 自带「, 程序集名」后缀，
        // 落库与概览页调度表都用这个字符串，过滤值必须同源
        string jobType = successContext.JobDetail.JobType.FullName;
        TestUser maintainer = await users.MaintainerAsync();
        using HttpResponseMessage listResponse = await client.GetWithTokenAsync(
            $"/api/admin/job-logs?page=1&pageSize=50&jobType={Uri.EscapeDataString(jobType)}",
            maintainer.AccessToken);
        TestResponse list = await listResponse.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, list.Retcode);

        List<JobLogItem> items =
            list.DeserializeData<server.Models.Admin.PagedResult<JobLogItem>>()!.Items;
        Assert.Equal(2, items.Count);

        JobLogItem succeeded = Assert.Single(items, item => item.Success);
        Assert.Equal(jobType, succeeded.JobType);
        Assert.Null(succeeded.Error);
        Assert.Equal(5000, succeeded.DurationMs);
        Assert.True(Math.Abs((succeeded.StartedAt - started).TotalSeconds) < 1);

        JobLogItem failed = Assert.Single(items, item => !item.Success);
        Assert.Equal(jobType, failed.JobType);
        Assert.NotNull(failed.Error);
        // 异常全文按 2000 字符截断落库，超长堆栈不撑爆行
        Assert.Equal(2000, failed.Error!.Length);

        using HttpResponseMessage failureOnlyResponse = await client.GetWithTokenAsync(
            $"/api/admin/job-logs?page=1&pageSize=50&jobType={Uri.EscapeDataString(jobType)}&success=false",
            maintainer.AccessToken);
        TestResponse failureOnly = await failureOnlyResponse.ReadTestResponseAsync();
        Assert.Equal(ReturnCodes.Success, failureOnly.Retcode);

        List<JobLogItem> failureOnlyItems =
            failureOnly.DeserializeData<server.Models.Admin.PagedResult<JobLogItem>>()!.Items;
        Assert.Single(failureOnlyItems, item => !item.Success);
    }

    // 调度器在测试宿主里不启动，用官方 builder 直接构造一次"执行完毕"的上下文：
    // UtcNow 恒为 started；时间戳第一读返回 0（构建时刻），之后每读前进 5 秒，
    // JobRunTime = GetElapsedTime(构建时间戳) 就是 5 秒
    private static IJobExecutionContext BuildContext(DateTimeOffset started)
    {
        return JobExecutionContextBuilder.For(new ProbeJob())
            .WithJob(JobBuilder.Create<ProbeJob>().WithIdentity("probe", "tests").Build())
            .FiredAt(started)
            .WithTimeProvider(new SteppedTimeProvider(started, TimeSpan.FromSeconds(5)))
            .Build();
    }

    private sealed class ProbeJob : IJob
    {
        public ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }

    private sealed class SteppedTimeProvider(DateTimeOffset start, TimeSpan step) : TimeProvider
    {
        private long calls;

        public override DateTimeOffset GetUtcNow() => start;

        public override long GetTimestamp()
        {
            long n = Interlocked.Increment(ref calls);
            return (long)(step.Ticks * (n - 1) * TimestampFrequency / TimeSpan.TicksPerSecond);
        }
    }
}
