namespace server.Models.Admin;

/// <summary>补丁存储桶中的一个对象.</summary>
public sealed class ReleaseBucketObjectItem
{
    public string Key { get; set; } = default!;

    public long Size
    {
        get; set;
    }

    public DateTimeOffset LastModified
    {
        get; set;
    }
}
