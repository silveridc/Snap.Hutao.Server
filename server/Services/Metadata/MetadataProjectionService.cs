using System.Globalization;
using System.Text.Json.Nodes;
using server.Core.IO;
using server.Models.Metadata;

namespace server.Services.Metadata;

/// <summary>
/// 把 <see cref="MetadataDocument"/> 里的原文档投影成分表读模型：
/// <see cref="MetadataObject"/> 骨架 + <see cref="MetadataObjectI18n"/> 随语言变化的叶子 +
/// <see cref="MetadataObjectLayout"/> 每个 locale 的成员与顺序 + <see cref="MetadataObjectRef"/> 引用边 +
/// <see cref="MetadataKind"/> 注册表。
/// <para>
/// 这条链路只生产「能查」的表，不生产「能服务」的字节：对外响应仍然直接吐 <c>metadata_document.Doc</c>。
/// 所以投影写错一行不会让启动器下到坏文件，只会让后台与外部 API 的过滤结果不对。
/// 反过来，<see cref="MetadataObjectAssembler"/> 必须能把原文档逐字节装配回来，
/// 这条不变量由 MetadataTests 的 <c>Assemble_*</c> 用例守着。
/// </para>
/// <para>
/// 一个分类要么整套落库、要么一行不留：中途发现身份不唯一、结构形状不认识就回滚该分类的写入并记 issue，
/// 因为半套投影比没有投影更难查。内存按「一个分类 × 15 个 locale」封顶（实测最大 Material 每 locale 3 MB），
/// 行写入按 4 MB 攒块走参数化 INSERT：1.5 M 行经 EF 逐行 Add 会跑到天亮，
/// 而本机 MySQL 的 <c>local_infile=OFF</c> 让 <c>LOAD DATA LOCAL</c> 用不了。
/// </para>
/// </summary>
public sealed class MetadataProjectionService(MetadataDbContext metadataDbContext, TimeProvider timeProvider)
{
    private const char _separator = '/';

    /// <summary>一条 INSERT 攒到这么多字节的文本就先落库，免得语句超过 <c>max_allowed_packet</c>。</summary>
    private const int _flushAfterBytes = 4 * 1024 * 1024;

    /// <summary>MySQL 单条语句最多 65535 个占位符（错误 1390），留点余量。</summary>
    private const int _maxParametersPerStatement = 60_000;

    /// <summary>回滚一个分类时要清空的表（顺序无关，都是按 <c>Kind</c> 删）。</summary>
    private static readonly string[] KindTables =
    [
        "metadata_kind",
        "metadata_object",
        "metadata_object_i18n",
        "metadata_object_layout",
        "metadata_object_ref",
    ];

    /// <summary>投影过程中发现的问题（身份不唯一、结构形状不认识等）。有 issue 的分类不会留下任何行。</summary>
    public sealed record Issue(string Kind, string Detail);

    public sealed record KindResult(string Kind, uint Objects, ushort Locales, int I18nRows, int LayoutRows, uint RawObjects, long Milliseconds);

    public sealed record Report(List<KindResult> Kinds, List<Issue> Issues, int RefRows)
    {
        public bool Succeeded => Issues.Count is 0;
    }

    /// <summary>查询投影用的行类型（EF 只能物化可访问的类型，所以它们是公开的，不算这层的概念）。</summary>
    public sealed record DocumentRow(string Locale, string Doc);

    public sealed record ObjectRow(ulong GameId, uint SubId, string Doc);

    public sealed record IdentityRow(string Kind, ulong GameId);

    /// <summary>一个对象在某个 locale 的文档里的位置与原文游标（游标指进该 locale 的 <see cref="JsonDocument"/>，所以它必须活着）。</summary>
    private readonly record struct ObjectAt(uint Position, JsonElement Element);

    /// <summary>一个 locale 在一组文档里的对象索引：身份 -> 位置与游标。</summary>
    private sealed class LocaleIndex
    {
        public required string Locale { get; init; }

        public required Dictionary<(ulong GameId, uint SubId), ObjectAt> Objects { get; init; }
    }

