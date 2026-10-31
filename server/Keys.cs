namespace server;

public static class Keys
{
    public const string PizzaHelperUidSalt = "PizzaHelper.UidSalt";
    public const string PizzaHelperApiSalt = "PizzaHelper.ApiSalt";
    public const string PizzaHelperAvatarHoldingEndpoint = "PizzaHelper.EndPoints.AvatarHolding";
    public const string PizzaHelperSpiralAbyssEndpoint = "PizzaHelper.EndPoints.SpiralAbyss";
    public const string AfdianSkuGachaLog = "Afdian.SkuGachaLogUploadService";
    public const string Afdian2SkuCdn = "Afdian2.SkuCdnService";
    public const string SmtpDiagnosticEmail = "Smtp.DiagnosticEmailAddress";
    public const string MaintenanceMode = "System.MaintenanceMode";
    public const string RegistrationEnabled = "System.RegistrationEnabled";
    public const string CaptchaEnabled = "System.CaptchaEnabled";
    public const string PatchDisableFpsUnlock = "Patch.DisableFpsUnlock";
    public const string PatchDisableIslandInjection = "Patch.DisableIslandInjection";
    public const string PatchDisableFpsUnlockReason = "Patch.DisableFpsUnlockReason";
    public const string PatchDisableIslandInjectionReason = "Patch.DisableIslandInjectionReason";
    public const string PatchS3Host = "Patch.S3.Host";
    public const string PatchS3Region = "Patch.S3.Region";
    public const string PatchS3AccessKeyId = "Patch.S3.AccessKeyId";
    public const string PatchS3SecretAccessKey = "Patch.S3.SecretAccessKey";
    public const string PatchS3ExpiresHours = "Patch.S3.ExpiresHours";
    public const string WallpaperTodayPool = "Wallpaper.Today.Pool";
    public const string MailTemplateHtml = "Mail.TemplateHtml";

    // 旧版单张配置，只作为壁纸池缺失时的回退，后台不再提供编辑入口
    public const string WallpaperTodayUrl = "Wallpaper.Today.Url";
    public const string WallpaperTodaySourceUrl = "Wallpaper.Today.SourceUrl";
    public const string WallpaperTodayAuthor = "Wallpaper.Today.Author";
    public const string WallpaperTodayUploader = "Wallpaper.Today.Uploader";
    public const string OidcEnabled = "Oidc.Enabled";
    public const string OidcAuthority = "Oidc.Authority";
    public const string OidcClientId = "Oidc.ClientId";
    public const string OidcClientSecret = "Oidc.ClientSecret";
    public const string OidcRedirectUri = "Oidc.RedirectUri";
    public const string OidcClientRedirectUri = "Oidc.ClientRedirectUri";
    public const string OidcScope = "Oidc.Scope";
    public const string OidcDisplayName = "Oidc.DisplayName";
    public const string OidcBootstrapClaim = "Oidc.BootstrapClaim";
    public const string OidcBootstrapValue = "Oidc.BootstrapValue";
    public const string OidcRequireHttpsMetadata = "Oidc.RequireHttpsMetadata";
}
