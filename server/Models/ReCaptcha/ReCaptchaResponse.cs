namespace server.Models.ReCaptcha
{
    public class ReCaptchaResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("score")]
        public float Score { get; set; }

        [JsonPropertyName("action")]
        public string Action { get; set; } = default!;

        [JsonPropertyName("challenge_ts")]
        public string ChallengeTimeStamp { get; set; } = default!;

        [JsonPropertyName("hostname")]
        public string HostName { get; set; } = default!;
    }
}