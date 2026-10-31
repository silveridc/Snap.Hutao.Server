using server.Filter;
using server.Models.Admin;
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
    /// get system config from db.
    /// </summary>
    /// <returns> config. </returns>
    [HttpGet("settings")]
    public async Task<IActionResult> ConfigGetAsync()
    {
        Dictionary<string, string?> settings = await systemConfigService.GetAllAsync().ConfigureAwait(false);
        settings[Keys.OidcEnabled] = systemConfigService.Oidc.Enabled ? "true" : "false";
        settings[Keys.OidcRequireHttpsMetadata] = systemConfigService.Oidc.RequireHttpsMetadata ? "true" : "false";
        return Success<Dictionary<string, string?>>("Configuration retrieved successfully", settings, ServerKeys.ServerAdminConfigGetSucceed);
    }

    /// <summary>
    /// patch update system config.
    /// </summary>
    /// <param name="settings">settings data.</param>
    /// <returns>data.</returns>
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
    /// delete settings.
    /// </summary>
    /// <param name="key">settingsKey.</param>
    /// <returns>data.</returns>
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
    /// get mail template.
    /// </summary>
    /// <returns>data.</returns>
    [HttpGet("mail/template")]
    public IActionResult MailTemplateGet()
    {
        string custom = systemConfigService.MailTemplateHtml;
        bool customized = !string.IsNullOrWhiteSpace(custom);
        return Success<MailTemplatePayload>(
            "Mail template retrieved successfully",
            new()
            {
                Template = customized ? custom : MailTemplates.DefaultHtml,
                DefaultTemplate = MailTemplates.DefaultHtml,
                Customized = customized,
            },
            ServerKeys.ServerAdminMailTemplateGetSucceed);
    }

    /// <summary>
    /// 保存邮件模板，写入数据库后缓存同步更新，下一封邮件即生效.
    /// </summary>
    [HttpPut("mail/template")]
    public async Task<IActionResult> MailTemplateUpdateAsync([FromBody] MailTemplateUpdateRequest request)
    {
        string template = request.Template?.Trim() ?? string.Empty;
        if (template.Length is 0)
        {
            return Fail("Mail template must not be empty", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminMailTemplateEmpty);
        }

        foreach (string placeholder in MailTemplates.Placeholders)
        {
            if (!template.Contains(placeholder, StringComparison.Ordinal))
            {
                return Fail($"Mail template must contain the placeholder {placeholder}", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminMailTemplatePlaceholderRequired);
            }
        }

        (bool success, string message) = await systemConfigService.UpdateAsync(new() { [Keys.MailTemplateHtml] = template }, this.GetUserIdOrZero()).ConfigureAwait(false);
        return success
            ? Success(message, ServerKeys.ServerAdminMailTemplateSaveSucceed)
            : Fail(message, ReturnCode.InvalidUploadData);
    }

    /// <summary>
    /// 删除自定义邮件模板，恢复内置默认模板.
    /// </summary>
    [HttpDelete("mail/template")]
    public async Task<IActionResult> MailTemplateResetAsync()
    {
        (bool success, string message) = await systemConfigService.DeleteAsync(Keys.MailTemplateHtml).ConfigureAwait(false);
        return success
            ? Success(message, ServerKeys.ServerAdminMailTemplateResetSucceed)
            : Fail(message, ReturnCode.NotFound, ServerKeys.ServerAdminMailTemplateNotFound);
    }

    /// <summary>
    /// 获取今日壁纸池，旧版单张配置会自动作为一条记录返回.
    /// </summary>
    [HttpGet("wallpaper-pool")]
    public IActionResult WallpaperPoolGet()
    {
        return Success<List<WallpaperPoolItem>>("Wallpaper pool retrieved successfully", [.. curatedWallpaper.GetPool()]);
    }

    /// <summary>
    /// 保存今日壁纸池，客户端每天从池中随机取一张.
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
