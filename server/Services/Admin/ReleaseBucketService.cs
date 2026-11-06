using server.Models.Admin;
using server.Models.Patch;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace server.Services.Admin;

// Scoped
public sealed partial class ReleaseBucketService(
    IHttpClientFactory httpClientFactory,
    SystemConfigService systemConfig,
    ReleaseService releaseService,
    ILogger<ReleaseBucketService> logger)
{
    private const int _maxListedKeys = 200;
    private const int _maxKeyLength = 1024;
    private const int _presignSeconds = 600;

    // 元数据请求（列举与 HEAD）应在前端 20 秒超时内返回，不能让默认的 100 秒挂死
    private const int _metadataTimeoutSeconds = 15;

    public bool IsConfigured => systemConfig.IsPatchS3SigningEnabled;

    private string Host
    {
        get
        {
            string host = systemConfig.PatchS3Host;
            if (host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                host = host[8..];
            }

            return host.TrimEnd('/');
        }
    }

    public async Task<(ReleaseBucketListResult? Result, string Message, string? LocalizationKey)> ListAsync(string? prefix, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return (null, "Patch S3 storage is not configured", ServerKeys.ServerAdminReleaseBucketNotConfigured);
        }

        string normalizedPrefix = prefix?.Trim() ?? string.Empty;
        string url = $"https://{Host}/?list-type=2&prefix={Uri.EscapeDataString(normalizedPrefix)}&max-keys={_maxListedKeys}";

        using HttpClient client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(_metadataTimeoutSeconds);
        using HttpResponseMessage response = await client.GetAsync(Sign(url, HttpMethod.Get), cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Bucket list failed: HTTP {Status}", (int)response.StatusCode);
            return (null, "Patch storage bucket is unavailable", ServerKeys.ServerAdminReleaseBucketUnavailable);
        }

        string xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException exception)
        {
            logger.LogWarning(exception, "Bucket list returned malformed XML");
            return (null, "Patch storage bucket is unavailable", ServerKeys.ServerAdminReleaseBucketUnavailable);
        }

        if (document.Root is null)
        {
            return (null, "Patch storage bucket is unavailable", ServerKeys.ServerAdminReleaseBucketUnavailable);
        }

        List<ReleaseBucketObjectItem> objects = [];
        foreach (XElement element in document.Root.Elements().Where(e => e.Name.LocalName == "Contents"))
        {
            string? key = ChildValue(element, "Key");
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            objects.Add(new()
            {
                Key = key,
                Size = long.TryParse(ChildValue(element, "Size"), CultureInfo.InvariantCulture, out long size) ? size : 0,
                LastModified = DateTimeOffset.TryParse(ChildValue(element, "LastModified"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset modified) ? modified : default,
            });
        }

        return (new()
        {
            Host = Host,
            Prefix = normalizedPrefix,
            Truncated = string.Equals(ChildValue(document.Root, "IsTruncated"), "true", StringComparison.OrdinalIgnoreCase),
            Objects = objects,
        }, string.Empty, null);
    }

    public async Task<(bool Success, string Message, string? LocalizationKey, HutaoReleaseItem? Release)> PublishAsync(
        HutaoReleasePublishFromBucketRequest request,
        int publisherId,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return (false, "Patch S3 storage is not configured", ServerKeys.ServerAdminReleaseBucketNotConfigured, null);
        }

        string key = request.Key?.Trim() ?? string.Empty;
        if (!IsValidKey(key))
        {
            return (false, "Object key is required", ServerKeys.ServerAdminReleaseBucketObjectRequired, null);
        }

        string? version = string.IsNullOrWhiteSpace(request.Version) ? ParseVersion(key) : request.Version!.Trim();
        if (version is null || !VersionPattern().IsMatch(version))
        {
            return (false, "Version must contain four numeric components, such as 1.17.2.0", ServerKeys.ServerAdminReleaseInvalidVersion, null);
        }

        string? sha256;
        long length;
        if (!string.IsNullOrWhiteSpace(request.Validation))
        {
            string supplied = request.Validation.Trim();
            if (!Sha256Pattern().IsMatch(supplied))
            {
                return (false, "SHA-256 checksum must contain 64 hexadecimal characters", ServerKeys.ServerAdminReleaseInvalidChecksum, null);
            }

            (bool exists, long objectLength, string message, string? errorKey) = await TryHeadAsync(key, cancellationToken).ConfigureAwait(false);
            if (!exists)
            {
                return (false, message, errorKey, null);
            }

            sha256 = supplied.ToLowerInvariant();
            length = objectLength;
        }
        else
        {
            (string? hashed, long hashedLength, string message, string? errorKey) = await TryHashAsync(key, cancellationToken).ConfigureAwait(false);
            if (hashed is null)
            {
                return (false, message, errorKey, null);
            }

            sha256 = hashed;
            length = hashedLength;
        }

        HutaoReleasePublishRequest publish = new()
        {
            Version = version,
            Validation = sha256,
            MinimumVersion = request.MinimumVersion,
            ReleaseNotes = request.ReleaseNotes,
            Mirrors =
            [
                new()
                {
                    Url = BuildObjectUrl(key),
                    MirrorName = string.IsNullOrWhiteSpace(request.MirrorName) ? Host : request.MirrorName!.Trim(),
                    MirrorType = HutaoPackageMirrorType.Direct,
                },
            ],
        };

        (bool success, string publishMessage, string? localizationKey, HutaoReleaseItem? release) = await releaseService
            .PublishAsync(publish, publisherId)
            .ConfigureAwait(false);

        if (success)
        {
            logger.LogInformation("Published {Version} from bucket object {Key} ({Size} bytes, SHA-256 {Sha256})", version, key, length, sha256);
        }

        return (success, publishMessage, localizationKey, release);
    }

    private static bool IsValidKey(string key)
    {
        if (key.Length is 0 or > _maxKeyLength || key[0] == '/' || key.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        foreach (string segment in key.Split('/'))
        {
            if (segment is "" or "." or "..")
            {
                return false;
            }
        }

        return key.All(c => !char.IsControl(c));
    }

    private static string? ParseVersion(string key)
    {
        Match match = VersionPattern().Match(key.Split('/')[^1]);
        return match.Success ? match.Value : null;
    }

    private static string? ChildValue(XElement parent, string localName)
    {
        return parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;
    }

    [GeneratedRegex(@"\d+\.\d+\.\d+\.\d+", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    [GeneratedRegex("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Pattern();

    private async Task<(string? Sha256, long Length, string Message, string? LocalizationKey)> TryHashAsync(string key, CancellationToken cancellationToken)
    {
        using HttpClient client = httpClientFactory.CreateClient();

        // 安装包动辄几百 MiB，默认 100 秒的超时不够从桶里读完一遍.
        client.Timeout = TimeSpan.FromMinutes(30);
        using HttpResponseMessage response = await client
            .GetAsync(Sign(BuildObjectUrl(key), HttpMethod.Get), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Bucket object {Key} download failed: HTTP {Status}", key, (int)response.StatusCode);
            return response.StatusCode is HttpStatusCode.NotFound
                ? (null, 0, "Object does not exist in the storage bucket", ServerKeys.ServerAdminReleaseBucketObjectNotFound)
                : (null, 0, "Patch storage bucket is unavailable", ServerKeys.ServerAdminReleaseBucketUnavailable);
        }

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[81920];
        long length = 0L;
        int readBytes;
        while ((readBytes = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            hash.AppendData(buffer, 0, readBytes);
            length += readBytes;
        }

        return (Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), length, string.Empty, null);
    }

    private string BuildObjectUrl(string key)
    {
        string path = string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
        return $"https://{Host}/{path}";
    }

    private string Sign(string url, HttpMethod method)
    {
        return S3Presigner.Presign(
            url,
            method,
            systemConfig.GetOrDefault(Keys.PatchS3Region, string.Empty).Trim(),
            systemConfig.GetOrDefault(Keys.PatchS3AccessKeyId, string.Empty).Trim(),
            systemConfig.GetOrDefault(Keys.PatchS3SecretAccessKey, string.Empty),
            _presignSeconds);
    }

    private async Task<(bool Exists, long Length, string Message, string? LocalizationKey)> TryHeadAsync(string key, CancellationToken cancellationToken)
    {
        using HttpClient client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(_metadataTimeoutSeconds);
        using HttpResponseMessage response = await client
            .SendAsync(new HttpRequestMessage(HttpMethod.Head, Sign(BuildObjectUrl(key), HttpMethod.Head)), cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Bucket object {Key} HEAD failed: HTTP {Status}", key, (int)response.StatusCode);
            return response.StatusCode is HttpStatusCode.NotFound
                ? (false, 0, "Object does not exist in the storage bucket", ServerKeys.ServerAdminReleaseBucketObjectNotFound)
                : (false, 0, "Patch storage bucket is unavailable", ServerKeys.ServerAdminReleaseBucketUnavailable);
        }

        return (true, response.Content.Headers.ContentLength ?? 0, string.Empty, null);
    }
}
