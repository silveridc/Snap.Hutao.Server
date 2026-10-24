namespace server.Models.Metadata;

/// <summary>
/// 启动器 metadata 的原文档表：一行 = 一个 locale 的一个 JSON 文档。
/// <see cref="Doc"/> 就是对外吐出去的字节本体，不做任何二次加工，
/// 因此 <c>/metadata/Genshin/{locale}/{name}.json</c> 能与上游静态站逐字节一致。
/// </summary>
[Table("metadata_document")]
[PrimaryKey(nameof(Locale), nameof(Name))]
[Index(nameof(Name), nameof(Locale), Name = "IX_metadata_document_Name_Locale")]
public sealed class MetadataDocument
{
    /// <summary>语言目录名，与 URL 里的一致（CHS、CHT、EN…）。</summary>
    [MaxLength(8)]
    public string Locale { get; set; } = default!;

    /// <summary>去掉 <c>.json</c> 的相对路径，如 <c>Achievement</c>、<c>Avatar/10000002</c>、<c>Meta</c>。</summary>
    [MaxLength(128)]
    public string Name { get; set; } = default!;

    /// <summary>行尾已归一化成 LF 的 JSON 原文。</summary>
    [Column(TypeName = "longtext")]
    public string Doc { get; set; } = default!;

    /// <summary>
    /// <see cref="Doc"/> 去掉所有 <c>0x0D</c> 后 UTF-8 字节的 XXH64，大写 16 位十六进制，
    /// 与 <c>Meta.json</c> 里给启动器校验的那套值同算法同口径。
    /// </summary>
    [MaxLength(16)]
    public string Hash { get; set; } = default!;

    /// <summary><see cref="Doc"/> 的 UTF-8 字节数，用于运维核对与响应体积估算。</summary>
    public uint Size { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UpdatedAt { get; set; }
}
