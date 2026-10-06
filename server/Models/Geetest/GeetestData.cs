namespace server.Models.Geetest
{
    internal sealed class GeetestData
    {
        [JsonPropertyName("gt")]
        public string Gt { get; set; } = default!;

        [JsonPropertyName("challenge")]
        public string Challenge { get; set; } = default!;

        [JsonPropertyName("validate")]
        public string Validate { get; set; } = default!;

        [JsonPropertyName("type")]
        public string Type { get; set; } = default!;
    }
}