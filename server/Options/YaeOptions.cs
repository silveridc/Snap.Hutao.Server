namespace server.Options;

public sealed class YaeOptions
{
    /// <summary>
    /// 拉取 Yae 版本表（protobuf）的地址，按顺序尝试。
    /// 上游 bucket 只有这一个数据源，国内镜像返回 403，所以默认列两个可达域名。
    /// </summary>
    public string[] BucketUrls { get; set; } =
    [
        "https://ena-rin.holohat.work/schicksal/metadata",
        "https://rin.holohat.work/schicksal/metadata",
    ];

    /// <summary>
    /// 国服主 exe 的指纹集合：值为 <c>CRC32(YuanShen.exe 的前 64 KiB)</c>。
    /// Yae 的 method_rva 是一张以该指纹为 key 的表，命中的写进 chinese，其余写进 oversea。
    /// 集合为空时同步会直接失败而不是猜测，因为猜错等于给注入下发错误的 RVA。
    /// </summary>
    public uint[] ChineseGameHashes { get; set; } = [];
}