    /// <summary>一次分类投影的可变状态。</summary>
    private sealed class KindState
    {
        public required MetadataKindRegistry.Definition Definition { get; init; }

        public required DateTime Now { get; init; }

        public required BulkInserter Objects { get; init; }

        public required BulkInserter I18ns { get; init; }

        public required BulkInserter Layouts { get; init; }

        public List<Issue> Issues { get; } = [];

        /// <summary>已经写过的身份，用来抓散文件之间的重复（两个 <c>Avatar/*.json</c> 里 <c>Id</c> 相同）。</summary>
        public HashSet<(ulong GameId, uint SubId)> Identities { get; } = [];

        public int Locales { get; set; }

        /// <summary>结构在各 locale 间对不齐、退化成整对象存储的对象数。</summary>
        public uint RawObjects { get; set; }

        public bool Failed => Issues.Count > 0;
    }

    /// <summary>
    /// 重建投影。<paramref name="kind"/> 为空表示全部分类。
    /// 引用边在每个分类的骨架落库后就建：它的目标分类可能还没轮到本次重建，
    /// 这时身份集合取不到、按 <see cref="MetadataRefRegistry.Edge.RefKinds"/> 的第一个候选写。
    /// </summary>
    public async ValueTask<Report> RebuildAsync(string? kind, IProgress<string>? progress, CancellationToken token)
    {
        List<KindResult> results = [];
        List<Issue> issues = [];
        List<MetadataKindRegistry.Definition> projected = [];

        foreach (MetadataKindRegistry.Definition definition in MetadataKindRegistry.All)
        {
            if (kind is not null && !string.Equals(definition.Kind, kind, StringComparison.Ordinal))
            {
                continue;
            }

            token.ThrowIfCancellationRequested();
            (KindResult? result, List<Issue> kindIssues) = await ProjectKindAsync(definition, token).ConfigureAwait(false);
            if (kindIssues.Count > 0)
            {
                issues.AddRange(kindIssues);
                progress?.Report($"{definition.Kind}: 投影中止，该分类的读模型已清空 — {kindIssues[0].Detail}");
                continue;
            }

            if (result is null)
            {
                progress?.Report($"{definition.Kind}: 库里没有对应的文档，跳过");
                continue;
            }

            results.Add(result);
            projected.Add(definition);
            progress?.Report($"{definition.Kind}: {result.Objects} objects / {result.Locales} locales, {result.I18nRows} i18n, {result.LayoutRows} layout, {result.RawObjects} raw, {result.Milliseconds} ms");
        }

        int refRows = projected.Count is 0
            ? 0
            : await ProjectRefsAsync(projected, progress, token).ConfigureAwait(false);

        return new Report(results, issues, refRows);
    }

    /// <summary>后台展示用的投影状态，不带身份路径这些注册表内部细节。</summary>
    public sealed record KindStatus(string Kind, string DocumentName, MetadataRootShape RootShape, uint ObjectCount, ushort LocaleCount, uint RawCount, DateTime ProjectedAt, DateTime? StaleSince);

    public async ValueTask<List<KindStatus>> ListKindsAsync(CancellationToken token)
    {
        return await metadataDbContext.MetadataKinds.AsNoTracking()
            .OrderBy(item => item.Kind)
            .Select(item => new KindStatus(item.Kind, item.DocumentName, item.RootShape, item.ObjectCount, item.LocaleCount, item.RawCount, item.ProjectedAt, item.StaleSince))
            .ToListAsync(token)
            .ConfigureAwait(false);
    }

