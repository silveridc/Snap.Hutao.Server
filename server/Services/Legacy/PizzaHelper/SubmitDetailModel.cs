namespace server.Services.Legacy.PizzaHelper
{
    internal sealed class SubmitDetailModel
    {
        [JsonPropertyName("floor")]
        public int Floor { get; set; }

        [JsonPropertyName("room")]
        public int Room { get; set; }

        [JsonPropertyName("half")]
        public int Half { get; set; }

        [JsonPropertyName("usedChars")]
        public List<int> UsedChars { get; set; } = default!;
    }
}