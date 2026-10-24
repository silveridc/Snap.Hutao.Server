using System.Text.Json.Nodes;
using server.Core.IO;
using server.Models.Metadata;

namespace server.Services.Metadata;

/// <summary>
/// metadata 的 i18n 覆盖层。只有人工改过的字符串叶子才会占一行，
/// 每次写覆盖都顺手把 <see cref="MetadataDocument.Doc"/> 重算落库，
/// 所以对外接口永远只需要读 <c>Doc</c> 这一列，不用在请求线程上重新序列化 5 MB 的文档。
/// </summary>
public sealed class MetadataTranslationService(MetadataDbContext metadataDbContext, MetadataStoreService metadataStoreService, TimeProvider timeProvider)
{

    public sealed record Row(string Locale, string Name, string Pointer, string Value, string OriginalValue, DateTime UpdatedAt);

    /// <param name="Pointer">JSON Pointer（RFC 6901）。</param>
    /// <param name="BaselineValue">对照 locale 上同一指针的值，该侧不是字符串时为 <see langword="null"/>。</param>
    /// <param name="LocalValue">本 locale 上的值。</param>
    /// <param name="Differs">两侧都有值且不相等，即已经翻译过。</param>
    public sealed record Leaf(string Pointer, string? BaselineValue, string? LocalValue, bool Differs);

    public sealed record Outcome(bool Succeeded, string? Hash, string? Error)
    {
        public static Outcome Ok(string hash)
        {
            return new(true, hash, null);
        }

        public static Outcome Fail(string error)
        {
            return new(false, null, error);
        }
    }

    /// <param name="RewrittenDocuments">重新贴过覆盖的文档数。</param>
    /// <param name="SkippedPointers">在新版本文档里已经找不到、因而没能贴回去的指针。</param>
    public sealed record ReapplyResult(int RewrittenDocuments, List<string> SkippedPointers);

    public async ValueTask<List<Row>> ListAsync(string? locale, string? name, CancellationToken token)
    {
        IQueryable<MetadataTranslation> query = metadataDbContext.MetadataTranslations.AsNoTracking();
        if (locale is not null)
        {
            query = query.Where(translation => translation.Locale == locale);
        }

        if (name is not null)
        {
            query = query.Where(translation => translation.Name == name);
        }

        List<Row> rows = await query
            .OrderBy(translation => translation.Locale)
            .ThenBy(translation => translation.Name)
            .ThenBy(translation => translation.Pointer)
            .Select(translation => new Row(translation.Locale, translation.Name, translation.Pointer, translation.Value, translation.OriginalValue, translation.UpdatedAt))
            .ToListAsync(token)
            .ConfigureAwait(false);
        return rows;
    }

    /// <summary>写下（或改掉）一条覆盖，并把译文贴进文档本体。第一次覆盖时记住原文，删除时据此还原。</summary>
    public async ValueTask<Outcome> SetAsync(string locale, string name, string pointer, string value, CancellationToken token)
    {
        string? invalid = Validate(locale, name, pointer);
        if (invalid is not null)
        {
            return Outcome.Fail(invalid);
        }

        WriteResult written = await WriteAsync(locale, name, pointer, value, token).ConfigureAwait(false);
        if (written.Error is not null)
        {
            return Outcome.Fail(written.Error);
        }

        MetadataTranslation? row = await metadataDbContext.MetadataTranslations
            .FirstOrDefaultAsync(translation => translation.Locale == locale && translation.Name == name && translation.Pointer == pointer, token)
            .ConfigureAwait(false);

        if (row is null)
        {
            await metadataDbContext.MetadataTranslations.AddAsync(
                new MetadataTranslation
                {
                    Locale = locale,
                    Name = name,
                    Pointer = pointer,
                    Value = value,
                    OriginalValue = written.Previous ?? string.Empty,
                    UpdatedAt = timeProvider.GetUtcNow().UtcDateTime,
                },
                token)
                .ConfigureAwait(false);
        }
        else
        {
            row.Value = value;
            row.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        }

        await metadataDbContext.SaveChangesAsync(token).ConfigureAwait(false);
        return Outcome.Ok(written.Hash!);
    }

    /// <summary>删掉一条覆盖，并把文档上的那个叶子还原成原文。</summary>
    public async ValueTask<Outcome> RemoveAsync(string locale, string name, string pointer, CancellationToken token)
    {
        string? invalid = Validate(locale, name, pointer);
        if (invalid is not null)
        {
            return Outcome.Fail(invalid);
        }

        MetadataTranslation? row = await metadataDbContext.MetadataTranslations
            .FirstOrDefaultAsync(translation => translation.Locale == locale && translation.Name == name && translation.Pointer == pointer, token)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Outcome.Fail($"No translation override on {locale}/{name}{pointer}");
        }

