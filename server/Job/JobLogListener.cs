using Quartz;

namespace server.Job;

// 由调度器长期持有，所以不能直接吃 scoped 的 AppDbContext，写库时自己开作用域
public sealed class JobLogListener(IServiceScopeFactory serviceScopeFactory, ILogger<JobLogListener> logger) : IJobListener
{
    private const int _maxErrorLength = 2000;

    /// <summary>
    /// Persists the execution record of a Quartz job; a failure to write the log never affects the job itself.
    /// </summary>
    /// <param name="context">The Quartz execution context.</param>
    /// <param name="exception">The exception thrown by the job, or <see langword="null"/> when it succeeded.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask JobWasExecuted(IJobExecutionContext context, JobExecutionException? exception, CancellationToken cancellationToken = default)
    {
        try
        {
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            appDbContext.JobLogs.Add(new()
            {
                JobType = context.JobDetail.JobType.FullName,
                Success = exception is null,
                Error = exception is null ? null : Truncate(exception.ToString(), _maxErrorLength),
                StartedAt = context.FireTimeUtc,
                DurationMs = (long)context.JobRunTime.TotalMilliseconds,
            });

            await appDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 记日志失败不能反过来影响任务本身
            logger.LogWarning(ex, "Failed to write job log");
        }
    }

    private static string? Truncate(string? value, int maxLength)
    {
        return value is null || value.Length <= maxLength ? value : value[..maxLength];
    }
}