    private async ValueTask<(KindResult? Result, List<Issue> Issues)> ProjectKindAsync(MetadataKindRegistry.Definition definition, CancellationToken token)
    {
        Stopwatch watch = Stopwatch.StartNew();
        List<string> names = await GroupNamesAsync(definition, token).ConfigureAwait(false);
        if (names.Count is 0)
        {
            return (null, []);
        }

        KindState state = new()
        {
            Definition = definition,
            Now = timeProvider.GetUtcNow().UtcDateTime,
            Objects = new BulkInserter(metadataDbContext, "metadata_object", ["`Kind`", "`GameId`", "`SubId`", "`SourceLocale`", "`Ordinal`", "`Doc`", "`UpdatedAt`"]),
            I18ns = new BulkInserter(metadataDbContext, "metadata_object_i18n", ["`Kind`", "`GameId`", "`SubId`", "`Field`", "`Locale`", "`Value`", "`UpdatedAt`"]),
            Layouts = new BulkInserter(metadataDbContext, "metadata_object_layout", ["`Locale`", "`Name`", "`Position`", "`Kind`", "`GameId`", "`SubId`"]),
        };

        await PurgeKindAsync(definition.Kind, token).ConfigureAwait(false);
        foreach (string name in names)
        {
            token.ThrowIfCancellationRequested();
            await ProjectGroupAsync(state, name, token).ConfigureAwait(false);
            if (state.Failed)
            {
                break;
            }
        }

        if (state.Failed)
        {
            // 没攒下的行不写了，已经写下的整批删掉：这个分类要么整套在、要么整套不在
            await PurgeKindAsync(definition.Kind, token).ConfigureAwait(false);
            return (null, state.Issues);
        }

        await state.Objects.FlushAsync(token).ConfigureAwait(false);
        await state.I18ns.FlushAsync(token).ConfigureAwait(false);
        await state.Layouts.FlushAsync(token).ConfigureAwait(false);
        await UpsertKindAsync(definition, (uint)state.Objects.Written, (ushort)state.Locales, state.RawObjects, state.Now, token).ConfigureAwait(false);

        KindResult result = new(
            definition.Kind,
            (uint)state.Objects.Written,
            (ushort)state.Locales,
            state.I18ns.Written,
            state.Layouts.Written,
            state.RawObjects,
            watch.ElapsedMilliseconds);
        return (result, state.Issues);
    }

    /// <summary>
    /// 一组文档 = 一个文档名在所有 locale 上的那几份正文。平铺分类整篇是一组，
    /// 散文件分类一个文件是一组（实测 Avatar 每个 locale 120 个文件，一次全读进来白占上百 MB）。
    /// </summary>
    private async ValueTask ProjectGroupAsync(KindState state, string name, CancellationToken token)
    {
        MetadataKindRegistry.Definition definition = state.Definition;
        List<DocumentRow> rows = await metadataDbContext.MetadataDocuments.AsNoTracking()
            .Where(document => document.Name == name)
            .Select(document => new DocumentRow(document.Locale, document.Doc))
            .ToListAsync(token)
            .ConfigureAwait(false);
        if (rows.Count is 0)
        {
            return;
        }

        string[] locales = [.. OrderedLocales(rows.Select(row => row.Locale))];
        state.Locales = Math.Max(state.Locales, locales.Length);

        Dictionary<string, string> docs = [];
        foreach (DocumentRow row in rows)
        {
            docs[row.Locale] = row.Doc;
        }

        List<JsonDocument> parsed = [];
        try
        {
            List<LocaleIndex> indexes = [];
            foreach (string locale in locales)
            {
                JsonDocument document = JsonDocument.Parse(docs[locale]);
                parsed.Add(document);
                Dictionary<(ulong GameId, uint SubId), ObjectAt> objects = [];
                foreach ((JsonElement element, uint position) in EnumerateObjects(definition, document.RootElement))
                {
                    if (!TryIdentity(definition, element, position, out ulong gameId, out uint subId))
                    {
                        state.Issues.Add(new Issue(definition.Kind, $"{name}/{locale} position {position} has no usable id on '{definition.GameIdPath}'"));
                        return;
                    }

                    if (!objects.TryAdd((gameId, subId), new ObjectAt(position, element)))
                    {
                        state.Issues.Add(new Issue(definition.Kind, $"{name}/{locale} identity {gameId}/{subId} repeats within one locale, the registry is wrong"));
                        return;
                    }
                }

                indexes.Add(new LocaleIndex { Locale = locale, Objects = objects });
            }

            // 正文已经进了 JsonDocument 自己的缓冲区，字符串可以早一步放手（Material 一组就 45 MB）
            docs.Clear();

            List<(ulong GameId, uint SubId)> identities = [];
            foreach (LocaleIndex index in indexes)
            {
                foreach ((ulong GameId, uint SubId) identity in index.Objects.Keys)
                {
                    if (state.Identities.Add(identity))
                    {
                        identities.Add(identity);
                    }
                }
            }

            ObjectAt[] found = new ObjectAt[indexes.Count];
            bool[] present = new bool[indexes.Count];
            foreach ((ulong GameId, uint SubId) identity in identities)
            {
                for (int index = 0; index < indexes.Count; index++)
                {
                    present[index] = indexes[index].Objects.TryGetValue(identity, out ObjectAt at);
                    if (present[index])
                    {
                        found[index] = at;
                    }
                }

                await ProjectObjectAsync(state, name, locales, identity.GameId, identity.SubId, found, present, token).ConfigureAwait(false);
                if (state.Failed)
                {
                    return;
                }
            }
        }
        finally
        {
            foreach (JsonDocument document in parsed)
            {
                document.Dispose();
            }
        }
    }

