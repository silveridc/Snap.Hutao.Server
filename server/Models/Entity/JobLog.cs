namespace server.Models.Entity;

[Table("job_logs")]
[Index(nameof(JobType))]
public sealed class JobLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id
    {
        get; set;
    }

    /// <summary>Gets or sets 任务实现类型，取值与概览页调度表里的 JobType 一致（<c>命名空间.类名, 程序集</c>）；调度器未指定 JobKey，每次启动生成的 GUID 名称跨启动对不上，所以按类型归集.</summary>
    [MaxLength(256)]
    public string JobType { get; set; } = default!;

    public bool Success
    {
        get; set;
    }

    /// <summary>Gets or sets 失败时的异常全文（超长截断），成功时为空.</summary>
    [MaxLength(2000)]
    public string? Error
    {
        get; set;
    }

    /// <summary>Gets or sets 触发器实际开火时间.</summary>
    public DateTimeOffset StartedAt
    {
        get; set;
    }

    public long DurationMs
    {
        get; set;
    }
}
