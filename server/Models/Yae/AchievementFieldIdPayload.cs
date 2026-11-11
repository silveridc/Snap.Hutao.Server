namespace server.Models.Yae;

public sealed class AchievementFieldIdPayload
{
    [JsonPropertyName("id")]
    public required uint Id
    {
        get; init;
    }

    [JsonPropertyName("status")]
    public required uint Status
    {
        get; init;
    }

    [JsonPropertyName("currentProgress")]
    public required uint CurrentProgress
    {
        get; init;
    }

    [JsonPropertyName("totalProgress")]
    public required uint TotalProgress
    {
        get; init;
    }

    [JsonPropertyName("finishTimestamp")]
    public required uint FinishTimestamp
    {
        get; init;
    }

    [JsonPropertyName("nativeConfig")]
    public required NativeConfigurationPayload NativeConfig
    {
        get; init;
    }
}

public sealed class NativeConfigurationPayload
{
    [JsonPropertyName("storeCmdId")]
    public required uint StoreCmdId
    {
        get; init;
    }

    [JsonPropertyName("achievementCmdId")]
    public required uint AchievementCmdId
    {
        get; init;
    }

    [JsonPropertyName("methodRva")]
    public required MethodRvaWrapperPayload MethodRva
    {
        get; init;
    }
}

public sealed class MethodRvaWrapperPayload
{
    /// <summary>Gets 国服 YuanShen.exe 的 RVA 组（按主 exe 前 64 KiB 的 CRC32 对号）.</summary>
    [JsonPropertyName("chinese")]
    public required MethodRvaPayload Chinese
    {
        get; init;
    }

    /// <summary>Gets 国际服 GenshinImpact.exe 的 RVA 组.</summary>
    [JsonPropertyName("oversea")]
    public required MethodRvaPayload Oversea
    {
        get; init;
    }
}

public sealed class MethodRvaPayload
{
    [JsonPropertyName("doCmd")]
    public required uint DoCmd
    {
        get; init;
    }

    [JsonPropertyName("updateNormalProp")]
    public required uint UpdateNormalProp
    {
        get; init;
    }

    [JsonPropertyName("newString")]
    public required uint NewString
    {
        get; init;
    }

    [JsonPropertyName("findGameObject")]
    public required uint FindGameObject
    {
        get; init;
    }

    [JsonPropertyName("eventSystemUpdate")]
    public required uint EventSystemUpdate
    {
        get; init;
    }

    [JsonPropertyName("simulatePointerClick")]
    public required uint SimulatePointerClick
    {
        get; init;
    }

    [JsonPropertyName("toInt32")]
    public required uint ToInt32
    {
        get; init;
    }

    [JsonPropertyName("tcpStatePtr")]
    public required uint TcpStatePtr
    {
        get; init;
    }

    [JsonPropertyName("sharedInfoPtr")]
    public required uint SharedInfoPtr
    {
        get; init;
    }

    [JsonPropertyName("decompress")]
    public required uint Decompress
    {
        get; init;
    }
}
