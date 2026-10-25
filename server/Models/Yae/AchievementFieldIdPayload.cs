namespace server.Models.Yae;

/// <summary>
/// <c>/client/AchievementFieldId_{version}.json</c> 的响应体。字段名必须与启动器
/// <c>Snap.Hutao.Service.Yae.Achievement</c> 下的模型逐一对应，且全部是必填：
/// 启动器用 <c>required</c> 成员反序列化，少任何一个键整个文件都会失败。
/// 返回的是裸对象，不带 retcode 信封。
/// </summary>
public sealed class AchievementFieldIdPayload
{
    [JsonPropertyName("id")]
    public required uint Id { get; init; }

    [JsonPropertyName("status")]
    public required uint Status { get; init; }

    [JsonPropertyName("currentProgress")]
    public required uint CurrentProgress { get; init; }

    [JsonPropertyName("totalProgress")]
    public required uint TotalProgress { get; init; }

    [JsonPropertyName("finishTimestamp")]
    public required uint FinishTimestamp { get; init; }

    [JsonPropertyName("nativeConfig")]
    public required NativeConfigurationPayload NativeConfig { get; init; }
}

public sealed class NativeConfigurationPayload
{
    [JsonPropertyName("storeCmdId")]
    public required uint StoreCmdId { get; init; }

    [JsonPropertyName("achievementCmdId")]
    public required uint AchievementCmdId { get; init; }

    [JsonPropertyName("methodRva")]
    public required MethodRvaWrapperPayload MethodRva { get; init; }
}

public sealed class MethodRvaWrapperPayload
{
    /// <summary>国服 YuanShen.exe 的 RVA 组（按主 exe 前 64 KiB 的 CRC32 对号）。</summary>
    [JsonPropertyName("chinese")]
    public required MethodRvaPayload Chinese { get; init; }

    /// <summary>国际服 GenshinImpact.exe 的 RVA 组。</summary>
    [JsonPropertyName("oversea")]
    public required MethodRvaPayload Oversea { get; init; }
}

/// <summary>字段名照 Yae 的 <c>MethodRvaConfig</c>，注入 DLL 通过命名管道按此顺序读取 10 个 RVA。</summary>
public sealed class MethodRvaPayload
{
    [JsonPropertyName("doCmd")]
    public required uint DoCmd { get; init; }

    [JsonPropertyName("updateNormalProp")]
    public required uint UpdateNormalProp { get; init; }

    [JsonPropertyName("newString")]
    public required uint NewString { get; init; }

    [JsonPropertyName("findGameObject")]
    public required uint FindGameObject { get; init; }

    [JsonPropertyName("eventSystemUpdate")]
    public required uint EventSystemUpdate { get; init; }

    [JsonPropertyName("simulatePointerClick")]
    public required uint SimulatePointerClick { get; init; }

    [JsonPropertyName("toInt32")]
    public required uint ToInt32 { get; init; }

    [JsonPropertyName("tcpStatePtr")]
    public required uint TcpStatePtr { get; init; }

    [JsonPropertyName("sharedInfoPtr")]
    public required uint SharedInfoPtr { get; init; }

    [JsonPropertyName("decompress")]
    public required uint Decompress { get; init; }
}
