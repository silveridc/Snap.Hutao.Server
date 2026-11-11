using Microsoft.EntityFrameworkCore.Storage;
using server.Models.Admin;
using server.Models.Entity;
using server.Models.Patch;
using System.Text.RegularExpressions;

namespace server.Services.Admin;

// Scoped
public sealed partial class ReleaseService(AppDbContext appDbContext, TimeProvider timeProvider, SystemConfigService systemConfig)
{
    private const int _maxMirrors = 20;
    private const int _maxUrlLength = 2048;

    /// <summary>
    /// Gets the package information of the active release, enriched with the current kill-switch settings.
    /// </summary>
    /// <returns>The active package information, or <see langword="null"/> when no release is available.</returns>
    public async Task<HutaoPackageInformation?> GetCurrentPackageAsync()
    {
        HutaoRelease? release = await appDbContext.HutaoReleases
            .AsNoTracking()
            .OrderByDescending(r => r.PublishedAt)
            .FirstOrDefaultAsync(r => r.IsActive)
            .ConfigureAwait(false);

        HutaoPackageInformation? info = release?.ToPackageInformation();

        // 熔断开关与具体版本无关（出问题随时关），从系统配置叠加，后台改动即时生效；
        // 只在熔断时下发 true（false 省略字段——客户端的非空 bool 收到 JSON null 会反序列化失败）
        if (info is not null)
        {
            bool disableFpsUnlock = systemConfig.PatchDisableFpsUnlock;
            bool disableIslandInjection = systemConfig.PatchDisableIslandInjection;
            info.FpsUnlockDisabled = disableFpsUnlock ? true : null;
            info.IslandInjectionDisabled = disableIslandInjection ? true : null;
            info.FpsUnlockDisabledReason = disableFpsUnlock && systemConfig.PatchDisableFpsUnlockReason.Length > 0
                ? systemConfig.PatchDisableFpsUnlockReason
                : null;
            info.IslandInjectionDisabledReason = disableIslandInjection && systemConfig.PatchDisableIslandInjectionReason.Length > 0
                ? systemConfig.PatchDisableIslandInjectionReason
                : null;
            SignS3Mirrors(info);
        }

        return info;
    }

    /// <summary>
    /// Gets the active release as an admin list item.
    /// </summary>
    /// <returns>The active release item, or <see langword="null"/> when no release is active.</returns>
    public async Task<HutaoReleaseItem?> GetCurrentAsync()
    {
        HutaoRelease? release = await appDbContext.HutaoReleases
            .AsNoTracking()
            .OrderByDescending(r => r.PublishedAt)
            .FirstOrDefaultAsync(r => r.IsActive)
            .ConfigureAwait(false);
        return release is null ? null : await ToItemAsync(release).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a page of releases ordered by publication time descending.
    /// </summary>
    /// <param name="page">The one-based page number; values below 1 are treated as 1.</param>
    /// <param name="pageSize">The page size, clamped to the range 1-100.</param>
    /// <returns>The requested page of releases.</returns>
    public async Task<PagedResult<HutaoReleaseItem>> GetListAsync(int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<HutaoRelease> query = appDbContext.HutaoReleases.AsNoTracking();
        long total = await query.LongCountAsync().ConfigureAwait(false);
        List<HutaoRelease> releases = await query
            .OrderByDescending(r => r.PublishedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync()
            .ConfigureAwait(false);
        Dictionary<int, string?> publisherNames = await GetPublisherNamesAsync(releases.Select(r => r.PublishedBy)).ConfigureAwait(false);
        List<HutaoReleaseItem> items = [.. releases.Select(r => ToItem(r, publisherNames.GetValueOrDefault(r.PublishedBy)))];

        return new()
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = items,
        };
    }

    /// <summary>
    /// Validates and publishes a new release, deactivating the previously active release in the same transaction.
    /// </summary>
    /// <param name="request">The publish request.</param>
    /// <param name="publisherId">The identifier of the administrator publishing the release.</param>
    /// <returns>
    /// A tuple containing whether the publish succeeded, a status message,
    /// the localization key of the message, and the created release item.
    /// </returns>
    public async Task<(bool Success, string Message, string? LocalizationKey, HutaoReleaseItem? Release)> PublishAsync(HutaoReleasePublishRequest request, int publisherId)
    {
        (string Message, string? LocalizationKey)? validationError = Validate(request, out string version, out string validation, out string? minimumVersion, out List<server.Models.Patch.HutaoPackageMirror> mirrors);
        if (validationError is { } error)
        {
            return (false, error.Message, error.LocalizationKey, null);
        }

        if (await appDbContext.HutaoReleases.AnyAsync(r => r.Version == version).ConfigureAwait(false))
        {
            return (false, "This version has already been published; activate a previous release to roll back", ServerKeys.ServerAdminReleaseAlreadyPublished, null);
        }

        await using IDbContextTransaction transaction = await appDbContext.Database.BeginTransactionAsync().ConfigureAwait(false);
        await appDbContext.HutaoReleases
            .Where(r => r.IsActive)
            .ExecuteUpdateAsync(update => update.SetProperty(r => r.IsActive, false))
            .ConfigureAwait(false);

        HutaoRelease release = new()
        {
            Version = version,
            Validation = validation,
            MinimumVersion = minimumVersion,
            MirrorsJson = JsonSerializer.Serialize(mirrors),
            ReleaseNotes = string.IsNullOrWhiteSpace(request.ReleaseNotes) ? null : request.ReleaseNotes.Trim(),
            IsActive = true,
            PublishedAt = timeProvider.GetUtcNow(),
            PublishedBy = publisherId,
        };

        appDbContext.HutaoReleases.Add(release);
        await appDbContext.SaveChangesAsync().ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);

        return (true, "Release published successfully", ServerKeys.ServerAdminReleasePublishSucceed, await GetByIdAsync(release.Id).ConfigureAwait(false));
    }

