using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using server.Models.Context;
using server.Models.Metadata;
using server.Services.Metadata;

// 把 static/metadata/Genshin 那棵树灌进 metadata_document 表，再投影成分表读模型。
// 先全量校验再写库：任何一个文档跟 Meta.json 记录的哈希对不上就一个字节都不写。
//   dotnet run -c Release --project tools/MetadataImport -- D:\projects\hutao\static\metadata\Genshin --check
//   dotnet run -c Release --project tools/MetadataImport -- D:\projects\hutao\static\metadata\Genshin --appsettings server\server\appsettings.json
//   dotnet run -c Release --project tools/MetadataImport -- --project --appsettings server\server\appsettings.json
// --project 不带目录时只做投影与「装配 == 原文档」的逐字节自检，库里的文档得已经导好。
string root = string.Empty;
string? locale = null;
string? kind = null;
string? connection = null;
string? appSettingsPath = null;
bool checkOnly = false;
bool project = false;

for (int index = 0; index < args.Length; index++)
{
    string argument = args[index];
    switch (argument)
    {
        case "--check":
        {
            checkOnly = true;
            break;
        }

        case "--project":
        {
            project = true;
            break;
        }

        case "--kind":
        {
            kind = args[++index];
            break;
        }

        case "--locale":
        {
            locale = args[++index];
            break;
        }

        case "--connection":
        {
            connection = args[++index];
            break;
        }

        case "--appsettings":
        {
            appSettingsPath = args[++index];
            break;
        }

        default:
        {
            if (root.Length is 0)
            {
                root = argument;
            }
            else
            {
                Console.Error.WriteLine($"不认识的参数：{argument}");
                return 2;
            }

            break;
        }
    }
}

// 没目录就只能投影；--check 需要一个目录才有意义
if (root.Length is 0 && (!project || checkOnly))
{
    Console.Error.WriteLine("""
用法：dotnet run -c Release --project tools/MetadataImport -- [<metadata 根目录>] [选项]
      根目录是 static\metadata\Genshin，下面每个子目录是一个 locale（CHS、EN…）。
选项：--locale <语言>          只处理一个语言
      --check                  只按启动器的口径校验，不写库
      --project                投影分表读模型，并逐字节自检「装配 == 原文档」
      --kind <分类>            只投影一个分类（配合 --project）
      --connection <连接串>    MetadataMysql8 的连接串，也可以用环境变量 HUTAO_METADATA_MYSQL
      --appsettings <路径>     从部署好的 appsettings.json 读 ConnectionStrings:MetadataMysql8
""");
    return 2;
}

if (root.Length is not 0 && !Directory.Exists(root))
{
    Console.Error.WriteLine($"目录不存在：{root}");
    return 2;
}

