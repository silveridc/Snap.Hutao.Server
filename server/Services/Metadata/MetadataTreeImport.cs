using server.Core.IO;

namespace server.Services.Metadata;

/// <summary>
/// 把 <c>static/metadata/Genshin</c> 那棵目录树搬进 <c>metadata_document</c> 表。
/// 分成「先全量校验、再写库」两步：只要有任何一个文档跟 <c>Meta.json</c> 记录的哈希对不上，
/// 就一个字节都不写。半套坏数据推上线的后果是启动器每次启动都重下、然后卡在反序列化失败上，
/// 比导入失败难查得多。
/// </summary>
public static class MetadataTreeImport
{
    /// <summary>清单文档，内容是由各文档哈希组成的对象，本身不参与哈希门禁。</summary>
    public const string ManifestName = "Meta";

    private const string _extension = ".json";

    public sealed record Issue(string Locale, string Name, string Reason);

    public sealed record Report(int Locales, int Documents, long Bytes, List<Issue> Issues)
    {
        public bool Succeeded => Issues.Count is 0;
    }

    /// <summary>逐个文档按启动器同一口径算哈希，与 <c>Meta.json</c> 比对。</summary>
    public static async ValueTask<Report> VerifyAsync(string root, string? locale, CancellationToken token)
    {
        List<Issue> issues = [];
        int locales = 0;
        int documents = 0;
        long bytes = 0;

        foreach (string localeDirectory in LocaleDirectories(root, locale))
        {
            string localeName = Path.GetFileName(localeDirectory);
            locales++;
            token.ThrowIfCancellationRequested();

            Dictionary<string, string> manifest = [];
            string manifestPath = Path.Combine(localeDirectory, $"{ManifestName}{_extension}");
            if (!File.Exists(manifestPath))
            {
                issues.Add(new Issue(localeName, ManifestName, "manifest file is missing"));
                continue;
            }

            foreach ((string name, string path) in DocumentFiles(localeDirectory))
            {
                token.ThrowIfCancellationRequested();
                string text = MetadataHasher.NormalizeLineEndings(await File.ReadAllTextAsync(path, token).ConfigureAwait(false));
                bytes += Encoding.UTF8.GetByteCount(text);
                documents++;

                if (!IsWellFormedJson(text))
                {
                    issues.Add(new Issue(localeName, name, "document is not well formed JSON"));
                    continue;
                }

                if (name == ManifestName)
                {
                    foreach (KeyValuePair<string, string> entry in ReadManifest(text))
                    {
                        manifest[entry.Key] = entry.Value;
                    }

                    continue;
                }

                if (!manifest.TryGetValue(name, out string? expected))
                {
                    issues.Add(new Issue(localeName, name, "document is not listed in the manifest"));
                    continue;
                }

                string actual = MetadataHasher.Hash(text);
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new Issue(localeName, name, $"manifest says {expected} but the bytes hash to {actual}"));
                }
            }

            // 清单里有、磁盘上没有的条目
            foreach (KeyValuePair<string, string> entry in manifest)
            {
                if (entry.Key != ManifestName && !File.Exists(Path.Combine(localeDirectory, entry.Key.Replace('/', Path.DirectorySeparatorChar) + _extension)))
                {
                    issues.Add(new Issue(localeName, entry.Key, "listed in the manifest but the file is missing"));
                }
            }
        }

        return new Report(locales, documents, bytes, issues);
    }

    /// <summary>
    /// 写库：每个文档一行，落库前同样先去 CR，落库后按库里的哈希重建该 locale 的 <c>Meta.json</c>。
    /// 调用方必须先跑 <see cref="VerifyAsync"/> 并确认成功。
    /// </summary>
    /// <param name="progress">每个 locale 导完汇报一次，344 MB 的树跑几分钟，没有进度看起来像卡死。</param>
    public static async ValueTask<Report> ImportAsync(MetadataDbContext metadataDbContext, string root, string? locale, IProgress<string>? progress, CancellationToken token)
    {
        MetadataStoreService store = new(metadataDbContext, TimeProvider.System);
        List<Issue> issues = [];
        int locales = 0;
        int documents = 0;
        long bytes = 0;

        foreach (string localeDirectory in LocaleDirectories(root, locale))
        {
            string localeName = Path.GetFileName(localeDirectory);
            locales++;
            int localeDocuments = 0;
            long localeBytes = 0;
            Stopwatch watch = Stopwatch.StartNew();

            foreach ((string name, string path) in DocumentFiles(localeDirectory))
            {
                if (name == ManifestName)
                {
                    continue;
                }

                token.ThrowIfCancellationRequested();
                string text = MetadataHasher.NormalizeLineEndings(await File.ReadAllTextAsync(path, token).ConfigureAwait(false));
                await store.UpsertAsync(localeName, name, text, token).ConfigureAwait(false);
                localeBytes += Encoding.UTF8.GetByteCount(text);
                localeDocuments++;
            }

            await store.RebuildMetaAsync(localeName, token).ConfigureAwait(false);
            documents += localeDocuments;
            bytes += localeBytes;
            progress?.Report($"{localeName}: {localeDocuments} documents, {localeBytes / 1024 / 1024} MiB, {watch.ElapsedMilliseconds} ms");
        }

        return new Report(locales, documents, bytes, issues);
    }

    private static IEnumerable<string> LocaleDirectories(string root, string? locale)
    {
        return Directory.EnumerateDirectories(root)
            .Where(directory => locale is null || string.Equals(Path.GetFileName(directory), locale, StringComparison.OrdinalIgnoreCase))
            .OrderBy(directory => directory, StringComparer.Ordinal);
    }

    /// <summary>树里的相对路径就是文档名（用 <c>/</c> 分隔，去掉扩展名），例如 <c>Avatar/10000002</c>。</summary>
    private static IEnumerable<(string Name, string Path)> DocumentFiles(string localeDirectory)
    {
        return Directory.EnumerateFiles(localeDirectory, $"*{_extension}", SearchOption.AllDirectories)
            .Select(file => (Name: RelativeName(localeDirectory, file), Path: file))
            .Where(document => document.Name.Length > 0)

            // 清单必须最先读，后面的文档都靠它比对；其余照上游顺序：平铺文档在前，Avatar/* 散文件在后
            .OrderBy(document => document.Name != ManifestName)
            .ThenBy(document => document.Name.Contains('/'))
            .ThenBy(document => document.Name, StringComparer.Ordinal);
    }

    private static string RelativeName(string localeDirectory, string file)
    {
        string relative = Path.GetRelativePath(localeDirectory, file).Replace('\\', '/');
        return relative.EndsWith(_extension, StringComparison.Ordinal) ? relative[..^_extension.Length] : relative;
    }

    private static bool IsWellFormedJson(string text)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Dictionary<string, string> ReadManifest(string text)
    {
        using JsonDocument document = JsonDocument.Parse(text);
        Dictionary<string, string> manifest = new(StringComparer.Ordinal);
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.String)
            {
                manifest[property.Name] = property.Value.GetString()!;
            }
        }

        return manifest;
    }
}