    /// <summary>
    /// Activates the specified release and deactivates every other release.
    /// </summary>
    /// <param name="id">The release identifier.</param>
    /// <returns>
    /// <see langword="true"/> if the release exists and was activated; otherwise, <see langword="false"/>.
    /// </returns>
    public async Task<bool> ActivateAsync(long id)
    {
        if (!await appDbContext.HutaoReleases.AnyAsync(r => r.Id == id).ConfigureAwait(false))
        {
            return false;
        }

        await using IDbContextTransaction transaction = await appDbContext.Database.BeginTransactionAsync().ConfigureAwait(false);
        await appDbContext.HutaoReleases
            .Where(r => r.IsActive && r.Id != id)
            .ExecuteUpdateAsync(update => update.SetProperty(r => r.IsActive, false))
            .ConfigureAwait(false);
        await appDbContext.HutaoReleases
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(update => update.SetProperty(r => r.IsActive, true))
            .ConfigureAwait(false);
        await transaction.CommitAsync().ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Deactivates the specified release.
    /// </summary>
    /// <param name="id">The release identifier.</param>
    /// <returns>
    /// <see langword="true"/> if an active release was deactivated; otherwise, <see langword="false"/>.
    /// </returns>
    public async Task<bool> DeactivateAsync(long id)
    {
        int updated = await appDbContext.HutaoReleases
            .Where(r => r.Id == id && r.IsActive)
            .ExecuteUpdateAsync(update => update.SetProperty(r => r.IsActive, false))
            .ConfigureAwait(false);
        return updated > 0;
    }

    private static HutaoReleaseItem ToItem(HutaoRelease release, string? publisherName)
    {
        return new()
        {
            Id = release.Id,
            Version = release.Version,
            Validation = release.Validation,
            MinimumVersion = release.MinimumVersion,
            Mirrors = JsonSerializer.Deserialize<List<server.Models.Patch.HutaoPackageMirror>>(release.MirrorsJson) ?? [],
            ReleaseNotes = release.ReleaseNotes,
            IsActive = release.IsActive,
            PublishedAt = release.PublishedAt,
            PublishedBy = release.PublishedBy,
            PublishedByUserName = publisherName,
        };
    }

    private static (string Message, string? LocalizationKey)? Validate(HutaoReleasePublishRequest request, out string version, out string validation, out string? minimumVersion, out List<server.Models.Patch.HutaoPackageMirror> mirrors)
    {
        version = request.Version?.Trim() ?? string.Empty;
        validation = request.Validation?.Trim().ToLowerInvariant() ?? string.Empty;
        mirrors = request.Mirrors ?? [];
        minimumVersion = string.IsNullOrWhiteSpace(request.MinimumVersion) ? null : request.MinimumVersion!.Trim();

        if (!VersionPattern().IsMatch(version) || !Version.TryParse(version, out _))
        {
            return ("Version must contain four numeric components, such as 1.17.2.0", ServerKeys.ServerAdminReleaseInvalidVersion);
        }

        if (minimumVersion is not null && (!VersionPattern().IsMatch(minimumVersion) || !Version.TryParse(minimumVersion, out _)))
        {
            return ("Minimum version must contain four numeric components, such as 1.17.2.0", ServerKeys.ServerAdminReleaseInvalidVersion);
        }

        if (minimumVersion is not null && Version.TryParse(minimumVersion, out Version? parsed) && Version.TryParse(version, out Version? current) && parsed > current)
        {
            return ("Minimum version must not be greater than the release version", ServerKeys.ServerAdminReleaseInvalidVersion);
        }

        if (!Sha256Pattern().IsMatch(validation))
        {
            return ("SHA-256 checksum must contain 64 hexadecimal characters", ServerKeys.ServerAdminReleaseInvalidChecksum);
        }

        if (mirrors.Count is < 1 or > _maxMirrors)
        {
            return ($"The number of download mirrors must be between 1 and {_maxMirrors}", null);
        }

        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (server.Models.Patch.HutaoPackageMirror mirror in mirrors)
        {
            mirror.MirrorName = mirror.MirrorName?.Trim() ?? string.Empty;
            mirror.Url = mirror.Url?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mirror.MirrorName) || mirror.MirrorName.Length > 100)
            {
                return ("Mirror name must not be empty or exceed 100 characters", ServerKeys.ServerAdminReleaseInvalidMirrorName);
            }

            if (!names.Add(mirror.MirrorName))
            {
                return ("Mirror names must be unique", ServerKeys.ServerAdminReleaseDuplicateMirrorName);
            }

            if (!Enum.IsDefined(mirror.MirrorType))
            {
                return ("Invalid mirror type", ServerKeys.ServerAdminReleaseInvalidMirrorType);
            }

            if (mirror.Url.Length > _maxUrlLength || !Uri.TryCreate(mirror.Url, UriKind.Absolute, out Uri? uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
            {
                return ("Mirror URL must be an absolute HTTPS URL without a username, password, or fragment", ServerKeys.ServerAdminReleaseInvalidMirrorUrl);
            }
        }

        return null;
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+\.\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    private async Task<HutaoReleaseItem?> GetByIdAsync(long id)
    {
        HutaoRelease? release = await appDbContext.HutaoReleases.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id).ConfigureAwait(false);
        return release is null ? null : await ToItemAsync(release).ConfigureAwait(false);
    }

    private async Task<HutaoReleaseItem> ToItemAsync(HutaoRelease release)
    {
        string? publisherName = await appDbContext.Users.AsNoTracking()
            .Where(u => u.Id == release.PublishedBy)
            .Select(u => u.UserName)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        return ToItem(release, publisherName);
    }

    private async Task<Dictionary<int, string?>> GetPublisherNamesAsync(IEnumerable<int> publisherIds)
    {
        List<int> ids = [.. publisherIds.Distinct()];
        return await appDbContext.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Replaces mirror URLs with time-limited presigned URLs when S3 signing is configured and the mirror host matches.
    /// The bucket stays private; every response is signed again, so clients download right away and expiry does not matter.
    /// </summary>
    /// <param name="info">The package information whose mirrors are signed in place.</param>
    private void SignS3Mirrors(HutaoPackageInformation info)
    {
        if (!systemConfig.IsPatchS3SigningEnabled)
        {
            return;
        }

        string host = systemConfig.PatchS3Host;
        int expiresHours = Math.Clamp(int.TryParse(systemConfig.GetOrDefault(Keys.PatchS3ExpiresHours, "24"), out int parsed) ? parsed : 24, 1, 168);
        string region = systemConfig.GetOrDefault(Keys.PatchS3Region, string.Empty).Trim();
        string accessKeyId = systemConfig.GetOrDefault(Keys.PatchS3AccessKeyId, string.Empty).Trim();
        string secretAccessKey = systemConfig.GetOrDefault(Keys.PatchS3SecretAccessKey, string.Empty);

        foreach (server.Models.Patch.HutaoPackageMirror mirror in info.Mirrors)
        {
            if (Uri.TryCreate(mirror.Url, UriKind.Absolute, out Uri? uri) &&
                uri.Authority is not null &&
                string.Equals(uri.Authority, host, StringComparison.OrdinalIgnoreCase))
            {
                mirror.Url = S3Presigner.Presign(mirror.Url, HttpMethod.Get, region, accessKeyId, secretAccessKey, expiresHours * 3600, timeProvider.GetUtcNow());
            }
        }
    }
}
