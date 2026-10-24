using System.Text.Json.Nodes;
using server.Core.IO;
using server.Models.Metadata;

namespace server.Services.Metadata;

/// <summary>
/// 从分表读模型装配回一个文档的字节。
/// <para>
/// 对外服务不走这条路——那条路只有 <c>metadata_document.Doc</c> 一个来源，这五张表全删了也照样服务。
/// 它是 <see cref="MetadataProjectionService"/> 的反向操作，存在的意义有两条：
/// 逐字节还原是投影唯一的健康检查（装配 == 原文档，投影才没骗人），
/// 以及将来对外 API 想按 locale 现取一份对象集合时的入口。
/// </para>
/// <para>
/// 成员与顺序都取自 <see cref="MetadataObjectLayout"/>，不猜：CHS 不是超集，
/// 各 locale 的数组长度和下标本来就可能不一样。返回 <see langword="null"/> 一律表示
/// 「这份文档不在这套读模型的覆盖范围内」或「投影与骨架对不上」，调用方应当回落到原文档表。
/// </para>
/// </summary>
public sealed class MetadataObjectAssembler(MetadataDbContext metadataDbContext)
{
    /// <summary>查询投影用的行类型，公开的理由同 <see cref="MetadataProjectionService.DocumentRow"/>。</summary>
    public sealed record LayoutRow(ulong GameId, uint SubId);

    public sealed record SkeletonRow(ulong GameId, uint SubId, string Doc);

    public sealed record I18nRow(ulong GameId, uint SubId, string Field, string Value);

    /// <summary>装配一个 locale 的一个文档；<paramref name="name"/> 与 <c>metadata_document.Name</c> 同口径（散文件形如 <c>Avatar/10000002</c>）。</summary>
    public async ValueTask<string?> AssembleAsync(string locale, string name, CancellationToken token)
    {
        MetadataKindRegistry.Definition? definition = MetadataKindRegistry.FindByName(name);
        if (definition is null)
        {
            return null;
        }

        bool projected = await metadataDbContext.MetadataKinds
            .AsNoTracking()
            .AnyAsync(item => item.Kind == definition.Kind, token)
            .ConfigureAwait(false);
        if (!projected)
        {
            return null;
        }

        List<LayoutRow> layout = await metadataDbContext.MetadataObjectLayouts.AsNoTracking()
            .Where(item => item.Locale == locale && item.Name == name)
            .OrderBy(item => item.Position)
            .Select(item => new LayoutRow(item.GameId, item.SubId))
            .ToListAsync(token)
            .ConfigureAwait(false);
        if (layout.Count is 0)
        {
            // 散文件形态下一个文件必有一个对象，空布局只能是没投影过；平铺分类的空数组是合法内容
            return definition.RootShape is MetadataRootShape.Array ? "[]" : null;
        }

        (ulong GameId, uint SubId) only = (layout[0].GameId, layout[0].SubId);

        // 散文件一个身份就是一本，按三元组取；平铺分类整类读回（一类的骨架合计就是它一个 locale 的正文大小）
        IQueryable<MetadataObject> skeletonQuery = metadataDbContext.MetadataObjects.AsNoTracking().Where(item => item.Kind == definition.Kind);
        IQueryable<MetadataObjectI18n> overrideQuery = metadataDbContext.MetadataObjectI18ns.AsNoTracking()
            .Where(item => item.Kind == definition.Kind && item.Locale == locale);
        if (definition.RootShape is MetadataRootShape.Scattered)
        {
            skeletonQuery = skeletonQuery.Where(item => item.GameId == only.GameId && item.SubId == only.SubId);
            overrideQuery = overrideQuery.Where(item => item.GameId == only.GameId && item.SubId == only.SubId);
        }

        List<SkeletonRow> skeletons = await skeletonQuery
            .Select(item => new SkeletonRow(item.GameId, item.SubId, item.Doc))
            .ToListAsync(token)
            .ConfigureAwait(false);
        Dictionary<(ulong GameId, uint SubId), string> docById = skeletons.ToDictionary(item => (item.GameId, item.SubId), item => item.Doc);

        List<I18nRow> overrides = await overrideQuery
            .Select(item => new I18nRow(item.GameId, item.SubId, item.Field, item.Value))
            .ToListAsync(token)
            .ConfigureAwait(false);
        Dictionary<(ulong GameId, uint SubId), List<I18nRow>> byIdentity = overrides
            .GroupBy(item => (item.GameId, item.SubId))
            .ToDictionary(group => group.Key, group => group.ToList());

        JsonArray root = [];
        foreach (LayoutRow row in layout)
        {
            if (!docById.TryGetValue((row.GameId, row.SubId), out string? doc))
            {
                return null;
            }

            byIdentity.TryGetValue((row.GameId, row.SubId), out List<I18nRow>? items);
            string? raw = items?.FirstOrDefault(item => item.Field == MetadataObjectI18n.RawField)?.Value;
            JsonNode? node = JsonNode.Parse(raw ?? doc);
            if (node is null)
            {
                return null;
            }

            // 有整对象兜底行时骨架结构对这个 locale 不适用，其余 locale 逐个把占位符换回该语言的值
            if (raw is null && items is not null)
            {
                foreach (I18nRow item in items)
                {
                    if (item.Field == MetadataObjectI18n.RawField)
                    {
                        continue;
                    }

                    if (!MetadataLeaves.TryReplace(node, item.Field, JsonNode.Parse(item.Value)!))
                    {
                        return null;
                    }
                }
            }

            if (definition.RootShape is MetadataRootShape.Scattered)
            {
                return MetadataHasher.Serialize(node);
            }

            root.Add(node);
        }

        return MetadataHasher.Serialize(root);
    }
}
