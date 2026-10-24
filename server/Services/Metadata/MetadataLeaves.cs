using System.Text.Json.Nodes;

namespace server.Services.Metadata;

/// <summary>
/// 骨架里「这个叶子交给 i18n 表」的记法，以及按路径读写叶子的公共走法。
/// 投影和装配必须用同一套，否则占位符会原样漏到响应里。
/// <para>
/// 占位符取 <c>"\u0000i18n:/Name"</c>：实测整棵 metadata 树（15 locale × 156 文档）里没有任何字符串含 U+0000，
/// 所以它不可能与自然内容撞车；又因为它是字符串，序列化成 <c>\u0000</c> 转义后仍然是合法 JSON，
/// 且被 <c>metadata_object</c> 上那些生成列的 <c>NULL ON ERROR</c> 判成「不是数字」，不会污染提列。
/// </para>
/// <para>
/// 路径以对象自身为原点，段用 <c>/</c> 分隔、按 RFC 6901 转义（<c>~</c> → <c>~0</c>，<c>/</c> → <c>~1</c>），
/// 数组下标直接写成 <c>/0</c>，与 <c>metadata_translation.Pointer</c> 的口径一致。
/// </para>
/// </summary>
public static class MetadataLeaves
{
    /// <summary>形状指纹里表示「这一层是对象」的字符。</summary>
    private const char _objectShape = 'O';

    /// <summary>形状指纹里表示「这一层是数组」的字符。</summary>
    private const char _arrayShape = 'A';

    private const string _prefix = "\u0000i18n:";

    /// <summary>把骨架里某个叶子换成占位符。</summary>
    public static string Placeholder(string path)
    {
        return _prefix + path;
    }

    /// <summary>取出占位符指向的路径；不是占位符返回 false。</summary>
    public static bool TryPlaceholder(JsonNode? node, [NotNullWhen(true)] out string? path)
    {
        path = null;
        if (node is not JsonValue value || !value.TryGetValue(out string? text))
        {
            return false;
        }

        if (!text.StartsWith(_prefix, StringComparison.Ordinal))
        {
            return false;
        }

        path = text[_prefix.Length..];
        return true;
    }

    /// <summary>
    /// 按文档顺序摊开所有叶子，给出路径、形状指纹与 JSON 原文；空对象/空数组也算一个叶子。
    /// <para>
    /// 形状指纹是路径上各层容器的类型串（<see cref="_objectShape"/>/<see cref="_arrayShape"/>）。
    /// 只比路径不够：<c>{"a":{"0":1}}</c> 与 <c>{"a":[1]}</c> 的叶子路径都是 <c>/a/0</c>，
    /// 把它们当成同一种结构，装配就会把对象拼成数组，字节全错。
    /// </para>
    /// <para>
    /// 原文取 <see cref="JsonElement.GetRawText"/>，就是文档里那一段字节本身（标量原样、容器去空白），
    /// 装配时贴回去再用同一口径序列化即可逐字节复现，不必再走一遍编码器。
    /// </para>
    /// </summary>
    public static void Collect(JsonElement root, List<(string Path, string Shape, string Text)> into)
    {
        Walk(root, string.Empty, string.Empty, into);

        static void Walk(JsonElement current, string path, string shape, List<(string Path, string Shape, string Text)> sink)
        {
            if (current.ValueKind is JsonValueKind.Object)
            {
                string own = shape + _objectShape;
                int count = 0;
                foreach (JsonProperty property in current.EnumerateObject())
                {
                    Walk(property.Value, $"{path}/{Escape(property.Name)}", own, sink);
                    count++;
                }

                if (count is 0)
                {
                    sink.Add((path, own, "{}"));
                }

                return;
            }

            if (current.ValueKind is JsonValueKind.Array)
            {
                string own = shape + _arrayShape;
                int index = 0;
                foreach (JsonElement item in current.EnumerateArray())
                {
                    Walk(item, $"{path}/{index}", own, sink);
                    index++;
                }

                if (index is 0)
                {
                    sink.Add((path, own, "[]"));
                }

                return;
            }

            sink.Add((path, shape, current.GetRawText()));
        }
    }

    /// <summary>按路径取叶子所在的节点（用于读值）。</summary>
    public static bool TryGet(JsonNode? root, string path, [NotNullWhen(true)] out JsonNode? node)
    {
        node = null;
        if (!TrySegments(path, out string[] segments))
        {
            return false;
        }

        JsonNode? current = root;
        foreach (string segment in segments)
        {
            if (current is JsonObject owner)
            {
                if (!owner.TryGetPropertyValue(segment, out current))
                {
                    return false;
                }

                continue;
            }

            if (current is JsonArray array)
            {
                if (!int.TryParse(segment, out int index) || index < 0 || index >= array.Count)
                {
                    return false;
                }

                current = array[index];
                continue;
            }

            return false;
        }

        if (current is null)
        {
            return false;
        }

        node = current;
        return true;
    }

    /// <summary>把路径指向的叶子换成 <paramref name="value"/>（调用方接管该节点的所有权）。</summary>
    public static bool TryReplace(JsonNode? root, string path, JsonNode value)
    {
        if (!TrySegments(path, out string[] segments) || segments.Length == 0)
        {
            return false;
        }

        JsonNode? current = root;
        foreach (string segment in segments[..^1])
        {
            if (current is JsonObject owner)
            {
                if (!owner.TryGetPropertyValue(segment, out current))
                {
                    return false;
                }

                continue;
            }

            if (current is JsonArray array)
            {
                if (!int.TryParse(segment, out int index) || index < 0 || index >= array.Count)
                {
                    return false;
                }

                current = array[index];
                continue;
            }

            return false;
        }

        string last = segments[^1];
        if (current is JsonObject target)
        {
            if (!target.ContainsKey(last))
            {
                return false;
            }

            target[last] = value;
            return true;
        }

        if (current is JsonArray list)
        {
            if (!int.TryParse(last, out int at) || at < 0 || at >= list.Count)
            {
                return false;
            }

            list[at] = value;
            return true;
        }

        return false;
    }

    /// <summary>按路径写占位符；路径不存在或落在对象/数组根上时返回 false。</summary>
    public static bool TryPlaceholderize(JsonNode? root, string path)
    {
        return TryReplace(root, path, JsonValue.Create(Placeholder(path)));
    }

    public static string Escape(string segment)
    {
        return segment.Replace("~", "~0").Replace("/", "~1");
    }

    private static string Unescape(string segment)
    {
        return segment.Replace("~1", "/").Replace("~0", "~");
    }

    private static bool TrySegments(string path, out string[] segments)
    {
        // 空路径只表示「整个对象」，交给 i18n 的 RawField 处理，不参与逐叶子寻址
        segments = [];
        if (path.Length == 0 || !path.StartsWith('/'))
        {
            return false;
        }

        segments = path[1..].Split('/')
            .Select(Unescape)
            .Where(segment => segment.Length > 0)
            .ToArray();
        return segments.Length > 0;
    }
}
