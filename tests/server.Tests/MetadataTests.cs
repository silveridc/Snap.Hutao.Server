using Microsoft.EntityFrameworkCore;
using server.Core.IO;
using server.Models.Metadata;
using server.Services.Metadata;
using server.Tests.Infrastructure;
using System.Net;
using System.Text.Json.Nodes;

namespace server.Tests;

/// <summary>
/// metadata 落库与对外服务。断言的重心全在「逐字节」上：
/// 启动器把响应体原样写盘，再用 XXH64 跟 <c>Meta.json</c> 里的哈希比对，
/// 所以序列化写法、CR 归一化、清单键顺序、提列里的 Hash/Size 任一跑偏，
/// 表现出来都是「每次启动都重下全量」这种极难定位的故障。
/// <para>
/// 每个用例各占一对没别人用过的 locale，写入互不影响；文档正文用真实的
/// <c>MetadataTreeImport</c> 导库，被测代码一行都没有绕过。
/// </para>
/// </summary>
[Collection(HutaoServerCollection.Name)]
public sealed class MetadataTests
{
    private const string RealHardChallengeScheduleHash = "1F8AD971DA83C997";

    private const int RealHardChallengeScheduleBytes = 1646;

    private readonly HutaoServerFactory factory;
    private readonly HttpClient client;
    private readonly TestUsers users;

    public MetadataTests(HutaoServerFactory factory)
    {
        this.factory = factory;
        client = factory.CreateApiClient();
        users = factory.Users;
    }

    [Fact]
    public void Hash_MatchesTheLauncherRecipeOnARealProductionDocument()
    {
        Assert.Equal(MetadataHasher.HashText("a\nb"), MetadataHasher.HashText("a\r\nb"));

        string normalized = MetadataHasher.NormalizeLineEndings(HardChallengeSchedule);
        Assert.Equal(RealHardChallengeScheduleHash, MetadataHasher.HashText(HardChallengeSchedule));
        Assert.Equal(RealHardChallengeScheduleBytes, Encoding.UTF8.GetByteCount(normalized));

        // 上游这批文件就是「JsonNode + 2 空格缩进 + 非 ASCII 不转义 + LF」写出来的。
        // 往返不逐字节复现的话，改一条 i18n 会让整个文件重算哈希，启动器就得重下。
        Assert.Equal(normalized, MetadataHasher.Serialize(JsonNode.Parse(normalized)!));
    }

    [Fact]
    public async Task Import_ServesEveryDocumentByteForByte_IncludingScatteredPaths()
    {
        await ImportAsync(("SVA", Docs("SVA")), ("SVB", Docs("SVB")));

        string[] locales = ["SVA", "SVB"];
        foreach (string locale in locales)
        {
            foreach (Doc doc in Docs(locale))
            {
                await AssertServedAsync(locale, doc.Name, doc.Json);
            }
        }

        // 语言目录大小写不敏感地归一化到库里的键；文档名保持上游写法
        await AssertServedAsync("sva", "Achievement", DocJson("SVA", "Achievement"));
    }

    [Fact]
    public async Task Import_RebuildsMetaWithTheUpstreamKeyOrderAndHashes()
    {
        await ImportAsync(("MTA", Docs("MTA")));

        string meta = await BodyAsync("MTA", "Meta");
        using JsonDocument document = JsonDocument.Parse(meta);

        string[] keys = [.. document.RootElement.EnumerateObject().Select(property => property.Name)];
        string[] expected = ["Achievement", "Meta", "Weapon", "Avatar/10000002"];
        Assert.Equal(expected, keys);

        // 提列与对外字节必须互相吻合：ETag 用的就是 Hash 列，Size 列是 UTF-8 字节数
        TestResponse list = await SendServiceAsync("GET", "/api/Service/Metadata/Documents?locale=MTA", (await users.MaintainerAsync()).AccessToken);
        Assert.Equal(ReturnCodes.Success, list.Retcode);
        using JsonDocument rows = JsonDocument.Parse(list.Data.GetRawText());
        Assert.Equal(4, rows.RootElement.EnumerateArray().Count());

        string[] names = ["Achievement", "Weapon", "Avatar/10000002", "Meta"];
        foreach (string name in names)
        {
            string served = await BodyAsync("MTA", name);
            JsonElement row = rows.RootElement.EnumerateArray().Single(item => item.GetProperty("Name").GetString() == name);
            Assert.Equal(MetadataHasher.HashText(served), row.GetProperty("Hash").GetString());
            Assert.Equal(Encoding.UTF8.GetByteCount(served), row.GetProperty("Size").GetInt32());
            Assert.Equal($"\"{MetadataHasher.HashText(served)}\"", await EtagAsync("MTA", name));

            if (name != "Meta")
            {
                Assert.Equal(MetadataHasher.HashText(served), document.RootElement.GetProperty(name).GetString());
            }
        }

        // Meta 这一行是自指，永远不可能等于自己的哈希；这里钉住库里的定义：
        // 「不含 Meta 条目时的清单字节的 XXH64」，顺序照清单（去掉 Meta 后不变）。
        Dictionary<string, string> withoutMeta = [];
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (property.Name != "Meta")
            {
                withoutMeta[property.Name] = property.Value.GetString()!;
            }
        }

