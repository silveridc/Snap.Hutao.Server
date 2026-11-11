using server.Core.Protobuf;
using server.Models.Yae;

namespace server.Services.Yae;

public static class YaeBucketDecoder
{
    // AchievementInfo.proto 的字段号
    private const int _versionField = 1;
    private const int _groupField = 2;
    private const int _itemField = 3;
    private const int _protoFieldInfoField = 4;
    private const int _nativeConfigField = 5;

    // NativeLibConfig 的字段号
    private const int _storeCmdIdField = 1;
    private const int _achievementCmdIdField = 2;
    private const int _methodRvaField = 10;

    // MethodRvaConfig 的字段号，与 MethodRvaPayload 的十个键一一对应
    private static readonly int[] RvaFieldNumbers = [1, 3, 4, 5, 6, 7, 8, 9, 10, 11];

    /// <summary>
    /// 解析 bucket 响应体：识别版本号与国服指纹，抽出成就字段号映射.
    /// </summary>
    /// <param name="data">bucket 响应体原文.</param>
    /// <param name="chineseHashes">已确认为国服的 exe 指纹集合；命中其中一个的 RVA 组写进 chinese.</param>
    public static Result Decode(byte[] data, IReadOnlyCollection<uint> chineseHashes)
    {
        List<ProtobufField> root = ProtobufReader.Parse(data);
        string version = root.String(_versionField);
        if (string.IsNullOrEmpty(version))
        {
            return Failed("Bucket data has no version field, refuse to import");
        }

        Dictionary<uint, string> groups = root.StringMap(_groupField);
        Dictionary<uint, List<ProtobufField>> items = root.Map(_itemField);
        List<ProtobufField> pbInfo = root.Message(_protoFieldInfoField);
        List<ProtobufField> nativeConfig = root.Message(_nativeConfigField);
        Dictionary<uint, List<ProtobufField>> methodRva = nativeConfig.Map(_methodRvaField);

        if (!TryClassify(methodRva.Keys, chineseHashes, out uint chinese, out uint oversea, out string? classifyError))
        {
            return Failed(classifyError!, version, (uint)items.Count, (uint)groups.Count);
        }

        AchievementFieldIdPayload payload = new()
        {
            Id = pbInfo.UInt32(1),
            Status = pbInfo.UInt32(2),
            TotalProgress = pbInfo.UInt32(3),
            CurrentProgress = pbInfo.UInt32(4),
            FinishTimestamp = pbInfo.UInt32(5),
            NativeConfig = new()
            {
                StoreCmdId = nativeConfig.UInt32(_storeCmdIdField),
                AchievementCmdId = nativeConfig.UInt32(_achievementCmdIdField),
                MethodRva = new()
                {
                    Chinese = ToPayload(methodRva[chinese]),
                    Oversea = ToPayload(methodRva[oversea]),
                },
            },
        };

        return new Result(payload, version, chinese, oversea, (uint)items.Count, (uint)groups.Count, null);
    }

    private static Result Failed(string error, string? version = null, uint itemCount = 0U, uint groupCount = 0U)
    {
        return new Result(null, version, 0U, 0U, itemCount, groupCount, error);
    }

    private static MethodRvaPayload ToPayload(List<ProtobufField> fields)
    {
        return new()
        {
            DoCmd = fields.UInt32(RvaFieldNumbers[0]),
            UpdateNormalProp = fields.UInt32(RvaFieldNumbers[1]),
            NewString = fields.UInt32(RvaFieldNumbers[2]),
            FindGameObject = fields.UInt32(RvaFieldNumbers[3]),
            EventSystemUpdate = fields.UInt32(RvaFieldNumbers[4]),
            SimulatePointerClick = fields.UInt32(RvaFieldNumbers[5]),
            ToInt32 = fields.UInt32(RvaFieldNumbers[6]),
            TcpStatePtr = fields.UInt32(RvaFieldNumbers[7]),
            SharedInfoPtr = fields.UInt32(RvaFieldNumbers[8]),
            Decompress = fields.UInt32(RvaFieldNumbers[9]),
        };
    }

    /// <summary>
    /// 按 exe 指纹给 chinese/oversea 对号.指纹是 <c>CRC32(主 exe 前 64 KiB)</c>（Yae 侧 <c>Utils.GetGameHash</c>），
    /// 与游戏版本号无关，所以每次大版本都可能变；判定不了就报错，绝不猜，因为错的 RVA 会让注入 DLL 直接跳飞.
    /// </summary>
    private static bool TryClassify(ICollection<uint> keys, IReadOnlyCollection<uint> chineseHashes, out uint chinese, out uint oversea, out string? error)
    {
        chinese = default;
        oversea = default;
        error = default;

        switch (keys.Count)
        {
            case 0:
                {
                    error = "Bucket data has no method_rva entry";
                    return false;
                }

            // 上游只发布了一组 RVA 时，两端只能共用同一组；多键时不允许这样退让.
            case 1:
                {
                    chinese = oversea = keys.First();
                    return true;
                }
        }

        if (chineseHashes.Count is 0)
        {
            error = "Cannot tell which method_rva key is the Chinese build: configure Yae.ChineseGameHashes in the admin panel or pass chineseHash";
            return false;
        }

        uint matched = 0U;
        foreach (uint candidate in chineseHashes)
        {
            // 0 保留作「未命中」哨兵，配置里出现 0 一律忽略（真实 CRC32 命中 0 的概率可忽略）
            if (candidate is not 0U && keys.Contains(candidate))
            {
                matched = candidate;
                break;
            }
        }

        if (matched is 0U)
        {
            error = $"None of the method_rva keys [{string.Join(", ", keys.Select(key => $"0x{key:X8}"))}] is in the configured Yae.ChineseGameHashes";
            return false;
        }

        chinese = matched;
        oversea = keys.First(key => key != matched);
        return true;
    }

    public sealed record Result(AchievementFieldIdPayload? Payload, string? Version, uint ChineseHash, uint OverseaHash, uint ItemCount, uint GroupCount, string? Error)
    {
        public bool Succeeded => Error is null;
    }
}
