using server.Tests.Infrastructure;
using System.Net;

namespace server.Tests;

/// <summary>
/// Yae 成就注入配置：把 bucket 的 protobuf 转成启动器契约的过程。
/// 断言集中在两件事上——chinese/oversea 是否按 exe 指纹对号，以及响应体的键名
/// 是否与 <c>Snap.Hutao.Service.Yae.Achievement</c> 的 required 成员完全一致
/// （少一个键或多一个键都会让客户端整体反序列化失败）。
/// </summary>
[Collection(HutaoServerCollection.Name)]
public sealed class YaeTests
{
    /// <summary>appsettings.json 的 App:Yae:ChineseGameHashes 里配置的国服指纹。</summary>
    private const uint ConfiguredChineseHash = 3851921252U;

    private const uint OverseaHash = 0x77777777U;

    private const uint UnconfiguredHashA = 0xAAAAAAABU;

    private const uint UnconfiguredHashB = 0xCCCCCCCDU;

    private static readonly string[] RvaNames = ["doCmd", "updateNormalProp", "newString", "findGameObject", "eventSystemUpdate", "simulatePointerClick", "toInt32", "tcpStatePtr", "sharedInfoPtr", "decompress"];

    private readonly HutaoServerFactory factory;
    private readonly HttpClient client;
    private readonly TestUsers users;

    public YaeTests(HutaoServerFactory factory)
    {
        this.factory = factory;
        client = factory.CreateApiClient();
        users = factory.Users;
    }