    /// <summary>
    /// 一个对象的跨 locale 归并：以第一个见到它的 locale（CHS 优先）为骨架来源，
    /// 路径与形状都对不齐的 locale 整个对象退化成兜底行，只把真正随语言变的叶子抽进 i18n 表。
    /// </summary>
    private static async ValueTask ProjectObjectAsync(
        KindState state,
        string name,
        string[] locales,
        ulong gameId,
        uint subId,
        ObjectAt[] found,
        bool[] present,
        CancellationToken token)
    {
        string kind = state.Definition.Kind;
        int count = locales.Length;
        List<(string Path, string Shape, string Text)>[] leaves = new List<(string Path, string Shape, string Text)>[count];
        int source = -1;
        for (int index = 0; index < count; index++)
        {
            if (!present[index])
            {
                continue;
            }

            if (source < 0)
            {
                source = index;
            }

            List<(string Path, string Shape, string Text)> into = [];
            MetadataLeaves.Collect(found[index].Element, into);
            leaves[index] = into;
        }

        List<(string Path, string Shape, string Text)> sourceLeaves = leaves[source];
        int total = sourceLeaves.Count;
        bool[] congruent = [.. Enumerable.Repeat(true, count)];
        bool[] varying = new bool[total];
        bool fallback = false;
        for (int index = 0; index < count; index++)
        {
            if (index == source || !present[index])
            {
                continue;
            }

            List<(string Path, string Shape, string Text)> other = leaves[index];
            if (!SameStructure(sourceLeaves, other))
            {
                congruent[index] = false;
                fallback = true;
                continue;
            }

            for (int leaf = 0; leaf < total; leaf++)
            {
                if (!string.Equals(sourceLeaves[leaf].Text, other[leaf].Text, StringComparison.Ordinal))
                {
                    varying[leaf] = true;
                }
            }
        }

        JsonNode skeleton = JsonNode.Parse(found[source].Element.GetRawText())!;
        for (int leaf = 0; leaf < total; leaf++)
        {
            if (varying[leaf] && !MetadataLeaves.TryPlaceholderize(skeleton, sourceLeaves[leaf].Path))
            {
                state.Issues.Add(new Issue(kind, $"cannot placeholder '{sourceLeaves[leaf].Path}' on {gameId}/{subId}"));
                return;
            }
        }

        if (fallback)
        {
            state.RawObjects++;
        }

        await state.Objects.AddAsync(
            token,
            kind,
            (long)gameId,
            (long)subId,
            locales[source],
            (long)found[source].Position,
            MetadataHasher.Serialize(skeleton),
            state.Now)
            .ConfigureAwait(false);

        for (int index = 0; index < count; index++)
        {
            if (!present[index])
            {
                continue;
            }

            await state.Layouts.AddAsync(token, locales[index], name, (long)found[index].Position, kind, (long)gameId, (long)subId).ConfigureAwait(false);
            if (!congruent[index])
            {
                await state.I18ns.AddAsync(
                    token,
                    kind,
                    (long)gameId,
                    (long)subId,
                    MetadataObjectI18n.RawField,
                    locales[index],
                    found[index].Element.GetRawText(),
                    state.Now)
                    .ConfigureAwait(false);
                continue;
            }

            List<(string Path, string Shape, string Text)> other = leaves[index];
            for (int leaf = 0; leaf < total; leaf++)
            {
                if (varying[leaf])
                {
                    await state.I18ns.AddAsync(token, kind, (long)gameId, (long)subId, other[leaf].Path, locales[index], other[leaf].Text, state.Now).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>叶子序列（路径 + 形状）逐个相同才算同一种结构，文本不参与比较——文本相同是常态，不同才是要抽出去的东西。</summary>
    private static bool SameStructure(List<(string Path, string Shape, string Text)> left, List<(string Path, string Shape, string Text)> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int index = 0; index < left.Count; index++)
        {
            if (!string.Equals(left[index].Path, right[index].Path, StringComparison.Ordinal)
                || !string.Equals(left[index].Shape, right[index].Shape, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>第二阶段：从已落库的骨架里取引用值，目标分类用身份全集校正。</summary>
    private async ValueTask<int> ProjectRefsAsync(List<MetadataKindRegistry.Definition> definitions, IProgress<string>? progress, CancellationToken token)
    {
        Dictionary<string, HashSet<ulong>> identities = await LoadIdentitiesAsync(token).ConfigureAwait(false);
        BulkInserter refs = new(metadataDbContext, "metadata_object_ref", ["`Kind`", "`GameId`", "`SubId`", "`Role`", "`Ordinal`", "`RefKind`", "`RefId`"]);
        int total = 0;

        foreach (MetadataKindRegistry.Definition definition in definitions)
        {
            IReadOnlyList<MetadataRefRegistry.Edge> edges = MetadataRefRegistry.For(definition.Kind);
            if (edges.Count is 0)
            {
                continue;
            }

            token.ThrowIfCancellationRequested();
            List<ObjectRow> rows = await metadataDbContext.MetadataObjects.AsNoTracking()
                .Where(item => item.Kind == definition.Kind)
                .Select(item => new ObjectRow(item.GameId, item.SubId, item.Doc))
                .ToListAsync(token)
                .ConfigureAwait(false);

            int count = 0;
            foreach (ObjectRow row in rows)
            {
                using JsonDocument document = JsonDocument.Parse(row.Doc);
                foreach (MetadataRefRegistry.Edge edge in edges)
                {
                    foreach ((uint ordinal, ulong value) in Match(document.RootElement, edge.Path))
                    {
                        if (value is MetadataRefRegistry.NoRef)
                        {
                            continue;
                        }

                        string refKind = edge.RefKinds.FirstOrDefault(candidate => identities.TryGetValue(candidate, out HashSet<ulong>? set) && set.Contains(value))
                            ?? edge.RefKinds[0];
                        await refs.AddAsync(token, definition.Kind, (long)row.GameId, (long)row.SubId, edge.Role, (long)ordinal, refKind, (long)value).ConfigureAwait(false);
                        count++;
                    }
                }
            }

            await refs.FlushAsync(token).ConfigureAwait(false);
            total += count;
            progress?.Report($"{definition.Kind}: {count} refs");
        }

        return total;
    }

    private static IEnumerable<string> OrderedLocales(IEnumerable<string> locales)
    {
        // CHS 永远当骨架来源：它是上游的原生产物，其余语言都是它的派生
        return locales.OrderByDescending(locale => locale == "CHS").ThenBy(locale => locale, StringComparer.Ordinal);
    }

    private static IEnumerable<(JsonElement Element, uint Position)> EnumerateObjects(MetadataKindRegistry.Definition definition, JsonElement root)
    {
        if (definition.RootShape is MetadataRootShape.Scattered)
        {
            // 散文件形态下一个文件就是一个对象，位置恒为 0
            return [(root, 0u)];
        }

        if (root.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        List<(JsonElement Element, uint Position)> objects = [];
        uint position = 0;
        foreach (JsonElement element in root.EnumerateArray())
        {
            objects.Add((element, position++));
        }

        return objects;
    }

    private static bool TryIdentity(MetadataKindRegistry.Definition definition, JsonElement element, uint position, out ulong gameId, out uint subId)
    {
        subId = 0;
        if (definition.GameIdPath == MetadataIdPaths.IndexPosition)
        {
            gameId = position;
            return true;
        }

        if (!TryReadNumber(element, definition.GameIdPath, out gameId))
        {
            return false;
        }

        if (definition.SubIdPath.Length is 0)
        {
            return true;
        }

        if (!TryReadNumber(element, definition.SubIdPath, out ulong second))
        {
            return false;
        }

        subId = (uint)second;
        return true;
    }

    /// <summary>
    /// 按路径读一个数值身份。段既可以是对象键也可以是数组下标——<c>Reliquary</c> 的身份是 <c>Ids/0</c>，
    /// 那一类文档的一个对象带 5 个等级 Id，只有第一个是全类唯一的。
    /// </summary>
    private static bool TryReadNumber(JsonElement element, string path, out ulong value)
    {
        value = 0;
        JsonElement current = element;
        foreach (string segment in path.Split(_separator))
        {
            if (current.ValueKind is JsonValueKind.Array)
            {
                if (!int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out int index) || index < 0 || index >= current.GetArrayLength())
                {
                    return false;
                }

                current = current[index];
                continue;
            }

            if (current.ValueKind is not JsonValueKind.Object || !current.TryGetProperty(segment, out JsonElement child))
            {
                return false;
            }

            current = child;
        }

        return current.ValueKind is JsonValueKind.Number && current.TryGetUInt64(out value);
    }

    /// <summary>按 <c>RewardList/*/Id</c> 这样的模式取数字叶子；<c>*</c> 是那层的数组下标，也是 <c>Ordinal</c>。骨架里的占位符是字符串，天然取不到数。</summary>
    private static List<(uint Ordinal, ulong Value)> Match(JsonElement root, string pattern)
    {
        string[] segments = pattern.Split(_separator);
        List<(uint Ordinal, ulong Value)> found = [];
        Walk(root, 0, 0);
        return found;

        void Walk(JsonElement current, int at, uint ordinal)
        {
            bool last = at == segments.Length - 1;
            if (segments[at] == "*")
            {
                if (current.ValueKind is not JsonValueKind.Array)
                {
                    return;
                }

                uint index = 0;
                foreach (JsonElement item in current.EnumerateArray())
                {
                    if (last)
                    {
                        if (TryNumber(item, out ulong value))
                        {
                            found.Add((index, value));
                        }
                    }
                    else
                    {
                        Walk(item, at + 1, index);
                    }

                    index++;
                }

                return;
            }

            if (current.ValueKind is not JsonValueKind.Object || !current.TryGetProperty(segments[at], out JsonElement child))
            {
                return;
            }

            if (last)
            {
                if (TryNumber(child, out ulong value))
                {
                    found.Add((ordinal, value));
                }

                return;
            }

            Walk(child, at + 1, ordinal);
        }
    }

    private static bool TryNumber(JsonElement element, out ulong value)
    {
        value = 0;
        return element.ValueKind is JsonValueKind.Number && element.TryGetUInt64(out value);
    }

    private async ValueTask<List<string>> GroupNamesAsync(MetadataKindRegistry.Definition definition, CancellationToken token)
    {
        if (definition.RootShape is MetadataRootShape.Array)
        {
            return [definition.DocumentName];
        }

        string prefix = $"{definition.DocumentName}{_separator}";
        return await metadataDbContext.MetadataDocuments.AsNoTracking()
            .Where(document => document.Name.StartsWith(prefix))
            .Select(document => document.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(token)
            .ConfigureAwait(false);
    }

    private async ValueTask<Dictionary<string, HashSet<ulong>>> LoadIdentitiesAsync(CancellationToken token)
    {
        List<IdentityRow> rows = await metadataDbContext.MetadataObjects.AsNoTracking()
            .Select(item => new IdentityRow(item.Kind, item.GameId))
            .ToListAsync(token)
            .ConfigureAwait(false);

        Dictionary<string, HashSet<ulong>> identities = [];
        foreach (IdentityRow row in rows)
        {
            if (!identities.TryGetValue(row.Kind, out HashSet<ulong>? set))
            {
                set = [];
                identities[row.Kind] = set;
            }

            set.Add(row.GameId);
        }

        return identities;
    }

    private async ValueTask PurgeKindAsync(string kind, CancellationToken token)
    {
        foreach (string table in KindTables)
        {
            // 表名取自上面的静态数组，不是外部输入；先拼成普通字符串再交给 EF，避免把插值直接当 SQL 传
            string sql = $"DELETE FROM `{table}` WHERE `Kind` = {{0}}";
            await metadataDbContext.Database.ExecuteSqlRawAsync(sql, [kind], token).ConfigureAwait(false);
        }
    }

    private async ValueTask UpsertKindAsync(MetadataKindRegistry.Definition definition, uint objects, ushort locales, uint rawObjects, DateTime now, CancellationToken token)
    {
        MetadataKind? row = await metadataDbContext.MetadataKinds
            .FirstOrDefaultAsync(item => item.Kind == definition.Kind, token)
            .ConfigureAwait(false);
        if (row is null)
        {
            await metadataDbContext.MetadataKinds.AddAsync(
                new MetadataKind
                {
                    Kind = definition.Kind,
                    DocumentName = definition.DocumentName,
                    RootShape = definition.RootShape,
                    GameIdPath = definition.GameIdPath,
                    SubIdPath = definition.SubIdPath,
                    ObjectCount = objects,
                    LocaleCount = locales,
                    RawCount = rawObjects,
                    ProjectedAt = now,
                },
                token)
                .ConfigureAwait(false);
        }
        else
        {
            row.DocumentName = definition.DocumentName;
            row.RootShape = definition.RootShape;
            row.GameIdPath = definition.GameIdPath;
            row.SubIdPath = definition.SubIdPath;
            row.ObjectCount = objects;
            row.LocaleCount = locales;
            row.RawCount = rawObjects;
            row.ProjectedAt = now;
            row.StaleSince = null;
        }

        await metadataDbContext.SaveChangesAsync(token).ConfigureAwait(false);
    }

    /// <summary>
    /// 分块参数化 INSERT。值一律走参数，不拼进 SQL；
    /// 语句里的 <c>{n}</c> 是 EF 的位置占位符，它按顺序换成 <c>@pn</c> 并绑定第 n 个值。
    /// </summary>
    private sealed class BulkInserter(MetadataDbContext metadataDbContext, string table, string[] columns)
    {
        private readonly StringBuilder _statement = new();
        private readonly List<object> _values = [];
        private readonly string _header = $"INSERT INTO `{table}` ({string.Join(',', columns)}) VALUES ";

        private int _rows;
        private int _pendingBytes;

        public int Written { get; private set; }

        public async ValueTask AddAsync(CancellationToken token, params object[] row)
        {
            if (_rows > 0 && (_pendingBytes >= _flushAfterBytes || _values.Count + columns.Length > _maxParametersPerStatement))
            {
                await FlushAsync(token).ConfigureAwait(false);
            }

            _statement.Append(_rows is 0 ? _header : ",");
            _statement.Append('(');
            for (int column = 0; column < columns.Length; column++)
            {
                if (column > 0)
                {
                    _statement.Append(',');
                }

                _statement.Append('{').Append(_values.Count + column).Append('}');
            }

            _statement.Append(')');
            _values.AddRange(row);
            _pendingBytes += row.Sum(value => value is string text ? text.Length : 32);
            _rows++;
        }

        public async ValueTask FlushAsync(CancellationToken token)
        {
            if (_rows is 0)
            {
                return;
            }

            Written += _rows;
            await metadataDbContext.Database.ExecuteSqlRawAsync(_statement.ToString(), _values, token).ConfigureAwait(false);
            _statement.Clear();
            _values.Clear();
            _rows = 0;
            _pendingBytes = 0;
        }
    }
}
