namespace server.Services.Distribution
{
    public sealed class HutaoPackageMirror(string url, string mirrorName, string mirrorType)
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = url;

        [JsonPropertyName("mirror_name")]
        public string MirrorName { get; set; } = mirrorName;

        [JsonPropertyName("mirror_type")]
        public string MirrorType { get; set; } = mirrorType;
    }
}