        string restore = row.OriginalValue;
        WriteResult written = await WriteAsync(locale, name, pointer, restore, token).ConfigureAwait(false);
        if (written.Error is not null)
        {
            return Outcome.Fail(written.Error);
        }

        metadataDbContext.MetadataTranslations.Remove(row);
        await metadataDbContext.SaveChangesAsync(token).ConfigureAwait(false);
        return Outcome.Ok(written.Hash!);
    }

    /// <summary>重新导入整棵树之后调用：以库里的原文档为底，把该 locale 的所有覆盖重新贴回去。</summary>
    public async ValueTask<ReapplyResult> ReapplyAsync(string locale, CancellationToken token)
    {
        List<Row> overrides = await ListAsync(locale, null, token).ConfigureAwait(false);
        List<string> skipped = [];
        int rewritten = 0;

        foreach (IGrouping<string, Row> group in overrides.GroupBy(row => row.Name))
        {
            MetadataDocument? document = await metadataStoreService.FindAsync(locale, group.Key, token).ConfigureAwait(false);
            if (document is null)
            {
                continue;
            }

            if (JsonNode.Parse(document.Doc) is not { } root)
            {
                continue;
            }

            foreach (Row item in group)
            {
                if (TryWritePointer(root, item.Pointer, item.Value, out _, out _))
                {
                    continue;
                }

                // 上游改了结构，这条覆盖在新文档里已经没有落点，跳过而不是让整个 locale 的重贴失败
                skipped.Add($"{group.Key}{item.Pointer}");
            }

            await metadataStoreService.UpsertAsync(locale, group.Key, MetadataHasher.Serialize(root), token).ConfigureAwait(false);
            rewritten++;
        }

        if (rewritten > 0)
        {
            await metadataStoreService.RebuildMetaAsync(locale, token).ConfigureAwait(false);
        }

        return new ReapplyResult(rewritten, skipped);
    }

    /// <summary>
    /// 把一个文档里所有字符串叶子按指针摊开并与对照 locale 比较，供人工挑要翻译的词条。
    /// 两个文档任一不存在时返回 <see langword="null"/>，好让接口区分「没导过」和「没有词条」。
    /// </summary>
    public async ValueTask<List<Leaf>?> LeavesAsync(string name, string locale, string baseline, CancellationToken token)
    {
        MetadataDocument? current = await metadataStoreService.FindAsync(locale, name, token).ConfigureAwait(false);
        MetadataDocument? source = await metadataStoreService.FindAsync(baseline, name, token).ConfigureAwait(false);
        if (current is null || source is null)
        {
            return null;
        }

        Dictionary<string, string> localLeaves = [];
        Walk(JsonNode.Parse(current.Doc), string.Empty, localLeaves);
        Dictionary<string, string> baselineLeaves = [];
        Walk(JsonNode.Parse(source.Doc), string.Empty, baselineLeaves);

        List<Leaf> leaves = [];
        foreach (KeyValuePair<string, string> pair in localLeaves)
        {
            baselineLeaves.TryGetValue(pair.Key, out string? counterpart);
            leaves.Add(new Leaf(pair.Key, counterpart, pair.Value, counterpart is not null && counterpart != pair.Value));
        }

        return leaves;
    }

    private sealed record WriteResult(string? Hash, string? Previous, string? Error);

    /// <summary>读出文档、把译文写到指针处、序列化落库，并顺带重算该 locale 的 Meta。</summary>
    private async ValueTask<WriteResult> WriteAsync(string locale, string name, string pointer, string value, CancellationToken token)
    {
        MetadataDocument? document = await metadataStoreService.FindAsync(locale, name, token).ConfigureAwait(false);
        if (document is null)
        {
            return new WriteResult(null, null, $"Document {locale}/{name} is not imported");
        }

        if (JsonNode.Parse(document.Doc) is not { } root)
        {
            return new WriteResult(null, null, $"Document {locale}/{name} is not JSON");
        }

        if (!TryWritePointer(root, pointer, value, out string previous, out string? error))
        {
            return new WriteResult(null, null, error!);
        }

        string hash = await metadataStoreService.UpsertAsync(locale, name, MetadataHasher.Serialize(root), token).ConfigureAwait(false);
        await metadataStoreService.RebuildMetaAsync(locale, token).ConfigureAwait(false);
        return new WriteResult(hash, previous, null);
    }

    private static bool TryWritePointer(JsonNode root, string pointer, string value, out string previous, out string? error)
    {
        error = null;
        if (!TrySplit(pointer, out string[] tokens, out error))
        {
            previous = string.Empty;
            return false;
        }

        return TryReplaceLeaf(root, tokens, value, out previous, out error);
    }

    private static string? Validate(string locale, string name, string pointer)
    {
        if (locale.Length is 0 or > 8)
        {
            return $"Invalid locale length: {locale.Length}";
        }

        if (name.Length is 0 or > 128)
        {
            return $"Invalid name length: {name.Length}";
        }

        if (pointer.Length is 0 or > 128)
        {
            return $"Invalid pointer length: {pointer.Length}";
        }

        return null;
    }

    private static bool TrySplit(string pointer, out string[] tokens, out string? error)
    {
        tokens = [];
        error = null;
        if (!pointer.StartsWith('/'))
        {
            error = $"Pointer must start with '/': {pointer}";
            return false;
        }

        tokens = pointer[1..].Split('/')
            .Select(segment => segment.Replace("~1", "/").Replace("~0", "~"))
            .ToArray();
        if (tokens.Any(token => token.Length is 0))
        {
            error = $"Pointer has an empty segment: {pointer}";
            return false;
        }

        return true;
    }

    /// <summary>把指针指向的字符串叶子改成 <paramref name="value"/>，并返回改前的值。</summary>
    private static bool TryReplaceLeaf(JsonNode root, string[] tokens, string value, out string previous, out string? error)
    {
        previous = string.Empty;
        error = null;
        JsonNode? parent = root;

        for (int index = 0; index < tokens.Length - 1; index++)
        {
            string segment = tokens[index];
            if (parent is JsonObject owner)
            {
                if (!owner.TryGetPropertyValue(segment, out parent))
                {
                    error = $"Pointer segment '{segment}' does not exist";
                    return false;
                }

                continue;
            }

            if (parent is JsonArray array)
            {
                if (!int.TryParse(segment, out int at) || at < 0 || at >= array.Count)
                {
                    error = $"Pointer segment '{segment}' is not an index of a {array.Count}-item array";
                    return false;
                }

                parent = array[at];
                continue;
            }

            error = $"Pointer segment '{segment}' points into a value, not a container";
            return false;
        }

        string last = tokens[^1];
        if (parent is JsonObject target)
        {
            if (!target.TryGetPropertyValue(last, out JsonNode? leaf))
            {
                error = $"Pointer leaf '{last}' does not exist";
                return false;
            }

            if (!Assign(leaf, value, last, out previous, ref error))
            {
                return false;
            }

            target[last] = JsonValue.Create(value);
            return true;
        }

        if (parent is JsonArray list)
        {
            if (!int.TryParse(last, out int index) || index < 0 || index >= list.Count)
            {
                error = $"Pointer leaf '{last}' is not an index of a {list.Count}-item array";
                return false;
            }

            if (!Assign(list[index], value, last, out previous, ref error))
            {
                return false;
            }

            list[index] = JsonValue.Create(value);
            return true;
        }

        error = "Pointer leaf is inside a value, not a container";
        return false;

        static bool Assign(JsonNode? leaf, string value, string name, out string previous, ref string? error)
        {
            previous = string.Empty;
            if (leaf?.GetValueKind() is not JsonValueKind.String)
            {
                error = $"Pointer target '{name}' is not a string, i18n only rewrites string leaves";
                return false;
            }

            previous = leaf.GetValue<string>();
            return true;
        }
    }

    private static void Walk(JsonNode? node, string prefix, Dictionary<string, string> into)
    {
        switch (node)
        {
            case null:
                {
                    break;
                }

            case JsonObject owner:
                {
                    foreach (KeyValuePair<string, JsonNode?> pair in owner)
                    {
                        Walk(pair.Value, $"{prefix}/{Escape(pair.Key)}", into);
                    }

                    break;
                }

            case JsonArray array:
                {
                    for (int index = 0; index < array.Count; index++)
                    {
                        Walk(array[index], $"{prefix}/{index}", into);
                    }

                    break;
                }

            default:
                {
                    if (node.GetValueKind() is JsonValueKind.String)
                    {
                        into[prefix] = node.GetValue<string>();
                    }

                    break;
                }
        }
    }

    private static string Escape(string segment)
    {
        return segment.Replace("~", "~0").Replace("/", "~1");
    }
}
