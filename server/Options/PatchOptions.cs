using server.Models.Patch;

namespace server.Options;

public sealed class PatchOptions
{
    /// <summary>
    /// Gets or sets snap Hutao 客户端自更新信息，供 /patch/hutao 返回.
    /// </summary>
    public HutaoPackageInformation Hutao { get; set; } = new();
}
