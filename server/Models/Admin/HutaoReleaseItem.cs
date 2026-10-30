namespace server.Models.Admin;

public sealed class HutaoReleaseItem
{
    public long Id
    {
        get; set;
    }

    public string Version { get; set; } = default!;

    public string Validation { get; set; } = default!;

    public string? MinimumVersion
    {
        get; set;
    }

    public List<global::server.Models.Patch.HutaoPackageMirror> Mirrors { get; set; } = [];

    public string? ReleaseNotes
    {
        get; set;
    }

    public bool IsActive
    {
        get; set;
    }

    public DateTimeOffset PublishedAt
    {
        get; set;
    }

    public int PublishedBy
    {
        get; set;
    }

    public string? PublishedByUserName
    {
        get; set;
    }
}