        Assert.Equal(MetadataHasher.Hash(MetadataHasher.Serialize(withoutMeta)), document.RootElement.GetProperty("Meta").GetString());
    }

    [Fact]
    public async Task Document_HonoursTheEtag_WithIfNoneMatch()
    {
        await ImportAsync(("ETA", Docs("ETA")));

        using HttpResponseMessage first = await client.GetAsync("/metadata/Genshin/ETA/Weapon.json", Token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        string etag = SingleHeader(first, "ETag");
        Assert.Equal($"\"{MetadataHasher.HashText(Canonical(DocJson("ETA", "Weapon")))}\"", etag);

        // URL 不带版本号，只能靠 revalidate 让反代每次回源问一嘴（指令顺序由框架重排，只断言内容）
        string cacheControl = SingleHeader(first, "Cache-Control");
        Assert.StartsWith("public", cacheControl, StringComparison.Ordinal);
        Assert.Contains("max-age=0", cacheControl, StringComparison.Ordinal);
        Assert.Contains("must-revalidate", cacheControl, StringComparison.Ordinal);

        using HttpResponseMessage notModified = await SendAsync("/metadata/Genshin/ETA/Weapon.json", etag);
        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
        Assert.Equal(string.Empty, await notModified.Content.ReadAsStringAsync(Token));

        using HttpResponseMessage changed = await SendAsync("/metadata/Genshin/ETA/Weapon.json", "\"0000000000000000\"");
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
    }

    [Fact]
    public async Task Document_ReturnsNotFound_ForUnknownOrMalformedRequests()
    {
        await ImportAsync(("NFA", Docs("NFA")));

        string[] urls =
        [
            "/metadata/Genshin/NFA/NoSuchDocument.json",
            "/metadata/Genshin/NFA/Achievement",
            "/metadata/Genshin/NFA/.json",
            "/metadata/Genshin/NFA/Achievement.json/",
            "/metadata/Genshin/ABCDEFGHI/Achievement.json",
            "/metadata/Genshin/NF/Achievement.json",
            "/metadata/Genshin/N1/Achievement.json",
            "/metadata/Genshin/ZZZ/Achievement.json",
            "/metadata/Genshin/NFA/Weapon%20two.json",
        ];

        foreach (string url in urls)
        {
            using HttpResponseMessage response = await client.GetAsync(url, Token);
            Assert.True(response.StatusCode is HttpStatusCode.NotFound, $"Expected 404 from {url}, got {(int)response.StatusCode}");
        }
    }

    [Fact]
    public async Task SetTranslation_RewritesOnlyTheTargetString_AndRefreshesHashAndMeta()
    {
        await ImportAsync(("TSA", Docs("TSA")));
        TestUser maintainer = await users.MaintainerAsync();
        const string translation = "雾切之回光";

        string pristine = Canonical(DocJson("TSA", "Weapon"));
        TestResponse set = await SetTranslationAsync(maintainer.AccessToken, "TSA", "Weapon", "/0/Name", translation);
        Assert.Equal(ReturnCodes.Success, set.Retcode);
        Assert.True(set.Data.GetProperty("Succeeded").GetBoolean());

        string served = await BodyAsync("TSA", "Weapon");
        Assert.Equal(set.Data.GetProperty("Hash").GetString(), MetadataHasher.HashText(served));

        // 只动那一行：缩进、键序、非 ASCII 写法都不许跟着变，否则整文件哈希漂移、客户端白重下
        string[] before = pristine.Split('\n');
        string[] after = served.Split('\n');
        Assert.Equal(before.Length, after.Length);
        string changed = Assert.Single(after.Where((line, index) => !string.Equals(line, before[index], StringComparison.Ordinal)));
        Assert.Contains(translation, changed, StringComparison.Ordinal);
        Assert.Equal(translation, JsonNode.Parse(served)!.AsArray()[0]!["Name"]!.GetValue<string>());
        Assert.Equal("A blade that never changes", JsonNode.Parse(served)!.AsArray()[0]!["Description"]!.GetValue<string>());

        TestResponse overrides = await SendServiceAsync("GET", "/api/Service/Metadata/Translations?locale=TSA", maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, overrides.Retcode);
        using JsonDocument rows = JsonDocument.Parse(overrides.Data.GetRawText());
        JsonElement row = Assert.Single(rows.RootElement.EnumerateArray());
        Assert.Equal("/0/Name", row.GetProperty("Pointer").GetString());
        Assert.Equal(translation, row.GetProperty("Value").GetString());
        Assert.Equal("TSA Sword", row.GetProperty("OriginalValue").GetString());

        // 覆盖写过之后清单里那一行也必须跟着新哈希走，否则启动器会一直判定文档损坏
        using JsonDocument meta = JsonDocument.Parse(await BodyAsync("TSA", "Meta"));
        Assert.Equal(MetadataHasher.HashText(served), meta.RootElement.GetProperty("Weapon").GetString());
    }

    [Fact]
    public async Task SetTranslation_RejectsPointersThatDoNotLandOnAStringLeaf()
    {
        await ImportAsync(("TPA", Docs("TPA")));
        TestUser maintainer = await users.MaintainerAsync();
        string etag = await EtagAsync("TPA", "Weapon");

        TestResponse number = await SetTranslationAsync(maintainer.AccessToken, "TPA", "Weapon", "/0/Id", "123");
        Assert.Equal(ReturnCodes.InvalidQueryString, number.Retcode);
        Assert.Contains("not a string", number.Message, StringComparison.Ordinal);

        TestResponse missing = await SetTranslationAsync(maintainer.AccessToken, "TPA", "Weapon", "/0/NoSuch", "x");
        Assert.Equal(ReturnCodes.InvalidQueryString, missing.Retcode);
        Assert.Contains("does not exist", missing.Message, StringComparison.Ordinal);

        TestResponse relative = await SetTranslationAsync(maintainer.AccessToken, "TPA", "Weapon", "0/Name", "x");
        Assert.Equal(ReturnCodes.InvalidQueryString, relative.Retcode);
        Assert.Contains("must start with", relative.Message, StringComparison.Ordinal);

        TestResponse unimported = await SetTranslationAsync(maintainer.AccessToken, "TPA", "NoSuchDocument", "/Name", "x");
        Assert.Equal(ReturnCodes.InvalidQueryString, unimported.Retcode);
        Assert.Contains("is not imported", unimported.Message, StringComparison.Ordinal);

        Assert.Equal(etag, await EtagAsync("TPA", "Weapon"));
    }

    [Fact]
    public async Task RemoveTranslation_RestoresThePristineBytesAndDeletesTheOverride()
    {
        await ImportAsync(("TRA", Docs("TRA")));
        TestUser maintainer = await users.MaintainerAsync();

        string pristine = Canonical(DocJson("TRA", "Achievement"));
        string pristineHash = MetadataHasher.HashText(pristine);
        Assert.Equal(ReturnCodes.Success, (await SetTranslationAsync(maintainer.AccessToken, "TRA", "Achievement", "/1/Description", "已译描述")).Retcode);
        Assert.NotEqual(pristineHash, MetadataHasher.HashText(await BodyAsync("TRA", "Achievement")));

        TestResponse remove = await SendServiceAsync(
            "POST",
            $"/api/Service/Metadata/Translation/Remove?locale=TRA&name=Achievement&pointer={Uri.EscapeDataString("/1/Description")}",
            maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, remove.Retcode);
        Assert.Equal(pristineHash, remove.Data.GetProperty("Hash").GetString());

        // 还原靠的是覆盖行里记的原文，不是重放导入，所以逐字节回到原来的哈希
        await AssertServedAsync("TRA", "Achievement", DocJson("TRA", "Achievement"));
        TestResponse list = await SendServiceAsync("GET", "/api/Service/Metadata/Translations?locale=TRA", maintainer.AccessToken);
        Assert.Empty(JsonDocument.Parse(list.Data.GetRawText()).RootElement.EnumerateArray());

        TestResponse again = await SendServiceAsync(
            "POST",
            $"/api/Service/Metadata/Translation/Remove?locale=TRA&name=Achievement&pointer={Uri.EscapeDataString("/1/Description")}",
            maintainer.AccessToken);
        Assert.Equal(ReturnCodes.InvalidQueryString, again.Retcode);
        Assert.Contains("No translation override", again.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reimport_ThenReapply_ResteadesOverridesAndReportsLostPointers()
    {
        await ImportAsync(("RPA", Docs("RPA")));
        TestUser maintainer = await users.MaintainerAsync();
        Assert.Equal(ReturnCodes.Success, (await SetTranslationAsync(maintainer.AccessToken, "RPA", "Achievement", "/1/Description", "旧版的译文")).Retcode);
        Assert.Equal(ReturnCodes.Success, (await SetTranslationAsync(maintainer.AccessToken, "RPA", "Avatar/10000002", "/Fetter/Title", "正义之 RPA")).Retcode);

        // 模拟上游改版：Achievement 里那个键没了，重贴时只能报「丢了」，不能让整语言重贴失败
        Doc[] upgraded =
        [
            new("Achievement", """{"1":{"Id":1,"Name":"成就 RPA"},"2":{"Id":2,"Name":"Second RPA"}}"""),
            .. Docs("RPA").Where(doc => doc.Name != "Achievement"),
        ];
        await ImportAsync(("RPA", upgraded));

        MetadataTranslationService.ReapplyResult reapplied = await ReapplyAsync("RPA");
        Assert.Equal(2, reapplied.RewrittenDocuments);
        Assert.Equal(["Achievement/1/Description"], [.. reapplied.SkippedPointers]);

        Assert.Equal("正义之 RPA", JsonNode.Parse(await BodyAsync("RPA", "Avatar/10000002"))!["Fetter"]!["Title"]!.GetValue<string>());
        Assert.Null(JsonNode.Parse(await BodyAsync("RPA", "Achievement"))!["1"]!["Description"]);

        // 丢了落点的覆盖行留着：下一版键回来了，重贴就能自己长回去
        TestResponse list = await SendServiceAsync("GET", "/api/Service/Metadata/Translations?locale=RPA", maintainer.AccessToken);
        Assert.Equal(2, JsonDocument.Parse(list.Data.GetRawText()).RootElement.EnumerateArray().Count());
    }

    [Fact]
    public async Task Leaves_ComparesStringLeavesAgainstTheBaselineLocale()
    {
        Doc[] baselineWeapon =
        [
            new("Weapon", """[{"Id":11401,"Name":"LVB Sword"}]"""),
            .. Docs("LVB").Where(doc => doc.Name != "Weapon"),
        ];
        await ImportAsync(("LVA", Docs("LVA")), ("LVB", baselineWeapon));
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse translated = await SendServiceAsync("GET", "/api/Service/Metadata/Leaves?name=Achievement&locale=LVA&baseline=LVB", maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, translated.Retcode);
        using JsonDocument document = JsonDocument.Parse(translated.Data.GetRawText());

        JsonElement name = document.RootElement.EnumerateArray().Single(leaf => leaf.GetProperty("Pointer").GetString() == "/1/Name");
        Assert.Equal("成就 LVA", name.GetProperty("LocalValue").GetString());
        Assert.Equal("成就 LVB", name.GetProperty("BaselineValue").GetString());
        Assert.True(name.GetProperty("Differs").GetBoolean());

        JsonElement shared = document.RootElement.EnumerateArray().Single(leaf => leaf.GetProperty("Pointer").GetString() == "/2/Description");
        Assert.Equal("Shared description", shared.GetProperty("LocalValue").GetString());
        Assert.False(shared.GetProperty("Differs").GetBoolean());

        // 对照侧没有这个指针：语言之间结构并不总是一致（实测 9877 个指针至少缺一处）
        TestResponse absent = await SendServiceAsync("GET", "/api/Service/Metadata/Leaves?name=Weapon&locale=LVA&baseline=LVB", maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, absent.Retcode);
        using JsonDocument weapon = JsonDocument.Parse(absent.Data.GetRawText());
        JsonElement description = weapon.RootElement.EnumerateArray().Single(leaf => leaf.GetProperty("Pointer").GetString() == "/0/Description");
        Assert.Equal("A blade that never changes", description.GetProperty("LocalValue").GetString());
        Assert.Null(Text(description, "BaselineValue"));
        Assert.False(description.GetProperty("Differs").GetBoolean());

        TestResponse none = await SendServiceAsync("GET", "/api/Service/Metadata/Leaves?name=Weapon&locale=LVA&baseline=ZZQ", maintainer.AccessToken);
        Assert.Equal(ReturnCodes.NotFound, none.Retcode);
    }

    [Fact]
    public async Task Verify_FailsOnlyWhenADocumentDoesNotMatchItsManifestEntry()
    {
        Doc[] docs = Docs("VER");
        using TempTree tree = TempTree.Create(("VER", docs));
        tree.Write("VER", "Meta", MetadataHasher.Serialize(docs.ToDictionary(doc => doc.Name, doc => MetadataHasher.HashText(Canonical(doc.Json)), StringComparer.Ordinal)));

        MetadataTreeImport.Report clean = await MetadataTreeImport.VerifyAsync(tree.Root, "VER", Token);
        Assert.True(clean.Succeeded, string.Join("; ", clean.Issues.Select(issue => $"{issue.Locale}/{issue.Name}: {issue.Reason}")));
        Assert.Equal(1, clean.Locales);

        // 半套坏数据推上线，表现是启动器反复重下、然后卡在反序列化失败，比导入失败难查得多
        tree.Write("VER", "Weapon", Canonical("""[{"Id":11401,"Name":"改过的武器","Description":"A blade that never changes"}]"""));
        MetadataTreeImport.Report dirty = await MetadataTreeImport.VerifyAsync(tree.Root, "VER", Token);
        Assert.False(dirty.Succeeded);
        MetadataTreeImport.Issue issue = Assert.Single(dirty.Issues);
        Assert.Equal("VER", issue.Locale);
        Assert.Equal("Weapon", issue.Name);
        Assert.Contains("manifest says", issue.Reason, StringComparison.Ordinal);

        File.Delete(tree.FilePath("VER", "Weapon"));
        MetadataTreeImport.Report missing = await MetadataTreeImport.VerifyAsync(tree.Root, "VER", Token);
        Assert.Contains("the file is missing", string.Join("; ", missing.Issues.Select(item => item.Reason)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Projection_PlaceholdersOnlyTheLeavesThatVary_AndAssemblesByteForByte()
    {
        const ulong First = 9110000;
        await ImportAsync(("PRA", ObjectDocs("PRA", First)), ("PRB", ObjectDocs("PRB", First)));
        AssertSucceeded(await RebuildAsync("Achievement"));

        // 只有真正随语言变的键才变成占位符；Id/Progress/Goal 这类数值留在骨架里，
        // 因为外部 API 的过滤靠它们，一旦被占位符吃掉生成列就只剩 NULL
        JsonNode varied = JsonNode.Parse((await ObjectAsync("Achievement", First)).Doc)!;
        Assert.True(MetadataLeaves.TryPlaceholder(varied["Name"], out string? namePath));
        Assert.Equal("/Name", namePath);
        Assert.True(MetadataLeaves.TryPlaceholder(varied["Description"], out string? descriptionPath));
        Assert.Equal("/Description", descriptionPath);
        Assert.Equal(0, varied["Progress"]!.GetValue<int>());
        Assert.Equal(First + 1, varied["Goal"]!.GetValue<ulong>());
        Assert.Equal(First + 9, varied["FinishReward"]!["Id"]!.GetValue<ulong>());

        // 两个语言一模一样的那条 Description 不许被抽走
        JsonNode shared = JsonNode.Parse((await ObjectAsync("Achievement", First + 1)).Doc)!;
        Assert.True(MetadataLeaves.TryPlaceholder(shared["Name"], out _));
        Assert.Equal("Shared description", shared["Description"]!.GetValue<string>());

        MetadataObject achievement = await ObjectAsync("Achievement", First);
        Assert.Equal("PRA", achievement.SourceLocale);
        Assert.Equal(0u, achievement.Ordinal);
        Assert.Equal(1u, (await ObjectAsync("Achievement", First + 1)).Ordinal);

        // 骨架来源自己也有一份 i18n 行：装配只有一条代码路，不给 CHS 开后门就永远不会走到没测过的分支
        List<MetadataObjectI18n> i18ns = await I18nsAsync("Achievement", First);
        Assert.Equal(4, i18ns.Count);
        Assert.Equal(2, i18ns.Select(item => item.Locale).Distinct().Count());
        Assert.Contains(i18ns, item => item.Locale == "PRA" && item.Field == "/Name" && item.Value == "\"成就 PRA\"");
        Assert.Contains(i18ns, item => item.Locale == "PRB" && item.Field == "/Description" && item.Value == "\"PRB 的描述\"");

        // 那条两边一致的 Description 只在每个 locale 留下一行 /Name
        Assert.Single(await I18nsAsync("Achievement", First + 1, "PRA"));

        foreach (string locale in new[] { "PRA", "PRB" })
        {
            Assert.Equal(await BodyAsync(locale, "Achievement"), await AssembleAsync(locale, "Achievement"));
        }
    }

    [Fact]
    public async Task Projection_AssemblesScatteredDocuments_AndIgnoresUnregisteredNames()
    {
        const ulong First = 9120000;
        await ImportAsync(("PSA", ObjectDocs("PSA", First)), ("PSB", ObjectDocs("PSB", First)));
        AssertSucceeded(await RebuildAsync("Avatar"));
        AssertSucceeded(await RebuildAsync("Weapon"));

        string avatar = (await ObjectAsync("Avatar", First + 201)).Doc;
        JsonNode skeleton = JsonNode.Parse(avatar)!;
        Assert.Equal(First + 201, skeleton["Id"]!.GetValue<ulong>());
        Assert.True(MetadataLeaves.TryPlaceholder(skeleton["Fetter"]!["Title"], out string? titlePath));
        Assert.Equal("/Fetter/Title", titlePath);
        Assert.Equal("A fixed intro", skeleton["Fetter"]!["Intro"]!.GetValue<string>());

        // 散文件一个对象一个文档，装配回来的文档名带目录前缀，跟 metadata_document.Name 同口径
        foreach (string locale in new[] { "PSA", "PSB" })
        {
            Assert.Equal(await BodyAsync(locale, $"Avatar/{First + 201}"), await AssembleAsync(locale, $"Avatar/{First + 201}"));
            Assert.Equal(await BodyAsync(locale, "Weapon"), await AssembleAsync(locale, "Weapon"));
        }

        // Meta 这类清单文档不是对象集合，读模型不认识它，调用方应当回落到原文档表
        Assert.Null(await AssembleAsync("PSA", "Meta"));
    }

    [Fact]
    public async Task Projection_KeepsPerLocaleMembershipAndOrder()
    {
        await ImportAsync(
            ("PLA", [new Doc("Achievement", """[{"Id":9130001,"Name":"One PLA"},{"Id":9130002,"Name":"Two PLA"}]""")]),
            ("PLB", [new Doc("Achievement", """[{"Id":9130002,"Name":"Two PLB"}]""")]));
        AssertSucceeded(await RebuildAsync("Achievement"));

        // CHS 不是超集，各 locale 的成员与下标本来就可能不一样，所以「谁在、排第几」只能逐 locale 存
        List<MetadataObjectLayout> pla = await LayoutsAsync("PLA", "Achievement");
        Assert.Equal([9130001, 9130002], pla.Select(item => (long)item.GameId).ToArray());
        Assert.Equal([0, 1], pla.Select(item => (int)item.Position).ToArray());

        MetadataObjectLayout plb = Assert.Single(await LayoutsAsync("PLB", "Achievement"));
        Assert.Equal(9130002UL, plb.GameId);
        Assert.Equal(0u, plb.Position);

        // 结构表里两个对象都在：PLB 少的那条不是投影丢的，是那个语言本来就没有它
        ulong[] projected = [.. (await ObjectsAsync("Achievement")).Select(item => item.GameId).Where(id => id is 9130001UL or 9130002UL).Order()];
        Assert.Equal([9130001UL, 9130002UL], projected);
        foreach (string locale in new[] { "PLA", "PLB" })
        {
            Assert.Equal(await BodyAsync(locale, "Achievement"), await AssembleAsync(locale, "Achievement"));
        }
    }

    [Fact]
    public async Task Projection_FallsBackToTheWholeObject_WhenALocaleHasAnotherShape()
    {
        const ulong First = 9140000;
        await ImportAsync(
            ("PKA", [new Doc("Achievement", $$$"""[{"Id":{{{First}}},"Name":"一 PKA"}]""")]),
            ("PKB", [new Doc("Achievement", $$$"""[{"Id":{{{First}}},"Name":"一 PKB"}]""")]));

        // 多加一个键就算另一种结构：占位符表对不上，逐叶子替换会把这份语言装配成别的东西
        await ImportAsync(("PKC", [new Doc("Achievement", $$$"""[{"Id":{{{First}}},"Name":"一 PKC","Extra":{"Deep":true}}]""")]));
        MetadataProjectionService.Report report = await RebuildAsync("Achievement");
        AssertSucceeded(report);

        // RawCount 是整个分类的计数，别的用例的 fixture 也可能贡献兜底对象，所以这里只要求「至少我这个算进去了」
        Assert.True((await KindAsync("Achievement")).RawCount >= 1u);

        MetadataObjectI18n fallback = Assert.Single(await I18nsAsync("Achievement", First, "PKC"));
        Assert.Equal(MetadataObjectI18n.RawField, fallback.Field);

        // 兜底行存的就是那个语言自己的整对象原文，装配时它顶替整个骨架
        JsonNode whole = JsonNode.Parse(fallback.Value)!;
        Assert.Equal("一 PKC", whole["Name"]!.GetValue<string>());
        Assert.True(whole["Extra"]!["Deep"]!.GetValue<bool>());

        Assert.Single(await I18nsAsync("Achievement", First, "PKA"));
        Assert.Single(await I18nsAsync("Achievement", First, "PKB"));
        foreach (string locale in new[] { "PKA", "PKB", "PKC" })
        {
            Assert.Equal(await BodyAsync(locale, "Achievement"), await AssembleAsync(locale, "Achievement"));
        }
    }

    [Fact]
    public async Task Projection_PromotesFilterableNumbers_AndYieldsNullWhenANumberVaries()
    {
        const ulong First = 9150000;
        await ImportAsync(
            ("PNA", [new Doc("Weapon", $$$"""[{"Id":{{{First}}},"Name":"剑 PNA","Quality":5,"Rank":2},{"Id":{{{First + 1}}},"Name":"弓 PNA","Quality":4,"Rank":1}]""")]),
            ("PNB", [new Doc("Weapon", $$$"""[{"Id":{{{First}}},"Name":"剑 PNB","Quality":5,"Rank":3},{"Id":{{{First + 1}}},"Name":"弓 PNB","Quality":4,"Rank":1}]""")]));
        AssertSucceeded(await RebuildAsync("Weapon"));

        // 生成列直接读 Doc：值跟正文不可能对不上，这是这套 schema 用 STORED 生成列的全部理由
        Assert.Equal(5u, (await ObjectAsync("Weapon", First)).Quality);
        Assert.Equal(4u, (await ObjectAsync("Weapon", First + 1)).Quality);
        Assert.Equal(1u, (await ObjectAsync("Weapon", First + 1)).Rank);

        // Rank 在不同语言里居然是不同数字时，骨架里它必须让位给占位符（否则装配回来的就不是那个语言的字节），
        // 生成列随之变 NULL——「这个属性按语言取值不同」本来就没法用一列表达
        Assert.True(MetadataLeaves.TryPlaceholder(JsonNode.Parse((await ObjectAsync("Weapon", First)).Doc)!["Rank"], out string? rankPath));
        Assert.Equal("/Rank", rankPath);
        Assert.Null((await ObjectAsync("Weapon", First)).Rank);

        List<ulong> fiveStars = await factory.WithMetadataDbContextAsync(async metadataDbContext => await metadataDbContext.MetadataObjects
            .AsNoTracking()
            .Where(item => item.Kind == "Weapon" && item.Quality == 5)
            .Select(item => item.GameId)
            .ToListAsync(Token));
        Assert.Contains(First, fiveStars);
        Assert.DoesNotContain(First + 1, fiveStars);
    }

    [Fact]
    public async Task Projection_ResolvesReferenceEdgesByCandidateOrder()
    {
        const ulong First = 9160000;
        Doc[] docs =
        [
            new("AchievementGoal", $$$"""[{"Id":{{{First}}},"Name":"目标 PRR","FinishReward":{"Id":{{{First + 101}}},"Count":1}},{"Id":{{{First + 1}}},"Name":"二 PRR","FinishReward":{"Id":{{{First + 201}}},"Count":1}},{"Id":{{{First + 2}}},"Name":"三 PRR","FinishReward":{"Id":999999999,"Count":1}},{"Id":{{{First + 3}}},"Name":"四 PRR","FinishReward":{"Id":0,"Count":0}}]"""),
            new("NameCard", $$$"""[{"Id":{{{First + 101}}},"Name":"名片 PRR"}]"""),
            new("Material", $$$"""[{"Id":{{{First + 201}}},"Name":"材料 PRR"}]"""),
            new($"Avatar/{First + 301}", $$$"""{"Id":{{{First + 301}}},"Name":"角色 PRR"}"""),
            new("Weapon", $$$"""[{"Id":{{{First + 401}}},"Name":"武器 PRR"}]"""),
            new("GachaEvent", """[{"Name":"池一 PRR","UpOrangeList":[9160301,9160401,0]}]"""),
        ];
        await ImportAsync(("PRR", docs));

        // 目标分类的身份集合决定 RefKind，所以它们得先落库；GachaEvent 用位置当身份，本来就唯一
        AssertSucceeded(await RebuildAsync("Avatar"));
        AssertSucceeded(await RebuildAsync("Weapon"));
        AssertSucceeded(await RebuildAsync("NameCard"));
        AssertSucceeded(await RebuildAsync("Material"));
        AssertSucceeded(await RebuildAsync("AchievementGoal"));
        AssertSucceeded(await RebuildAsync("GachaEvent"));

        List<MetadataObjectRef> goals = await RefsAsync("AchievementGoal");
        Assert.Equal(3, goals.Count);
        Assert.Equal(["NameCard", "Material", "NameCard"], goals
            .OrderBy(item => item.GameId)
            .Select(item => item.RefKind)
            .ToArray());

        // 两边都找不到的值按候选的第一个写（号段混在一起时这是唯一确定的口径），0 一律表示没有引用
        Assert.Equal(First + 101, goals.First(item => item.GameId == First).RefId);
        Assert.Equal(First + 201, goals.First(item => item.GameId == First + 1).RefId);
        Assert.Equal(999999999UL, goals.First(item => item.GameId == First + 2).RefId);
        Assert.DoesNotContain(goals, item => item.GameId == First + 3);

        // GachaEvent 没有可用的数值身份，按文档下标认，所以三条边的发起方都是同一个 GameId
        List<MetadataObjectRef> gacha = await RefsAsync("GachaEvent");
        Assert.Equal(2, gacha.Count);
        Assert.All(gacha, item => Assert.Equal(0u, item.GameId));
        Assert.Equal("Avatar", gacha.Single(item => item.Ordinal == 0).RefKind);
        Assert.Equal("Weapon", gacha.Single(item => item.Ordinal == 1).RefKind);
    }

    [Fact]
    public async Task Projection_LeavesNothingBehind_WhenAnIdentityIsNotUnique()
    {
        await ImportAsync(
            ("PZA", [new Doc("Combine", """[{"Id":9170001,"Name":"重复 A"},{"Id":9170001,"Name":"重复 B"}]""")]),
            ("PZB", [new Doc("Combine", """[{"Id":9170011,"Name":"本来没问题"}]""")]));

        MetadataProjectionService.Report report = await RebuildAsync("Combine");
        Assert.False(report.Succeeded);
        MetadataProjectionService.Issue issue = Assert.Single(report.Issues);
        Assert.Equal("Combine", issue.Kind);
        Assert.Contains("repeats within one locale", issue.Detail, StringComparison.Ordinal);

        // 半套投影比没投影难查得多：这个分类一行都不许留下，装配也只能回答「不归我管」
        Assert.Empty(await ObjectsAsync("Combine"));
        Assert.Empty(await LayoutsAsync("PZB", "Combine"));
        Assert.False(await factory.WithMetadataDbContextAsync(async metadataDbContext => await metadataDbContext.MetadataKinds.AnyAsync(item => item.Kind == "Combine", Token)));
        Assert.Null(await AssembleAsync("PZB", "Combine"));
    }

    [Fact]
    public async Task Projection_RebuildIsIdempotent_AndClearsTheStaleMark()
    {
        const ulong First = 9180000;
        await ImportAsync(("PYA", ObjectDocs("PYA", First)), ("PYB", ObjectDocs("PYB", First)));
        AssertSucceeded(await RebuildAsync("Achievement"));

        MetadataKind first = await KindAsync("Achievement");
        Assert.Null(first.StaleSince);
        Assert.Equal(await ObjectCountAsync("Achievement"), (int)first.ObjectCount);
        string served = await BodyAsync("PYA", "Achievement");

        // 写正文的通道同时是打标的通道：导入一遍不改内容也要让读模型显式落后，
        // 否则过滤结果悄悄用的是旧骨架，而对外字节已经是新的了
        await ImportAsync(("PYA", ObjectDocs("PYA", First)));
        Assert.NotNull((await KindAsync("Achievement")).StaleSince);

        AssertSucceeded(await RebuildAsync("Achievement"));
        MetadataKind rebuilt = await KindAsync("Achievement");
        Assert.Null(rebuilt.StaleSince);
        Assert.Equal(await ObjectCountAsync("Achievement"), (int)rebuilt.ObjectCount);

        // 后台状态面板走的是同一个查询，这里连 DTO 一起验一遍：
        // RootShape 在库里是 varchar（MetadataDbContext 里 HasConversion<string>），空表上过一次不算数
        MetadataProjectionService.KindStatus status = Assert.Single(await KindsAsync(), item => item.Kind == "Achievement");
        Assert.Equal(MetadataRootShape.Array, status.RootShape);
        Assert.Equal(await ObjectCountAsync("Achievement"), (int)status.ObjectCount);
        Assert.Null(status.StaleSince);

        // 重建成败都不许碰对外服务的那张表
        Assert.Equal(served, await BodyAsync("PYA", "Achievement"));
        Assert.Equal(served, await AssembleAsync("PYA", "Achievement"));
    }

    private static void AssertSucceeded(MetadataProjectionService.Report report)
    {
        Assert.True(report.Succeeded, string.Join("; ", report.Issues.Select(issue => $"{issue.Kind}: {issue.Detail}")));
    }

    private async Task<MetadataProjectionService.Report> RebuildAsync(string kind)
    {
        CancellationToken token = Token;
        return await factory.WithMetadataDbContextAsync(async metadataDbContext =>
            await new MetadataProjectionService(metadataDbContext, TimeProvider.System).RebuildAsync(kind, null, token));
    }

    private async Task<string?> AssembleAsync(string locale, string name)
    {
        CancellationToken token = Token;
        return await factory.WithMetadataDbContextAsync(async metadataDbContext =>
            await new MetadataObjectAssembler(metadataDbContext).AssembleAsync(locale, name, token));
    }

    private async Task<MetadataObject> ObjectAsync(string kind, ulong gameId)
    {
        return await factory.WithMetadataDbContextAsync(async metadataDbContext => await metadataDbContext.MetadataObjects
            .AsNoTracking()
            .SingleAsync(item => item.Kind == kind && item.GameId == gameId && item.SubId == 0, Token));
    }

    private async Task<List<MetadataObject>> ObjectsAsync(string kind)
    {
        return await factory.WithMetadataDbContextAsync(async metadataDbContext => await metadataDbContext.MetadataObjects
            .AsNoTracking()
            .Where(item => item.Kind == kind)
            .ToListAsync(Token));
    }

    private async Task<int> ObjectCountAsync(string kind)
    {
        return await factory.WithMetadataDbContextAsync(async metadataDbContext => await metadataDbContext.MetadataObjects
            .AsNoTracking()
            .CountAsync(item => item.Kind == kind, Token));
    }

    private async Task<List<MetadataObjectI18n>> I18nsAsync(string kind, ulong gameId, string? locale = null)
    {
        return await factory.WithMetadataDbContextAsync(async metadataDbContext => await metadataDbContext.MetadataObjectI18ns
            .AsNoTracking()
            .Where(item => item.Kind == kind && item.GameId == gameId && (locale == null || item.Locale == locale))
            .OrderBy(item => item.Locale)
            .ThenBy(item => item.Field)
            .ToListAsync(Token));
    }

    private async Task<List<MetadataObjectLayout>> LayoutsAsync(string locale, string name)
    {
        return await factory.WithMetadataDbContextAsync(async metadataDbContext => await metadataDbContext.MetadataObjectLayouts
            .AsNoTracking()
            .Where(item => item.Locale == locale && item.Name == name)
            .OrderBy(item => item.Position)
            .ToListAsync(Token));
    }

    private async Task<List<MetadataObjectRef>> RefsAsync(string kind)
    {
        return await factory.WithMetadataDbContextAsync(async metadataDbContext => await metadataDbContext.MetadataObjectRefs
            .AsNoTracking()
            .Where(item => item.Kind == kind)
            .OrderBy(item => item.GameId)
            .ThenBy(item => item.Ordinal)
            .ToListAsync(Token));
    }

    private async Task<MetadataKind> KindAsync(string kind)
    {
        return await factory.WithMetadataDbContextAsync(async metadataDbContext => await metadataDbContext.MetadataKinds
            .AsNoTracking()
            .SingleAsync(item => item.Kind == kind, Token));
    }

    private async Task<List<MetadataProjectionService.KindStatus>> KindsAsync()
    {
        CancellationToken token = Token;
        return await factory.WithMetadataDbContextAsync(async metadataDbContext =>
            await new MetadataProjectionService(metadataDbContext, TimeProvider.System).ListKindsAsync(token));
    }

    /// <summary>
    /// 投影用例的 fixture。平铺分类的文档根必须是数组（注册表按 <c>Id</c> 认身份），散文件按目录名前缀认。
    /// 每个用例占一段没别人用过的 Id 区间：结构表的主键是全 locale 共用的，撞了 Id 就会把两个用例的对象并成一个。
    /// </summary>
    private static Doc[] ObjectDocs(string locale, ulong first) =>
    [
        new("Achievement", $$$"""[{"Id":{{{first}}},"Name":"成就 {{{locale}}}","Description":"{{{locale}}} 的描述","Progress":0,"Goal":{{{first + 1}}},"FinishReward":{"Id":{{{first + 9}}},"Count":2}},{"Id":{{{first + 1}}},"Name":"Second {{{locale}}}","Description":"Shared description","Progress":1,"Goal":{{{first + 1}}},"FinishReward":{"Id":{{{first + 9}}},"Count":2}}]"""),
        new("Weapon", $$$"""[{"Id":{{{first + 101}}},"Name":"{{{locale}}} Sword","Description":"A blade that never changes","Quality":4,"WeaponType":1}]"""),
        new($"Avatar/{first + 201}", $$$"""{"Id":{{{first + 201}}},"Name":"{{{locale}}}","Fetter":{"Title":"{{{locale}}} of Justice","Intro":"A fixed intro"}}"""),
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>一套固定的 fixture 文档；字符串叶子里带上 locale，好让跨语言比较天然有差异。</summary>
    private static Doc[] Docs(string locale) =>
    [
        new("Achievement", $$$"""{"1":{"Id":1,"Name":"成就 {{{locale}}}","Description":"{{{locale}}} 的描述","Progress":0},"2":{"Id":2,"Name":"Second {{{locale}}}","Description":"Shared description","Progress":1}}"""),
        new("Weapon", $$$"""[{"Id":11401,"Name":"{{{locale}}} Sword","Description":"A blade that never changes"}]"""),
        new("Avatar/10000002", $$$"""{"Id":10000002,"Name":"{{{locale}}}","Fetter":{"Title":"{{{locale}}} of Justice","Intro":"A fixed intro"}}"""),
    ];

    private static string DocJson(string locale, string name) => Docs(locale).First(doc => doc.Name == name).Json;

    private static string Canonical(string json) => MetadataHasher.Serialize(JsonNode.Parse(json)!);

    /// <summary>走真实的导入器：写一棵临时树、导进库、立刻删掉树。</summary>
    private async Task ImportAsync(params (string Locale, Doc[] Docs)[] trees)
    {
        using TempTree tree = TempTree.Create(trees);
        CancellationToken token = Token;
        await factory.WithMetadataDbContextAsync(async metadataDbContext =>
        {
            await MetadataTreeImport.ImportAsync(metadataDbContext, tree.Root, null, null, token);
        });
    }

    private async Task<MetadataTranslationService.ReapplyResult> ReapplyAsync(string locale)
    {
        CancellationToken token = Token;
        return await factory.WithMetadataDbContextAsync(async metadataDbContext =>
            await new MetadataTranslationService(metadataDbContext, new MetadataStoreService(metadataDbContext, TimeProvider.System), TimeProvider.System).ReapplyAsync(locale, token));
    }

    private async Task<TestResponse> SetTranslationAsync(string token, string locale, string name, string pointer, string value)
    {
        string url = $"/api/Service/Metadata/Translation?locale={locale}&name={Uri.EscapeDataString(name)}&pointer={Uri.EscapeDataString(pointer)}&value={Uri.EscapeDataString(value)}";
        return await SendServiceAsync("POST", url, token);
    }

    private async Task<string> BodyAsync(string locale, string name)
    {
        using HttpResponseMessage response = await client.GetAsync($"/metadata/Genshin/{locale}/{name}.json", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(Token);
    }

    private async Task<string> EtagAsync(string locale, string name)
    {
        using HttpResponseMessage response = await client.GetAsync($"/metadata/Genshin/{locale}/{name}.json", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return SingleHeader(response, "ETag");
    }

    private async Task<HttpResponseMessage> SendAsync(string url, string ifNoneMatch)
    {
        HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("If-None-Match", ifNoneMatch);
        return await client.SendAsync(request, Token);
    }

    /// <summary>响应体逐字节等于上游口径的规范化正文——启动器是原样写盘的。</summary>
    private async Task AssertServedAsync(string locale, string name, string json)
    {
        using HttpResponseMessage response = await client.GetAsync($"/metadata/Genshin/{locale}/{name}.json", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.ToString(), StringComparison.OrdinalIgnoreCase);

        string expected = Canonical(json);
        byte[] served = await response.Content.ReadAsByteArrayAsync(Token);

        // 先比文本（失败时看不出是缩进还是键序），再比字节（钉住没有 BOM、编码没换）
        Assert.Equal(expected, Encoding.UTF8.GetString(served));
        Assert.Equal(Encoding.UTF8.GetBytes(expected), served);
    }

    private static string SingleHeader(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out IEnumerable<string>? values))
        {
            return Assert.Single(values);
        }

        if (response.Content.Headers.TryGetValues(name, out values))
        {
            return Assert.Single(values);
        }

        Assert.Fail($"Response has no {name} header");
        return string.Empty;
    }

    /// <summary>响应外壳里值为 null 的成员整个键都不下发，这里统一按 null 读回来。</summary>
    private static string? Text(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out JsonElement value) ? value.GetString() : null;
    }

    private async Task<TestResponse> SendServiceAsync(string method, string url, string? token)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), url);
        request.WithToken(token);
        request.WithDevice();
        request.WithClientVersion();

        using HttpResponseMessage response = await client.SendAsync(request, Token);
        string body = await response.Content.ReadAsStringAsync(Token);
        Assert.True(response.StatusCode is not HttpStatusCode.InternalServerError, $"{method} {url} returned 500: {body}");
        return TestResponse.Parse(body);
    }

    private sealed record Doc(string Name, string Json);

    /// <summary>用完即删的临时目录树。落盘一律 CRLF，导入必须把 CR 剥干净。</summary>
    private sealed class TempTree : IDisposable
    {
        private static readonly UTF8Encoding Utf8NoBom = new(false);

        private TempTree(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TempTree Create(params (string Locale, Doc[] Docs)[] trees)
        {
            TempTree tree = new(Path.Combine(Path.GetTempPath(), $"hutao-metadata-{Guid.NewGuid():N}"));
            foreach ((string locale, Doc[] docs) in trees)
            {
                foreach (Doc doc in docs)
                {
                    tree.Write(locale, doc.Name, Canonical(doc.Json));
                }
            }

            return tree;
        }

        public void Write(string locale, string name, string text)
        {
            string file = FilePath(locale, name);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, text.Replace("\n", "\r\n", StringComparison.Ordinal), Utf8NoBom);
        }

        public string FilePath(string locale, string name)
        {
            return Path.Combine(Root, locale, name.Replace('/', Path.DirectorySeparatorChar) + ".json");
        }

        public void Dispose()
        {
            Directory.Delete(Root, true);
        }
    }

    /// <summary>
    /// 生产数据向量：上游 <c>CHS/HardChallengeSchedule.json</c> 去掉行尾 CR 之后的原文，
    /// 以及它在同目录 <c>Meta.json</c> 里记的哈希。改序列化写法（缩进、转义、大小写）时这条会先炸。
    /// </summary>
    private const string HardChallengeSchedule = """
        [
          {
            "Id": 5269001,
            "Begin": "2025-06-25T10:00:00+08:00",
            "End": "2025-08-06T09:59:59+08:00",
            "Name": "驱侵之役"
          },
          {
            "Id": 5269002,
            "Begin": "2025-08-06T10:00:00+08:00",
            "End": "2025-09-17T09:59:59+08:00",
            "Name": "劲烈之役"
          },
          {
            "Id": 5269003,
            "Begin": "2025-09-17T10:00:00+08:00",
            "End": "2025-10-29T09:59:59+08:00",
            "Name": "骇轰之役"
          },
          {
            "Id": 5269004,
            "Begin": "2025-10-29T10:00:00+08:00",
            "End": "2025-12-10T09:59:59+08:00",
            "Name": "迅悍之役"
          },
          {
            "Id": 5269005,
            "Begin": "2025-12-10T10:00:00+08:00",
            "End": "2026-01-21T09:59:59+08:00",
            "Name": "坠震之役"
          },
          {
            "Id": 5269006,
            "Begin": "2026-01-21T10:00:00+08:00",
            "End": "2026-03-04T09:59:59+08:00",
            "Name": "除晦之役"
          },
          {
            "Id": 5269007,
            "Begin": "2026-03-04T10:00:00+08:00",
            "End": "2026-04-15T09:59:59+08:00",
            "Name": "摧枯之役"
          },
          {
            "Id": 5269008,
            "Begin": "2026-04-15T10:00:00+08:00",
            "End": "2026-05-27T09:59:59+08:00",
            "Name": "断玉之役"
          },
          {
            "Id": 5269009,
            "Begin": "2026-05-27T10:00:00+08:00",
            "End": "2026-07-08T09:59:59+08:00",
            "Name": "啸卷之役"
          },
          {
            "Id": 5269010,
            "Begin": "2026-07-08T10:00:00+08:00",
            "End": "2026-08-19T09:59:59+08:00",
            "Name": "栗烈之役"
          },
          {
            "Id": 5269011,
            "Begin": "2026-08-19T10:00:00+08:00",
            "End": "2026-09-30T09:59:59+08:00",
            "Name": "星芒之役"
          },
          {
            "Id": 5269012,
            "Begin": "2026-09-30T10:00:00+08:00",
            "End": "2026-11-11T09:59:59+08:00",
            "Name": "烁煌之役"
          }
        ]
        """;
}
