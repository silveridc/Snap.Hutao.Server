namespace server.Models.PublicAPI;

public sealed class WallpaperPoolItem
{
    public string Url { get; set; } = string.Empty;

    public string? SourceUrl { get; set; }

    public string? Author { get; set; }

    public string? Uploader { get; set; }
}
