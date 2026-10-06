namespace server.Models.Github
{
    public sealed class GithubUserResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("node_id")]
        public string NodeId { get; set; } = default!;

        [JsonPropertyName("login")]
        public string Login { get; set; } = default!;
    }
}