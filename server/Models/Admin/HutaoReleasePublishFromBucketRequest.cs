namespace server.Models.Admin;

/// <summary>从存储桶发布：只需给出对象键，校验值与下载地址由服务端补齐.</summary>
public sealed class HutaoReleasePublishFromBucketRequest
{
    public string Key { get; set; } = default!;

    /// <summary>Gets or sets 留空时从对象键的文件名里解析四段版本号.</summary>
    public string? Version
    {
        get; set;
    }

    /// <summary>Gets or sets 预先计算好的 SHA-256；留空时服务端下载整个对象计算.</summary>
    public string? Validation
    {
        get; set;
    }

    /// <summary>Gets or sets 镜像名称，留空用存储桶主机名.</summary>
    public string? MirrorName
    {
        get; set;
    }

    public string? MinimumVersion
    {
        get; set;
    }

    public string? ReleaseNotes
    {
        get; set;
    }
}
