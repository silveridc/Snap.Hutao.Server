namespace server.Models.Patch;

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
