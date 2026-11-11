using server.Filter;
using server.Models.Admin;
using server.Models.Mail;
using server.Models.PublicAPI;
using server.Services.Admin;
using server.Services.Mail;
using server.Services.WallPaper;

namespace server.Controllers;

[Authorize]
[ApiController]
[Route("api/admin")]
[ServiceFilter(typeof(ValidateMaintainPermission), Order = 1)]
[ServiceFilter(typeof(ValidateAdminWritePermission), Order = 2)]
[ServiceFilter(typeof(AuditLogActionFilter), Order = 3)]
[ApiExplorerSettings(GroupName = "Services")]
public sealed class AdminSettingsController(SystemConfigService systemConfigService, CuratedWallpaper curatedWallpaper, MailService mailService, ILogger<AdminSettingsController> logger) : ControllerBase
{
    /// <summary>
    /// Gets the system configuration from the database, with sensitive values masked.
    /// </summary>
    /// <returns>The configuration.</returns>
    [HttpGet("settings")]
    public async Task<IActionResult> ConfigGetAsync()
    {
        Dictionary<string, string?> settings = await systemConfigService.GetAllAsync().ConfigureAwait(false);
        settings[Keys.OidcEnabled] = systemConfigService.Oidc.Enabled ? "true" : "false";
        settings[Keys.OidcRequireHttpsMetadata] = systemConfigService.Oidc.RequireHttpsMetadata ? "true" : "false";
        return Success<Dictionary<string, string?>>("Configuration retrieved successfully", settings, ServerKeys.ServerAdminConfigGetSucceed);
    }

    /// <summary>
    /// Partially updates the system configuration.
    /// </summary>
    /// <param name="settings">The configuration values to update.</param>
    /// <returns>The operation result.</returns>
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
    /// Deletes a stored configuration value so that the default takes effect again.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <returns>The operation result.</returns>
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
    /// Sends a fixed diagnostic email using the saved SMTP configuration; no verification code or reset link is issued.
    /// </summary>
    /// <param name="request">The recipient; the diagnostic address is used when it is left blank.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The delivery result, without SMTP credentials or server exception details.</returns>
    [HttpPost("mail/test")]
    public async Task<IActionResult> MailTestAsync([FromBody] MailTestRequest request, CancellationToken cancellationToken)
    {
        string address = string.IsNullOrWhiteSpace(request.Address) ? systemConfigService.SmtpDiagnosticEmail.Trim() : request.Address.Trim();
        if (!SystemSettingValidation.IsEmailAddress(address))
        {
            return Fail("A valid test recipient or diagnostic email address is required", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminMailTestAddressInvalid);
        }

        try
        {
            await mailService.SendDiagnosticMailAsync(address, cancellationToken).ConfigureAwait(false);
            return Success("Test email sent successfully", ServerKeys.ServerAdminMailTestSucceed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // SMTP 服务端响应可能包含用户名或凭据，日志和接口仅记录异常种类
            logger.LogWarning("SMTP diagnostic delivery failed ({ExceptionType})", ex.GetType().Name);
            return Fail("Unable to send the test email. Check the saved SMTP configuration", ReturnCode.InternalStateException, ServerKeys.ServerAdminMailTestFailed);
        }
    }

    /// <summary>
    /// Gets the mail template: the customized template when present, otherwise the built-in default.
    /// </summary>
    /// <returns>The mail template payload.</returns>
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
    /// Saves the mail template. After it is written to the database the cache is updated, so the next email uses it.
    /// </summary>
    /// <param name="request">The template to save.</param>
    /// <returns>The operation result.</returns>
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

        (bool success, string message) = await systemConfigService.UpdateAsync(
            new()
            {
                [Keys.MailTemplateHtml] = template,
            },
            this.GetUserIdOrZero()).ConfigureAwait(false);
        return success
            ? Success(message, ServerKeys.ServerAdminMailTemplateSaveSucceed)
            : Fail(message, ReturnCode.InvalidUploadData);
    }

    /// <summary>
    /// Deletes the customized mail template and restores the built-in default.
    /// </summary>
    /// <returns>The operation result.</returns>
    [HttpDelete("mail/template")]
    public async Task<IActionResult> MailTemplateResetAsync()
    {
        (bool success, string message) = await systemConfigService.DeleteAsync(Keys.MailTemplateHtml).ConfigureAwait(false);
        return success
            ? Success(message, ServerKeys.ServerAdminMailTemplateResetSucceed)
            : Fail(message, ReturnCode.NotFound, ServerKeys.ServerAdminMailTemplateNotFound);
    }

    /// <summary>
    /// Lists the mail kinds together with whether each language has been customized.
    /// </summary>
    /// <returns>The mail content summaries.</returns>
    [HttpGet("mail/contents")]
    public IActionResult MailContentsGet()
    {
        List<MailContentSummary> items = [.. MailContents.Kinds.Select(kind => new MailContentSummary
        {
            Kind = kind.Key,
            Languages = MailContents.Languages,
            Variables = kind.Variables,
            Customized = [.. MailContents.Languages.Select(language => !string.IsNullOrWhiteSpace(systemConfigService.Get(ContentKey(kind.Key, language))))],
        })];
        return Success<List<MailContentSummary>>("Mail contents retrieved successfully", items, ServerKeys.ServerAdminMailContentListSucceed);
    }

