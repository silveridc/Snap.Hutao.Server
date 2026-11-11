namespace server.Models.Admin;

/// <summary>补丁存储桶的对象清单，供后台选择要发布的安装包.</summary>
public sealed class ReleaseBucketListResult
{
    public string Host { get; set; } = default!;

    public string Prefix { get; set; } = default!;

    public bool Truncated
    {
        get; set;
    }

    public List<ReleaseBucketObjectItem> Objects { get; set; } = [];
}
