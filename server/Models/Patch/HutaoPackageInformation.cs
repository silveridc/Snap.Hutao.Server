namespace server.Models.Patch
{
    /// <summary>
    /// /patch/hutao 返回的更新信息，字段与客户端 HutaoPackageInformation 对应。
    /// 同时作为配置绑定类型（App:Patch:Hutao）使用。
    /// </summary>
    public sealed class HutaoPackageInformation
    {
        /// <summary>
        /// 最新可用版本，形如 1.17.2.0；客户端以 HutaoRuntime.Version 与其比较，较大才提示更新。
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// 更新包的 SHA256 校验值，为空时客户端会提示校验信息缺失。
        /// </summary>
        [JsonPropertyName("validation")]
        public string Validation { get; set; } = string.Empty;

        [JsonPropertyName("mirrors")]
        public List<HutaoPackageMirror> Mirrors { get; set; } = [];
    }
}
