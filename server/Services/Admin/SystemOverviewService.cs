using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using MySqlConnector;
using Quartz;
using server.Models.Admin;
using StackExchange.Redis;

namespace server.Services.Admin;

// Scoped
public sealed class SystemOverviewService(
    AppDbContext appDbContext,
    IConnectionMultiplexer multiplexer,
    ISchedulerFactory schedulerFactory,
    ILogger<SystemOverviewService> logger)
{
    private const int _cpuSampleMs = 150;

    /// <summary>
    /// 采集服务端运行状态：进程与机器信息、资源占用、两套 MySQL 连接、Redis、Quartz 调度。
    /// </summary>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>系统概览数据。</returns>
    public async Task<SystemOverview> GetOverviewAsync(CancellationToken cancellationToken)
    {
        using Process process = Process.GetCurrentProcess();
        TimeSpan cpuBefore = process.TotalProcessorTime;
        DateTimeOffset startedAt = process.StartTime.ToUniversalTime();
        DateTimeOffset sampleBefore = DateTimeOffset.UtcNow;

        // 采样窗口：进程累计 CPU 时间要除以实测窗口，用启动至今的平均值会把瞬时占用抹平
        await Task.Delay(_cpuSampleMs, cancellationToken).ConfigureAwait(false);

        process.Refresh();
        double sampleSeconds = (DateTimeOffset.UtcNow - sampleBefore).TotalSeconds;
        TimeSpan cpu = process.TotalProcessorTime - cpuBefore;

        RuntimeInfo runtime = new(
            ApplicationVersion: Program.applicationVersion,
            MachineName: Environment.MachineName,
            OperatingSystem: RuntimeInformation.OSDescription,
            Framework: RuntimeInformation.FrameworkDescription,
            ProcessId: Environment.ProcessId,
            StartedAt: startedAt,
            UptimeSeconds: (long)(DateTimeOffset.UtcNow - startedAt).TotalSeconds,
            LocalTime: DateTimeOffset.Now,
            TimeZoneId: TimeZoneInfo.Local.Id);

        ResourceUsage resource = new(
            ProcessorCount: Environment.ProcessorCount,
            ProcessCpuPercent: Math.Round(cpu.TotalSeconds / sampleSeconds / Environment.ProcessorCount * 100d, 2),
            ProcessCpuSeconds: Math.Round(process.TotalProcessorTime.TotalSeconds, 2),
            WorkingSetBytes: process.WorkingSet64,
            PrivateMemoryBytes: process.PrivateMemorySize64,
            ManagedHeapBytes: GC.GetGCMemoryInfo().HeapSizeBytes,
            ThreadCount: process.Threads.Count);

        List<DatabaseInfo> databases =
        [
            await ProbeDatabaseAsync("主库", appDbContext, cancellationToken).ConfigureAwait(false),
        ];

        return new SystemOverview(runtime, resource, databases, await ProbeRedisAsync(cancellationToken).ConfigureAwait(false), await ProbeSchedulerAsync(cancellationToken).ConfigureAwait(false));
    }

    private static string Describe(EndPoint endpoint)
    {
        return endpoint switch
        {
            DnsEndPoint dns => $"{dns.Host}:{dns.Port}",
            IPEndPoint ip => $"{ip.Address}:{ip.Port}",
            _ => endpoint.ToString()!,
        };
    }

    private static string SafeMessage(Exception exception)
    {
        string message = exception.Message;
        int password = message.IndexOf("password=", StringComparison.OrdinalIgnoreCase);
        return password < 0 ? message : string.Concat(message.AsSpan(0, password), "password=***");
    }

    private static Dictionary<string, string> ParseInfo(string info)
    {
        Dictionary<string, string> fields = [];
        foreach (string line in info.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = line.IndexOf(':');
            if (separator > 0 && line[0] != '#')
            {
                fields[line[..separator]] = line[(separator + 1)..].Trim();
            }
        }

        return fields;
    }

    private static long? ParseLong(string? value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : null;
    }

    private static long CountKeys(Dictionary<string, string> info)
    {
        long total = 0;
        foreach (string value in info.Values)
        {
            int marker = value.IndexOf("keys=", StringComparison.Ordinal);
            if (marker >= 0 && long.TryParse(value.AsSpan(marker + 5), NumberStyles.Integer, CultureInfo.InvariantCulture, out long keys))
            {
                total += keys;
            }
        }

        return total;
    }

    private async Task<DatabaseInfo> ProbeDatabaseAsync(string name, DbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            if (dbContext.Database.GetDbConnection() is not MySqlConnection connection)
            {
                throw new InvalidOperationException($"Unsupported provider {dbContext.Database.GetDbConnection().GetType().Name}");
            }

            MySqlConnectionStringBuilder builder = new(connection.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                await using DbCommand command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();

                return new DatabaseInfo(
                    Name: name,
                    Connected: true,
                    ServerVersion: connection.ServerVersion,
                    Host: builder.Server,
                    Port: (int)builder.Port,
                    Database: builder.Database,
                    MaxPoolSize: (int)builder.MaximumPoolSize,
                    RoundtripMs: Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                    Error: null);
            }
            finally
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Database probe {Name} failed", name);
            return new DatabaseInfo(name, false, null, null, null, null, null, null, SafeMessage(exception));
        }
    }

    private async Task<RedisInfo> ProbeRedisAsync(CancellationToken cancellationToken)
    {
        EndPoint[] endPoints = multiplexer.GetEndPoints();
        if (endPoints.Length is 0)
        {
            return new RedisInfo(false, null, null, DistributedCacheExtension.InstanceName, null, null, null, null, null, null, "未配置可用的 Redis 端点");
        }

        string endpoint = Describe(endPoints[0]);
        IServer server = multiplexer.GetServer(endPoints[0]);
        double? pingMs;
        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            await multiplexer.GetDatabase().PingAsync().ConfigureAwait(false);
            stopwatch.Stop();
            pingMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Redis probe failed");
            return new RedisInfo(false, endpoint, null, DistributedCacheExtension.InstanceName, null, null, null, null, null, null, SafeMessage(exception));
        }

        try
        {
            RedisResult info = await server.ExecuteAsync("INFO", []).ConfigureAwait(false);
            Dictionary<string, string> fields = ParseInfo(info.ToString() ?? string.Empty);

            return new RedisInfo(
                Connected: server.IsConnected,
                Endpoint: endpoint,
                Version: fields.GetValueOrDefault("redis_version"),
                InstanceName: DistributedCacheExtension.InstanceName,
                UsedMemory: fields.GetValueOrDefault("used_memory_human"),
                MaxMemoryPolicy: fields.GetValueOrDefault("maxmemory_policy"),
                ConnectedClients: ParseLong(fields.GetValueOrDefault("connected_clients")),
                KeyCount: CountKeys(fields),
                UptimeDays: ParseLong(fields.GetValueOrDefault("uptime_in_days")),
                PingMs: pingMs,
                Error: null);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Redis INFO command failed");
            return new RedisInfo(server.IsConnected, endpoint, null, DistributedCacheExtension.InstanceName, null, null, null, null, null, pingMs, SafeMessage(exception));
        }
    }

    private async Task<SchedulerInfo> ProbeSchedulerAsync(CancellationToken cancellationToken)
    {
        try
        {
            IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken).ConfigureAwait(false);
            IReadOnlyCollection<JobKey> jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), cancellationToken).ConfigureAwait(false);
            IReadOnlyCollection<TriggerKey> triggerKeys = await scheduler.GetTriggerKeys(GroupMatcher<TriggerKey>.AnyGroup(), cancellationToken).ConfigureAwait(false);
            List<SchedulerJobInfo> jobs = [];
            foreach (JobKey jobKey in jobKeys)
            {
                IJobDetail? detail = await scheduler.GetJobDetail(jobKey, cancellationToken).ConfigureAwait(false);
                IReadOnlyCollection<ITrigger> triggers = await scheduler.GetTriggersOfJob(jobKey, cancellationToken).ConfigureAwait(false);
                ITrigger? trigger = triggers.FirstOrDefault();
                string state = trigger is null ? "None" : (await scheduler.GetTriggerState(trigger.Key, cancellationToken).ConfigureAwait(false)).ToString();

                jobs.Add(new SchedulerJobInfo(
                    Group: jobKey.Group,
                    Name: jobKey.Name,
                    JobType: detail?.JobType.FullName ?? string.Empty,
                    Cron: (trigger as ICronTrigger)?.CronExpressionString,
                    State: state,
                    NextFireAt: trigger?.NextFireTimeUtc?.ToUniversalTime(),
                    PreviousFireAt: trigger?.PreviousFireTimeUtc?.ToUniversalTime()));
            }

            return new SchedulerInfo(
                Name: scheduler.SchedulerName,
                InstanceId: scheduler.SchedulerInstanceId,
                State: scheduler.Status.ToString(),
                JobCount: jobKeys.Count,
                TriggerCount: triggerKeys.Count,
                Jobs: jobs,
                Error: null);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Quartz probe failed");
            return new SchedulerInfo("Quartz", string.Empty, "Unknown", 0, 0, [], SafeMessage(exception));
        }
    }
}