    [Fact]
    public async Task Sync_WithConfiguredChineseHash_LabelsRvaGroupsAndServesTheClientContract()
    {
        ServeBucket(Bucket("9.9.9", (ConfiguredChineseHash, RvaGroup(1001U)), (OverseaHash, RvaGroup(2001U))));

        TestUser maintainer = await users.MaintainerAsync();
        Assert.Equal(ReturnCodes.Success, (await SendServiceAsync("POST", "/Service/Yae/SyncMetadata", maintainer.AccessToken)).Retcode);

        using HttpResponseMessage response = await client.GetAsync("/client/AchievementFieldId_9.9.9.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.ToString(), StringComparison.OrdinalIgnoreCase);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;

        AssertNames(root, ["currentProgress", "finishTimestamp", "id", "nativeConfig", "status", "totalProgress"]);
        Assert.Equal(11U, root.GetProperty("id").GetUInt32());
        Assert.Equal(3U, root.GetProperty("status").GetUInt32());
        Assert.Equal(12U, root.GetProperty("currentProgress").GetUInt32());
        Assert.Equal(10U, root.GetProperty("totalProgress").GetUInt32());
        Assert.Equal(14U, root.GetProperty("finishTimestamp").GetUInt32());

        JsonElement nativeConfig = root.GetProperty("nativeConfig");
        AssertNames(nativeConfig, ["achievementCmdId", "methodRva", "storeCmdId"]);
        Assert.Equal(22160U, nativeConfig.GetProperty("storeCmdId").GetUInt32());
        Assert.Equal(24317U, nativeConfig.GetProperty("achievementCmdId").GetUInt32());

        JsonElement methodRva = nativeConfig.GetProperty("methodRva");
        AssertNames(methodRva, ["chinese", "oversea"]);
        AssertNames(methodRva.GetProperty("chinese"), RvaNames);
        AssertNames(methodRva.GetProperty("oversea"), RvaNames);

        // proto 字段号 1、3..11 依次对应 RvaNames，组内值按写入顺序递增
        for (int index = 0; index < RvaNames.Length; ++index)
        {
            Assert.Equal((uint)(1001 + index), methodRva.GetProperty("chinese").GetProperty(RvaNames[index]).GetUInt32());
            Assert.Equal((uint)(2001 + index), methodRva.GetProperty("oversea").GetProperty(RvaNames[index]).GetUInt32());
        }
    }

    [Fact]
    public async Task Sync_ForcedChineseHash_RelabelsTheGroupThatTheConfigWouldNotPick()
    {
        // 两个 key 都不在配置里，只能靠 chineseHash 参数指定；指定哪个，哪个就写进 chinese
        ServeBucket(Bucket("8.8.8", (UnconfiguredHashA, RvaGroup(3001U)), (UnconfiguredHashB, RvaGroup(4001U))));

        TestUser maintainer = await users.MaintainerAsync();
        TestResponse sync = await SendServiceAsync("POST", $"/Service/Yae/SyncMetadata?chineseHash={UnconfiguredHashB}", maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, sync.Retcode);

        using HttpResponseMessage response = await client.GetAsync("/client/AchievementFieldId_8.8.8.json", TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement methodRva = document.RootElement.GetProperty("nativeConfig").GetProperty("methodRva");

        Assert.Equal(4001U, methodRva.GetProperty("chinese").GetProperty("doCmd").GetUInt32());
        Assert.Equal(3001U, methodRva.GetProperty("oversea").GetProperty("doCmd").GetUInt32());
    }

    [Fact]
    public async Task Sync_WithNoWayToTellTheChineseBuild_FailsInsteadOfGuessing()
    {
        ServeBucket(Bucket("7.7.7", (UnconfiguredHashA, RvaGroup(5001U)), (UnconfiguredHashB, RvaGroup(6001U))));

        TestUser maintainer = await users.MaintainerAsync();
        TestResponse sync = await SendServiceAsync("POST", "/Service/Yae/SyncMetadata", maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InternalStateException, sync.Retcode);
        Assert.Contains("ChineseGameHashes", sync.Message, StringComparison.Ordinal);

        using HttpResponseMessage response = await client.GetAsync("/client/AchievementFieldId_7.7.7.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Sync_BucketUnavailableFromEveryMirror_ReportsTheFailure()
    {
        factory.StubHttp.Map(
            request => request.RequestUri?.ToString().Contains("schicksal/metadata", StringComparison.Ordinal) is true,
            _ => new HttpResponseMessage(HttpStatusCode.BadGateway));

        TestUser maintainer = await users.MaintainerAsync();
        TestResponse sync = await SendServiceAsync("POST", "/Service/Yae/SyncMetadata", maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InternalStateException, sync.Retcode);
        Assert.Contains("bucket mirrors failed", sync.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sync_BadChineseHashParameter_IsRejectedAsInvalidQuery()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestResponse sync = await SendServiceAsync("POST", "/Service/Yae/SyncMetadata?chineseHash=not-a-hash", maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidQueryString, sync.Retcode);
    }

    [Fact]
    public async Task List_ContainsTheSyncedVersion()
    {
        ServeBucket(Bucket("6.6.6", (ConfiguredChineseHash, RvaGroup(7001U)), (OverseaHash, RvaGroup(8001U))));

        TestUser maintainer = await users.MaintainerAsync();
        Assert.Equal(ReturnCodes.Success, (await SendServiceAsync("POST", "/Service/Yae/SyncMetadata", maintainer.AccessToken)).Retcode);

        TestResponse list = await SendServiceAsync("GET", "/Service/Yae/Metadata", maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, list.Retcode);

        using JsonDocument document = JsonDocument.Parse(list.Data.GetRawText());
        JsonElement row = document.RootElement.EnumerateArray().Single(item => item.GetProperty("Version").GetString() == "6.6.6");
        Assert.Equal(ConfiguredChineseHash, row.GetProperty("ChineseHash").GetUInt32());
        Assert.Equal(OverseaHash, row.GetProperty("OverseaHash").GetUInt32());
        Assert.Equal(22160U, row.GetProperty("StoreCmdId").GetUInt32());
        Assert.Equal(2U, row.GetProperty("ItemCount").GetUInt32());
        Assert.Equal(2U, row.GetProperty("GroupCount").GetUInt32());
    }

    private static void AssertNames(JsonElement element, string[] expectedNames)
    {
        string[] actual = [.. element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];
        string[] expected = [.. expectedNames.Order(StringComparer.Ordinal)];
        Assert.Equal(expected, actual);
    }

    private static uint[] RvaGroup(uint first)
    {
        uint[] values = new uint[RvaNames.Length];
        for (int index = 0; index < values.Length; ++index)
        {
            values[index] = first + (uint)index;
        }

        return values;
    }

    /// <summary>按 AchievementInfo.proto 编码一份字段齐全的 bucket 数据。</summary>
    private static byte[] Bucket(string version, params (uint Hash, uint[] Rva)[] methodRva)
    {
        ProtoBuilder nativeConfig = new();
        nativeConfig.Varint(1, 22160U).Varint(2, 24317U);
        int[] rvaFieldNumbers = [1, 3, 4, 5, 6, 7, 8, 9, 10, 11];
        foreach ((uint hash, uint[] rva) in methodRva)
        {
            ProtoBuilder group = new();
            for (int index = 0; index < rvaFieldNumbers.Length; ++index)
            {
                group.Varint(rvaFieldNumbers[index], rva[index]);
            }

            nativeConfig.MapEntry(10, hash, group);
        }

        return new ProtoBuilder()
            .String(1, version)
            .MapEntry(2, 1U, "成就组一")
            .MapEntry(2, 2U, "成就组二")
            .MapEntry(3, 100U, new ProtoBuilder().Varint(1, 0U).Varint(2, 1U).String(3, "第一次").String(4, "描述"))
            .MapEntry(3, 101U, new ProtoBuilder().Varint(1, 100U).Varint(2, 1U).String(3, "第二次").String(4, "描述"))
            .Message(4, new ProtoBuilder().Varint(1, 11U).Varint(2, 3U).Varint(3, 10U).Varint(4, 12U).Varint(5, 14U))
            .Message(5, nativeConfig)
            .ToArray();
    }

    /// <summary>让 bucket 地址返回指定内容；桩规则后登记者优先，所以每个用例都会覆盖前一个。</summary>
    private void ServeBucket(byte[] payload)
    {
        factory.StubHttp.Map(
            request => request.RequestUri?.ToString().Contains("schicksal/metadata", StringComparison.Ordinal) is true,
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            });
    }

    private async Task<TestResponse> SendServiceAsync(string method, string url, string token)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), url);
        request.WithToken(token);
        request.WithDevice();
        request.WithClientVersion();

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode is not HttpStatusCode.InternalServerError, $"{method} {url} returned 500: {body}");
        return TestResponse.Parse(body);
    }

