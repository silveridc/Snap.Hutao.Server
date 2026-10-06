namespace server.Models.Patch
{
    /// <summary>
    /// 客户端更新包的下载镜像，字段与客户端 HutaoPackageMirror 对应。
    /// </summary>
    public sealed class HutaoPackageMirror
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("mirror_name")]
        public string MirrorName { get; set; } = string.Empty;

        [JsonPropertyName("mirror_type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HutaoPackageMirrorType MirrorType { get; set; }
    }
}
