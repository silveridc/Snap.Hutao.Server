namespace server.Models.Redeem
{
    public sealed class RedeemUseRequest
    {
        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; } = default!;
    }
}