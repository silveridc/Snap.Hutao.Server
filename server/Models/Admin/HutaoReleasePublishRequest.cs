namespace server.Models.Admin;

public sealed class HutaoReleasePublishRequest
{
    public string Version { get; set; } = default!;

    public string Validation { get; set; } = default!;

    /// <summary>Gets or sets 最低强制版本：客户端低于它时强制更新.可空，且不得大于 <see cref="Version"/>.</summary>
    public string? MinimumVersion
    {
        get; set;
    }

    public List<global::server.Models.Patch.HutaoPackageMirror> Mirrors { get; set; } = [];

    public string? ReleaseNotes
    {
        get; set;
    }
}
