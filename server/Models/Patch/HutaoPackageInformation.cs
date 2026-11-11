namespace server.Models.Patch;

public sealed class HutaoPackageInformation
{
    /// <summary>
    /// Gets or sets 最新可用版本，形如 1.17.2.0；客户端以 HutaoRuntime.Version 与其比较，较大才提示更新.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets 更新包的 SHA256 校验值，为空时客户端会提示校验信息缺失.
    /// </summary>
    [JsonPropertyName("validation")]
    public string Validation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets 最低可用版本.客户端版本低于它时强制更新（不可跳过），为 null 时不强制.
    /// </summary>
    [JsonPropertyName("minimumVersion")]
    public string? MinimumVersion
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets 熔断开关：禁用客户端的解锁帧率功能.true 时下发，false/未配置时省略字段；
    /// 客户端拿到 true 后本次会话内将解锁帧率整体视为关闭，用户本地设置保留、解除后自动恢复.
    /// </summary>
    [JsonPropertyName("fpsUnlockDisabled")]
    public bool? FpsUnlockDisabled
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets 熔断开关：禁用客户端的 Island 注入功能.语义同 <see cref="FpsUnlockDisabled"/>.
    /// </summary>
    [JsonPropertyName("islandInjectionDisabled")]
    public bool? IslandInjectionDisabled
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets 熔断原因：客户端在功能被禁用时展示给用户；未填写时省略，客户端使用默认提示.
    /// </summary>
    [JsonPropertyName("fpsUnlockDisabledReason")]
    public string? FpsUnlockDisabledReason
    {
        get; set;
    }

    /// <summary>
    /// Gets or sets 熔断原因：语义同 <see cref="FpsUnlockDisabledReason"/>.
    /// </summary>
    [JsonPropertyName("islandInjectionDisabledReason")]
    public string? IslandInjectionDisabledReason
    {
        get; set;
    }

    [JsonPropertyName("mirrors")]
    public List<HutaoPackageMirror> Mirrors { get; set; } = [];

    /// <summary>
    /// Gets or sets 更新说明（Markdown/纯文本），更新器窗口直接展示；未填写时为 null.
    /// </summary>
    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes
    {
        get; set;
    }
}
