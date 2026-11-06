namespace server.Models.Admin;

public sealed class JobLogItem
{
    public long Id
    {
        get; set;
    }

    /// <summary>Gets or sets 任务实现类型，与概览页调度表的 JobType 取值一致.</summary>
    public string JobType { get; set; } = default!;

    public bool Success
    {
        get; set;
    }

    /// <summary>Gets or sets 失败时的异常全文，成功时为空.</summary>
    public string? Error
    {
        get; set;
    }

    public DateTimeOffset StartedAt
    {
        get; set;
    }

    public long DurationMs
    {
        get; set;
    }
}
