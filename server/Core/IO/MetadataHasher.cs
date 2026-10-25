using System.Text.Json.Nodes;

namespace server.Core.IO;

/// <summary>
/// 启动器 metadata 的哈希与序列化口径，两处都必须与启动器逐字节一致：
/// <list type="bullet">
/// <item><description>哈希 = 去掉所有 <c>0x0D</c> 后的 UTF-8 字节做 XXH64（seed 0），输出大写 16 位十六进制，见启动器 <c>Core/IO/Hashing/XxHash64.cs</c> 与 <c>MetadataService.HashFileNormalizedAsync</c>。</description></item>
/// <item><description>序列化 = 2 空格缩进、非 ASCII 不转义、行尾 LF。实测上游 15 locale × 156 文档全部能经 <see cref="JsonNode"/> 往返逐字节复现，所以改完 i18n 再写回不会带来无意义的整文件 diff。</description></item>
/// </list>
/// </summary>
public static class MetadataHasher
{
    /// <summary>上游生成这批文件时用的写法，改动它会立刻让全量哈希对不上。</summary>
    public static JsonSerializerOptions WriteOptions { get; } = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>把 CRLF/CR 归一化成 LF。JSON 字符串里的裸 0x0D 只会是行尾，合法的换行在文本里是 <c>\r</c> 两个字符，不受影响。</summary>
    public static string NormalizeLineEndings(string text)
    {
        return text.Contains('\r') ? text.Replace("\r", string.Empty) : text;
    }

    /// <summary>对已经归一化过的文本算 XXH64，返回大写十六进制。</summary>
    public static string Hash(string normalized)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(normalized);
        Span<byte> hash = stackalloc byte[8];
        if (System.IO.Hashing.XxHash64.Hash(bytes, hash) is not 8)
        {
            throw new InvalidOperationException("XxHash64 did not fill the destination");
        }

        return Convert.ToHexString(hash);
    }

    public static string HashText(string text)
    {
        return Hash(NormalizeLineEndings(text));
    }

    public static string Serialize(JsonNode node)
    {
        return NormalizeLineEndings(node.ToJsonString(WriteOptions));
    }

    public static string Serialize<TValue>(TValue value)
    {
        return NormalizeLineEndings(JsonSerializer.Serialize(value, WriteOptions));
    }

    /// <summary>
    /// 序列化并算出该字节串的哈希，落库时一次做完。
    /// </summary>
    /// <typeparam name="TValue">要序列化的类型。</typeparam>
    public static (string Doc, string Hash) SerializeWithHash<TValue>(TValue value)
    {
        string doc = Serialize(value);
        return (doc, Hash(doc));
    }
}
