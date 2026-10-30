using server.Filter;
using server.Models.PublicAPI;
using server.Services.Admin;
using server.Services.WallPaper;

namespace server.Controllers;

[Authorize]
[ApiController]
[Route("api/admin")]
[ServiceFilter(typeof(ValidateMaintainPermission), Order = 1)]
[ServiceFilter(typeof(ValidateAdminWritePermission), Order = 2)]
[ServiceFilter(typeof(AuditLogActionFilter), Order = 3)]
[ApiExplorerSettings(GroupName = "Services")]
public sealed class AdminSettingsController(SystemConfigService systemConfigService, CuratedWallpaper curatedWallpaper) : ControllerBase
{
    /// <summary>
    /// 获取所有系统配置项（DB 覆盖值）。
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> ConfigGetAsync()
    {
        Dictionary<string, string?> settings = await systemConfigService.GetAllAsync().ConfigureAwait(false);
        settings[Keys.OidcEnabled] = systemConfigService.Oidc.Enabled ? "true" : "false";
        settings[Keys.OidcRequireHttpsMetadata] = systemConfigService.Oidc.RequireHttpsMetadata ? "true" : "false";
        return Success<Dictionary<string, string?>>("Configuration retrieved successfully", settings, ServerKeys.ServerAdminConfigGetSucceed);
    }

    /// <summary>
    /// 批量更新系统配置项。
    /// </summary>
    [HttpPatch("settings")]
    public async Task<IActionResult> ConfigUpdateAsync([FromBody] Dictionary<string, string?> settings)
    {
        if (settings.Count == 0)
        {
            return Fail("No configuration changes provided", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminConfigUpdateEmpty);
        }

        (bool success, string message) = await systemConfigService.UpdateAsync(settings, this.GetUserIdOrZero()).ConfigureAwait(false);
        return success
            ? Success(message, ServerKeys.ServerAdminConfigUpdateSucceed)
            : Fail(message, ReturnCode.InvalidUploadData);
    }

    /// <summary>
    /// 删除配置项，恢复为 appsettings.json 默认值。
    /// </summary>
    [HttpDelete("settings/{key}")]
    public async Task<IActionResult> ConfigResetAsync([FromRoute] string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Fail("Configuration key must not be empty", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminConfigKeyRequired);
        }

        (bool success, string message) = await systemConfigService.DeleteAsync(key).ConfigureAwait(false);
        return success
            ? Success(message, ServerKeys.ServerAdminConfigResetSucceed)
            : Fail(message, ReturnCode.NotFound, ServerKeys.ServerAdminConfigNotFound);
    }

    /// <summary>
    /// 获取今日壁纸池，旧版单张配置会自动作为一条记录返回。
    /// </summary>
    [HttpGet("wallpaper-pool")]
    public IActionResult WallpaperPoolGet()
    {
        return Success<List<WallpaperPoolItem>>("Wallpaper pool retrieved successfully", [.. curatedWallpaper.GetPool()]);
    }

    /// <summary>
    /// 保存今日壁纸池，客户端每天从池中随机取一张。
    /// </summary>
    [HttpPut("wallpaper-pool")]
    public async Task<IActionResult> WallpaperPoolUpdateAsync([FromBody] List<WallpaperPoolItem>? items)
    {
        if (items is null)
        {
            return Fail("Wallpaper pool payload is required", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminWallpaperPoolInvalid);
        }

        (bool success, string message) = await curatedWallpaper.SavePoolAsync(items, this.GetUserIdOrZero()).ConfigureAwait(false);
        return success
            ? Success(message, ServerKeys.ServerAdminWallpaperPoolSaveSucceed)
            : Fail(message, ReturnCode.InvalidUploadData, ServerKeys.ServerAdminWallpaperPoolInvalid);
    }
}
