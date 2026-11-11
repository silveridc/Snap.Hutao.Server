using System.Globalization;
using System.Security.Cryptography;

namespace server.Services.Admin;

public static class S3Presigner
{
    /// <summary>
    /// Creates a presigned URL for the given object URL without modifying its path or query;
    /// only the X-Amz-* parameters and the signature are appended.
    /// </summary>
    /// <param name="objectUrl">The absolute HTTPS URL of the object, in either virtual-hosted or path-style form.</param>
    /// <param name="method">
    /// The HTTP method that will be used with the URL. The method name participates in the canonical request,
    /// so using another method after signing results in SignatureDoesNotMatch.
    /// </param>
    /// <param name="region">The region name, for example us-east-1.</param>
    /// <param name="accessKeyId">The S3 access key ID.</param>
    /// <param name="secretAccessKey">The S3 secret access key.</param>
    /// <param name="expiresSeconds">The validity period in seconds; S3 allows at most 7 days.</param>
    /// <param name="now">The signing time in UTC, injectable for testing.</param>
    /// <returns>The complete URL including the X-Amz-Signature parameter.</returns>
    public static string Presign(string objectUrl, HttpMethod method, string region, string accessKeyId, string secretAccessKey, int expiresSeconds, DateTimeOffset? now = null)
    {
        Uri uri = new(objectUrl, UriKind.Absolute);
        DateTimeOffset utcNow = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        string amzDate = utcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        string dateStamp = utcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        Dictionary<string, string> query = [];
        foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] kv = pair.Split('=', 2);
            query[Uri.UnescapeDataString(kv[0])] = kv.Length is 2 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
        }

        query["X-Amz-Algorithm"] = "AWS4-HMAC-SHA256";
        query["X-Amz-Credential"] = $"{accessKeyId}/{dateStamp}/{region}/s3/aws4_request";
        query["X-Amz-Date"] = amzDate;
        query["X-Amz-Expires"] = expiresSeconds.ToString(CultureInfo.InvariantCulture);
        query["X-Amz-SignedHeaders"] = "host";

        // 规范查询串：键按字典序，值与键均按 RFC3986 未保留字符集编码（S3 要求 '/' 也编码为 %2F）
        string canonicalQuery = string.Join('&', query
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{Rfc3986Encode(kv.Key)}={Rfc3986Encode(kv.Value)}"));

        string canonicalUri = CanonicalizePath(uri.AbsolutePath);
        string canonicalRequest = $"{method.Method}\n{canonicalUri}\n{canonicalQuery}\nhost:{uri.Authority.ToLowerInvariant()}\n\nhost\nUNSIGNED-PAYLOAD";
        string scope = $"{dateStamp}/{region}/s3/aws4_request";
        string stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{scope}\n{Sha256Hex(Encoding.UTF8.GetBytes(canonicalRequest))}";

        byte[] signingKey = Hmac(Hmac(Hmac(Hmac(Encoding.UTF8.GetBytes("AWS4" + secretAccessKey), Encoding.UTF8.GetBytes(dateStamp)), Encoding.UTF8.GetBytes(region)), Encoding.UTF8.GetBytes("s3")), Encoding.UTF8.GetBytes("aws4_request"));
        string signature = Convert.ToHexString(Hmac(signingKey, Encoding.UTF8.GetBytes(stringToSign))).ToLowerInvariant();

        return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}?{canonicalQuery}&X-Amz-Signature={signature}";
    }

    /// <summary>
    /// Builds the canonical S3 path: each segment is decoded and then re-encoded with RFC 3986 unreserved characters, keeping '/'.
    /// </summary>
    /// <param name="absolutePath">The absolute path of the object URL.</param>
    /// <returns>The canonical path used by the signature calculation.</returns>
    private static string CanonicalizePath(string absolutePath)
    {
        return string.Join('/', absolutePath.Split('/').Select(Rfc3986EncodeSegment));
    }

    /// <summary>
    /// Decodes a single path segment and re-encodes it with RFC 3986 unreserved characters.
    /// </summary>
    /// <param name="segment">The raw path segment.</param>
    /// <returns>The re-encoded path segment.</returns>
    private static string Rfc3986EncodeSegment(string segment)
    {
        return Rfc3986Encode(Uri.UnescapeDataString(segment));
    }

    /// <summary>
    /// Percent-encodes a value using the RFC 3986 unreserved character set.
    /// </summary>
    /// <param name="value">The value to encode.</param>
    /// <returns>The encoded value.</returns>
    private static string Rfc3986Encode(string value)
    {
        StringBuilder builder = new();
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        foreach (byte b in bytes)
        {
            char c = (char)b;
            if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_' or '.' or '~')
            {
                builder.Append(c);
            }
            else
            {
                builder.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Computes the lowercase hexadecimal SHA-256 hash of the given data.
    /// </summary>
    /// <param name="data">The data to hash.</param>
    /// <returns>The lowercase hexadecimal hash string.</returns>
    private static string Sha256Hex(byte[] data)
    {
        return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    }

    /// <summary>
    /// Computes the HMAC-SHA256 of the given data with the given key.
    /// </summary>
    /// <param name="key">The HMAC key.</param>
    /// <param name="data">The data to sign.</param>
    /// <returns>The computed HMAC bytes.</returns>
    private static byte[] Hmac(byte[] key, byte[] data)
    {
        return HMACSHA256.HashData(key, data);
    }
}
