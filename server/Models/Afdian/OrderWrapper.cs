namespace server.Models.Afdian
{
    public sealed class OrderWrapper
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = default!;

        [JsonPropertyName("order")]
        public Order Order { get; set; } = default!;
    }
}