    /// <summary>proto3 wire format 的最小写入器，只用于造测试数据。</summary>
    private sealed class ProtoBuilder
    {
        private readonly List<byte> bytes = [];

        public ProtoBuilder Varint(int field, uint value)
        {
            WriteTag(field, 0);
            WriteVarint(value);
            return this;
        }

        public ProtoBuilder String(int field, string value)
        {
            return Bytes(field, Encoding.UTF8.GetBytes(value));
        }

        public ProtoBuilder Message(int field, ProtoBuilder value)
        {
            return Bytes(field, value.ToArray());
        }

        /// <summary>map 字段的一个 entry：外层字段号里裹「键=1，值=2」。</summary>
        public ProtoBuilder MapEntry(int field, uint key, ProtoBuilder value)
        {
            return Bytes(field, new ProtoBuilder().Varint(1, key).Message(2, value).ToArray());
        }

        public ProtoBuilder MapEntry(int field, uint key, string value)
        {
            return Bytes(field, new ProtoBuilder().Varint(1, key).String(2, value).ToArray());
        }

        public ProtoBuilder Bytes(int field, byte[] value)
        {
            WriteTag(field, 2);
            WriteVarint((uint)value.Length);
            bytes.AddRange(value);
            return this;
        }

        public byte[] ToArray()
        {
            return [.. bytes];
        }

        private void WriteTag(int field, int wireType)
        {
            WriteVarint((uint)((field << 3) | wireType));
        }

        private void WriteVarint(uint value)
        {
            while (value >= 0x80U)
            {
                bytes.Add((byte)(value | 0x80U));
                value >>= 7;
            }

            bytes.Add((byte)value);
        }
    }
}
