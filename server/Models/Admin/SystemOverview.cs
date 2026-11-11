namespace server.Models.Admin;

public sealed record SystemOverview(
    RuntimeInfo Runtime,
    ResourceUsage Resource,
    IReadOnlyList<DatabaseInfo> Databases,
    RedisInfo Redis,
    SchedulerInfo Scheduler);

public sealed record RuntimeInfo(
    string ApplicationVersion,
    string MachineName,
    string OperatingSystem,
    string Framework,
    int ProcessId,
    DateTimeOffset StartedAt,
    long UptimeSeconds,
    DateTimeOffset LocalTime,
    string TimeZoneId);

public sealed record ResourceUsage(
    int ProcessorCount,
    double ProcessCpuPercent,
    double ProcessCpuSeconds,
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    long ManagedHeapBytes,
    int ThreadCount);

public sealed record DatabaseInfo(
    string Name,
    bool Connected,
    string? ServerVersion,
    string? Host,
    int? Port,
    string? Database,
    int? MaxPoolSize,
    double? RoundtripMs,
    string? Error);

public sealed record RedisInfo(
    bool Connected,
    string? Endpoint,
    string? Version,
    string InstanceName,
    string? UsedMemory,
    string? MaxMemoryPolicy,
    long? ConnectedClients,
    long? KeyCount,
    long? UptimeDays,
    double? PingMs,
    string? Error);

public sealed record SchedulerInfo(
    string Name,
    string InstanceId,
    string State,
    int JobCount,
    int TriggerCount,
    IReadOnlyList<SchedulerJobInfo> Jobs,
    string? Error);

public sealed record SchedulerJobInfo(
    string Group,
    string Name,
    string JobType,
    string? Cron,
    string State,
    DateTimeOffset? NextFireAt,
    DateTimeOffset? PreviousFireAt);
