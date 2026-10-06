namespace server.Models.PublicAPI;

public sealed record WallPaperResponse(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("source_url")] string SourceUrl,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("uploader")] string Uploader
);