    /// <summary>
    /// Gets the content of one mail kind in one language; the built-in default is returned when it is not customized.
    /// </summary>
    /// <param name="kind">The mail content kind.</param>
    /// <param name="language">The language code.</param>
    /// <returns>The mail content payload.</returns>
    [HttpGet("mail/content/{kind}")]
    public IActionResult MailContentGet([FromRoute] string kind, [FromQuery] string language = "CHS")
    {
        if (!ValidateKindAndLanguage(kind, language, out MailContentKind? definition, out IActionResult? error))
        {
            return error!;
        }

        MailContent current = systemConfigService.GetMailContent(kind, language);
        return Success<MailContentPayload>(
            "Mail content retrieved successfully",
            new()
            {
                Kind = kind,
                Language = language,
                Variables = definition!.Variables,
                Subject = current.Subject,
                Title = current.Title,
                RawContent = current.RawContent,
                Footer = current.Footer,
                Customized = !string.IsNullOrWhiteSpace(systemConfigService.Get(ContentKey(kind, language))),
            },
            ServerKeys.ServerAdminMailContentGetSucceed);
    }

    /// <summary>
    /// Saves the content of one mail kind in one language; the next email uses it.
    /// </summary>
    /// <param name="kind">The mail content kind.</param>
    /// <param name="request">The mail content to save.</param>
    /// <returns>The operation result.</returns>
    [HttpPut("mail/content/{kind}")]
    public async Task<IActionResult> MailContentUpdateAsync([FromRoute] string kind, [FromBody] MailContentUpdateRequest request)
    {
        if (!ValidateKindAndLanguage(kind, request.Language ?? "CHS", out MailContentKind? definition, out IActionResult? error))
        {
            return error!;
        }

        string subject = request.Subject?.Trim() ?? string.Empty;
        string title = request.Title?.Trim() ?? string.Empty;
        string rawContent = request.RawContent?.Trim() ?? string.Empty;
        string footer = request.Footer?.Trim() ?? string.Empty;
        if (subject.Length is 0 || title.Length is 0 || rawContent.Length is 0 || footer.Length is 0)
        {
            return Fail("Mail subject, title, content and footer must not be empty", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminMailContentEmpty);
        }

        string? missing = definition!.Variables.FirstOrDefault((variable) => !rawContent.Contains($"{{{{{variable}}}}}", StringComparison.Ordinal));
        if (missing is not null)
        {
            return Fail($"Mail content must contain the variable {{{{{missing}}}}}", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminMailContentVariableRequired);
        }

        MailContent content = new()
        {
            Subject = subject,
            Title = title,
            RawContent = rawContent,
            Footer = footer,
        };
        (bool success, string message) = await systemConfigService.UpdateAsync(
            new()
            {
                [ContentKey(kind, request.Language ?? "CHS")] = JsonSerializer.Serialize(content),
            },
            this.GetUserIdOrZero()).ConfigureAwait(false);
        return success
            ? Success(message, ServerKeys.ServerAdminMailContentSaveSucceed)
            : Fail(message, ReturnCode.InvalidUploadData);
    }

    /// <summary>
    /// Deletes the customized content of one mail kind in one language and restores the built-in default.
    /// </summary>
    /// <param name="kind">The mail content kind.</param>
    /// <param name="language">The language code.</param>
    /// <returns>The operation result.</returns>
    [HttpDelete("mail/content/{kind}")]
    public async Task<IActionResult> MailContentResetAsync([FromRoute] string kind, [FromQuery] string language = "CHS")
    {
        if (!ValidateKindAndLanguage(kind, language, out _, out IActionResult? error))
        {
            return error!;
        }

        (bool success, string message) = await systemConfigService.DeleteAsync(ContentKey(kind, language)).ConfigureAwait(false);
        return success
            ? Success(message, ServerKeys.ServerAdminMailContentResetSucceed)
            : Fail(message, ReturnCode.NotFound, ServerKeys.ServerAdminMailContentNotFound);
    }

    /// <summary>
    /// Gets the today wallpaper pool; the legacy single-wallpaper configuration is returned as one entry.
    /// </summary>
    /// <returns>The wallpaper pool items.</returns>
    [HttpGet("wallpaper-pool")]
    public IActionResult WallpaperPoolGet()
    {
        return Success<List<WallpaperPoolItem>>("Wallpaper pool retrieved successfully", [.. curatedWallpaper.GetPool()]);
    }

    /// <summary>
    /// Saves the today wallpaper pool from which the client picks one wallpaper at random every day.
    /// </summary>
    /// <param name="items">The wallpaper pool items.</param>
    /// <returns>The operation result.</returns>
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

    /// <summary>
    /// Builds the configuration key of one mail content entry (format: MailContent.{kind}.{language}).
    /// </summary>
    /// <param name="kind">The mail content kind.</param>
    /// <param name="language">The language code.</param>
    /// <returns>The configuration key.</returns>
    private static string ContentKey(string kind, string language)
    {
        return $"MailContent.{kind}.{language}";
    }

    /// <summary>
    /// Validates the mail kind and language and resolves the kind definition.
    /// </summary>
    /// <param name="kind">The mail content kind.</param>
    /// <param name="language">The language code.</param>
    /// <param name="definition">The resolved kind definition when validation succeeds.</param>
    /// <param name="error">The error result when validation fails.</param>
    /// <returns><see langword="true"/> if both values are valid; otherwise, <see langword="false"/>.</returns>
    private static bool ValidateKindAndLanguage(string kind, string language, out MailContentKind? definition, out IActionResult? error)
    {
        definition = null;
        error = null;
        if (!MailContents.TryGet(kind, out MailContentKind? value))
        {
            error = Fail("Unknown mail kind", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminMailContentKindInvalid);
            return false;
        }

        if (!MailContents.Languages.Contains(language, StringComparer.Ordinal))
        {
            error = Fail("Unsupported mail language", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminMailContentLanguageInvalid);
            return false;
        }

        definition = value;
        return true;
    }
}