Stopwatch watch = Stopwatch.StartNew();
if (root.Length is not 0)
{
    Console.WriteLine($"root: {root}{(locale is null ? string.Empty : $", locale: {locale}")}");
    MetadataTreeImport.Report verified = await MetadataTreeImport.VerifyAsync(root, locale, CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine($"校验：{verified.Locales} 个 locale，{verified.Documents} 个文档，{verified.Bytes / 1024 / 1024} MiB，{verified.Issues.Count} 个问题，{watch.ElapsedMilliseconds} ms");
    foreach (MetadataTreeImport.Issue issue in verified.Issues.Take(40))
    {
        Console.WriteLine($"  ! {issue.Locale}/{issue.Name}: {issue.Reason}");
    }

    if (!verified.Succeeded)
    {
        Console.Error.WriteLine("校验没通过，一个字节都没写。先把这棵树和 Meta.json 修一致。");
        return 1;
    }
}

if (checkOnly)
{
    return 0;
}

string? resolved = connection
    ?? Environment.GetEnvironmentVariable("HUTAO_METADATA_MYSQL")
    ?? ReadMetadataConnection(appSettingsPath);
if (resolved is null)
{
    Console.Error.WriteLine("没有连接串：用 --connection、环境变量 HUTAO_METADATA_MYSQL，或 --appsettings 指向含 ConnectionStrings:MetadataMysql8 的 appsettings.json。");
    return 2;
}

DbContextOptions<MetadataDbContext> options = new DbContextOptionsBuilder<MetadataDbContext>()
    .UseMySql(resolved, ServerVersion.AutoDetect(resolved), builder => builder.CommandTimeout(600))
    .Options;

await using MetadataDbContext metadataDbContext = new(options);
MetadataTranslationService translationService = new(metadataDbContext, new MetadataStoreService(metadataDbContext));
Progress<string> progress = new(line => Console.WriteLine(line));

if (root.Length is not 0)
{
    watch.Restart();
    MetadataTreeImport.Report imported;
    try
    {
        imported = await MetadataTreeImport.ImportAsync(metadataDbContext, root, locale, progress, CancellationToken.None).ConfigureAwait(false);
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"导入失败：{exception.GetType().Name}: {exception.Message}");
        Console.Error.WriteLine("若报 Table ... doesn't exist：先在 Metadata 库执行 server/update_metadata.sql 里的 metadata_document 与 metadata_translation 两段建表语句。");
        return 3;
    }

    Console.WriteLine($"导入：{imported.Locales} 个 locale，{imported.Documents} 个文档，{imported.Bytes / 1024 / 1024} MiB，{watch.Elapsed.TotalSeconds:F0} s");

    // 导入会把文档打回上游原文，人工改过的词条按覆盖表重新贴回去
    foreach (string localeDirectory in Directory.EnumerateDirectories(root).OrderBy(directory => directory, StringComparer.Ordinal))
    {
        string localeName = Path.GetFileName(localeDirectory);
        if (locale is not null && !string.Equals(localeName, locale, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        MetadataTranslationService.ReapplyResult reapplied = await translationService.ReapplyAsync(localeName, CancellationToken.None).ConfigureAwait(false);
        if (reapplied.RewrittenDocuments > 0)
        {
            Console.WriteLine($"重贴覆盖：{localeName} 重写 {reapplied.RewrittenDocuments} 个文档");
        }

        foreach (string skipped in reapplied.SkippedPointers)
        {
            Console.WriteLine($"  ? {localeName}/{skipped} 在新版本里已无落点，覆盖未生效");
        }
    }
}

if (project)
{
    return await ProjectAsync(metadataDbContext, kind, locale, progress).ConfigureAwait(false);
}

return 0;

static string? ReadMetadataConnection(string? path)
{
    if (path is null || !File.Exists(path))
    {
        return null;
    }

    IConfiguration configuration = new ConfigurationBuilder()
        .AddJsonFile(path, optional: false, reloadOnChange: false)
        .Build();
    return configuration.GetConnectionString("MetadataMysql8");
}

// 投影 + 自检。自检只认一个标准：用分表装配出来的字节必须跟 metadata_document.Doc 逐字节相同，
// 因为那五张表是派生数据，一旦装配不出来或装配出另一个字节序列，就说明投影口径错了，
// 而对外服务完全不碰它们，所以这里失败不影响启动器下载。
static async ValueTask<int> ProjectAsync(MetadataDbContext metadataDbContext, string? kind, string? locale, IProgress<string> progress)
{
    Stopwatch watch = Stopwatch.StartNew();
    MetadataProjectionService.Report report = await new MetadataProjectionService(metadataDbContext)
        .RebuildAsync(kind, progress, CancellationToken.None)
        .ConfigureAwait(false);

    Console.WriteLine($"投影：{report.Kinds.Count} 个分类，{report.Kinds.Sum(item => (long)item.Objects)} 个对象（整对象兜底 {report.Kinds.Sum(item => (long)item.RawObjects)} 个），"
        + $"{report.Kinds.Sum(item => (long)item.I18nRows)} 行 i18n，{report.Kinds.Sum(item => (long)item.LayoutRows)} 行 layout，引用边 {report.RefRows} 行，{watch.Elapsed.TotalSeconds:F0} s");
    foreach (MetadataProjectionService.Issue issue in report.Issues)
    {
        Console.WriteLine($"  ! {issue.Kind}: {issue.Detail}");
    }

    if (!report.Succeeded)
    {
        Console.Error.WriteLine("投影没通过：报问题的分类一行读模型都没留（原文档表不受影响，对外服务照常）。");
        return 4;
    }

    watch.Restart();
    MetadataObjectAssembler assembler = new(metadataDbContext);
    List<string> locales = await metadataDbContext.MetadataDocuments
        .AsNoTracking()
        .Select(document => document.Locale)
        .Distinct()
        .ToListAsync(CancellationToken.None)
        .ConfigureAwait(false);
    locales.Sort(StringComparer.Ordinal);

    int compared = 0;
    int uncovered = 0;
    List<string> mismatches = [];
    foreach (string current in locales)
    {
        if (locale is not null && !string.Equals(current, locale, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        List<string> names = await metadataDbContext.MetadataDocuments
            .AsNoTracking()
            .Where(document => document.Locale == current)
            .OrderBy(document => document.Name)
            .Select(document => document.Name)
            .ToListAsync(CancellationToken.None)
            .ConfigureAwait(false);

        foreach (string name in names)
        {
            if (MetadataKindRegistry.FindByName(name) is null)
            {
                uncovered++;
                continue;
            }

            string? assembled = await assembler.AssembleAsync(current, name, CancellationToken.None).ConfigureAwait(false);
            MetadataDocument document = await metadataDbContext.MetadataDocuments
                .AsNoTracking()
                .FirstAsync(item => item.Locale == current && item.Name == name, CancellationToken.None)
                .ConfigureAwait(false);

            compared++;
            if (string.Equals(assembled, document.Doc, StringComparison.Ordinal))
            {
                continue;
            }

            mismatches.Add($"{current}/{name}");
            if (mismatches.Count <= 40)
            {
                Console.WriteLine($"  ! {current}/{name}: {(assembled is null ? "读模型装配不出这个文档" : "装配结果与原文档字节不同")}");
            }
        }

        progress.Report($"{current}: 已装配比对 {compared} 个文档，{watch.Elapsed.TotalSeconds:F0} s");
    }

    Console.WriteLine($"自检：{compared} 个文档逐字节相同，{uncovered} 个不在投影范围内（清单文档 Meta 与未注册分类），{mismatches.Count} 个不一致，{watch.Elapsed.TotalSeconds:F0} s");
    return mismatches.Count is 0 ? 0 : 5;
}
