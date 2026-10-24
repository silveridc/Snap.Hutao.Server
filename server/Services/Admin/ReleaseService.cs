using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using server.Models.Admin;
using server.Models.Entity;
using server.Models.Patch;

namespace server.Services.Admin;

// Scoped
public sealed partial class ReleaseService(AppDbContext appDbContext, AppOptions appOptions, TimeProvider timeProvider, SystemConfigService systemConfig)
{
    private const int _maxMirrors = 20;
    private const int _maxUrlLength = 2048;

    public async Task<HutaoPackageInformation?> GetCurrentPackageAsync()
    {
        HutaoRelease? release = await appDbContext.HutaoReleases
            .AsNoTracking()
            .OrderByDescending(r => r.PublishedAt)
            .FirstOrDefaultAsync(r => r.IsActive)
            .ConfigureAwait(false);

        HutaoPackageInformation? info = release is not null
            ? release.ToPackageInformation()
            : (string.IsNullOrWhiteSpace(appOptions.Patch.Hutao.Version) ? null : appOptions.Patch.Hutao);

        // 熔断开关与具体版本无关（出问题随时关），从系统配置叠加，后台改动即时生效；
        // 只在熔断时下发 true（false 省略字段——客户端的非空 bool 收到 JSON null 会反序列化失败）
        if (info is not null)
        {
            info.FpsUnlockDisabled = systemConfig.PatchDisableFpsUnlock ? true : null;
            info.IslandInjectionDisabled = systemConfig.PatchDisableIslandInjection ? true : null;
        }

        return info;
    }

    public async Task<HutaoReleaseItem?> GetCurrentAsync()
    {
        HutaoRelease? release = await appDbContext.HutaoReleases
            .AsNoTracking()
            .OrderByDescending(r => r.PublishedAt)
            .FirstOrDefaultAsync(r => r.IsActive)
            .ConfigureAwait(false);
        return release is null ? null : await ToItemAsync(release).ConfigureAwait(false);
    }

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
        List<HutaoReleaseItem> items = releases.Select(r => ToItem(r, publisherNames.GetValueOrDefault(r.PublishedBy))).ToList();

        return new() { Total = total, Page = page, PageSize = pageSize, Items = items };
    }

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
        List<int> ids = publisherIds.Distinct().ToList();
        return await appDbContext.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.UserName)
            .ConfigureAwait(false);
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
}
