using server.Tests.Infrastructure;
using System.Reflection;
using System.Text.Json;

namespace server.Tests;

public sealed class ServerKeyLocalizationTests
{
    /// <summary>语言包里这些键由前端自己维护，不来自 <see cref="ServerKeys"/>.</summary>
    private static readonly string[] ClientOnlyKeys = ["requestFailed", "httpError", "networkError"];

    /// <summary>
    /// 契约：l10nKey 的值就是词条名，后台按 l10nKey 直接查 i18n 语言包.
    /// 常量名与取值不一致时这条链路会静默退化成服务端英文 message.
    /// </summary>
    [Fact]
    public void EveryServerKey_IsNamedAfterItsValue()
    {
        string[] mismatched = [.. ServerKeyValues()
            .Where(key => key.Name != key.Value)
            .Select(key => $"{key.Name} = {key.Value}")
            .Order(StringComparer.Ordinal)];

        Assert.True(mismatched.Length is 0, $"ServerKeys 的常量名必须与取值一致：\n{string.Join('\n', mismatched)}");
    }

    [Theory]
    [InlineData("zh_CN")]
    [InlineData("en_US")]
    public void EveryServerKey_HasALocalizedMessage(string locale)
    {
        IReadOnlyDictionary<string, string?> bundle = ReadBundle(locale);

        string[] missing = [.. ServerKeyValues()
            .Select(key => key.Value)
            .Where(value => !bundle.TryGetValue(value, out string? text) || string.IsNullOrWhiteSpace(text))
            .Order(StringComparer.Ordinal)];

        Assert.True(missing.Length is 0, $"{locale} 的 i18n/server.json 缺少 {missing.Length} 条 ServerKeys 词条：\n{string.Join('\n', missing)}");
    }

    [Theory]
    [InlineData("zh_CN")]
    [InlineData("en_US")]
    public void LocaleBundle_OnlyContainsKnownNonEmptyKeys(string locale)
    {
        HashSet<string> known = [.. ServerKeyValues().Select(key => key.Value), .. ClientOnlyKeys];
        IReadOnlyDictionary<string, string?> bundle = ReadBundle(locale);

        string[] unknown = [.. bundle.Keys.Where(key => !known.Contains(key)).Order(StringComparer.Ordinal)];
        Assert.True(unknown.Length is 0, $"{locale} 的 i18n/server.json 含 ServerKeys 里没有的键（多半是改名后的残留）：\n{string.Join('\n', unknown)}");

        string[] empty = [.. bundle.Where(pair => string.IsNullOrWhiteSpace(pair.Value)).Select(pair => pair.Key).Order(StringComparer.Ordinal)];
        Assert.True(empty.Length is 0, $"{locale} 的 i18n/server.json 有空词条：\n{string.Join('\n', empty)}");
    }

    [Fact]
    public void BothLocales_CarryTheSameKeys()
    {
        string[] zh = [.. ReadBundle("zh_CN").Keys.Order(StringComparer.Ordinal)];
        string[] en = [.. ReadBundle("en_US").Keys.Order(StringComparer.Ordinal)];

        Assert.Equal(zh, en);
    }

    private static IEnumerable<(string Name, string Value)> ServerKeyValues()
    {
        return typeof(ServerKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true } && field.FieldType == typeof(string))
            .Select(field => (field.Name, (string)field.GetRawConstantValue()!));
    }

    private static IReadOnlyDictionary<string, string?> ReadBundle(string locale)
    {
        string path = Path.Combine(FindRepositoryRoot(), "admin-web", "src", "i18n", locale, "server.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement.GetProperty("server").EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString());
    }

    private static string FindRepositoryRoot()
    {
        for (string? directory = AppContext.BaseDirectory; directory is not null; directory = Path.GetDirectoryName(directory))
        {
            if (Directory.Exists(Path.Combine(directory, "admin-web")))
            {
                return directory;
            }
        }

        throw new InvalidOperationException($"从 {AppContext.BaseDirectory} 向上找不到含 admin-web 的仓库根目录");
    }
}