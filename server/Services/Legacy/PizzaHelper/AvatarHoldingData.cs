namespace server.Services.Legacy.PizzaHelper
{
    internal sealed class AvatarHoldingData
    {
        [JsonPropertyName("uid")]
        public string Uid { get; set; } = default!;

        [JsonPropertyName("updateDate")]
        public string UpdateDate { get; set; } = default!;

        [JsonPropertyName("owningChars")]
        public List<int> OwningChars { get; set; } = default!;

        [JsonPropertyName("serverId")]
        public string ServerId { get; set; } = default!;
    }
}