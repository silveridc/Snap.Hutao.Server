using server.Models.Patch;

namespace server.Options
{
    /// <summary>
    /// 补丁 / 更新相关配置，对应 appsettings 的 App:Patch 节点。
    /// </summary>
    public sealed class PatchOptions
    {
        /// <summary>
        /// Snap Hutao 客户端自更新信息，供 /patch/hutao 返回。
        /// </summary>
        public HutaoPackageInformation Hutao { get; set; } = new();
    }
}
