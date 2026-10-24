namespace server.Models.Metadata;

/// <summary>文档根节点的形态，决定装配时怎么把对象拼回去。</summary>
public enum MetadataRootShape
{
    /// <summary>根是数组，一个元素一个对象（绝大多数分类）。</summary>
    Array = 0,

    /// <summary>一个对象一个文件，<c>Avatar/10000002.json</c>。</summary>
    Scattered = 1,
}

/// <summary>
/// <see cref="MetadataKind.GameIdPath"/> 与 <see cref="MetadataKind.SubIdPath"/> 的特殊取值。
/// 其余取值就是对象内的相对路径，用 <c>/</c> 分隔，例如 <c>Ids/0</c>。
/// </summary>
public static class MetadataIdPaths
{
    /// <summary>对象里没有能当身份的数字键，按它在文档里的下标认（实测 <c>GachaEvent</c> 的 <c>Version</c> 只有 53 个不同值对 301 个对象）。</summary>
    public const string IndexPosition = "#index";
}
