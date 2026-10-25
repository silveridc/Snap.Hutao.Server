using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace server.Services.Admin;

/// <summary>
/// S3 SigV4 查询串预签名（GET）。桶保持私有，/Patch/hutao 下发前现场签发限时 URL——
/// 每次响应都是新签名，客户端拿到即下载，过期不影响。AWS S3 与 S3 兼容存储（R2/MinIO/OSS 等）通用。
/// 纯 HMAC-SHA256 链实现，不引入 AWS SDK（AGENTS.md：新增 NuGet 依赖需随包部署 DLL）。
/// </summary>
public static class S3Presigner
{
    /// <summary>
    /// 生成预签名 URL。不修改原路径与查询，只追加 X-Amz-* 参数与签名。
    /// </summary>
    /// <param name="objectUrl">对象的 https 绝对地址（虚拟主机风格或路径风格均可）。</param>
    /// <param name="region">区域名，如 us-east-1。</param>
    /// <param name="accessKeyId">AccessKeyId。</param>
    /// <param name="secretAccessKey">SecretAccessKey。</param>
    /// <param name="expiresSeconds">有效期（秒），S3 上限 7 天。</param>
    /// <param name="now">签名时刻（UTC），测试可注入。</param>
    /// <returns>带 X-Amz-Signature 的完整 URL。</returns>
    public static string PresignGet(string objectUrl, string region, string accessKeyId, string secretAccessKey, int expiresSeconds, DateTimeOffset? now = null)
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
        string canonicalRequest = $"GET\n{canonicalUri}\n{canonicalQuery}\nhost:{uri.Authority.ToLowerInvariant()}\n\nhost\nUNSIGNED-PAYLOAD";
        string scope = $"{dateStamp}/{region}/s3/aws4_request";
        string stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{scope}\n{Sha256Hex(Encoding.UTF8.GetBytes(canonicalRequest))}";

        byte[] signingKey = Hmac(Hmac(Hmac(Hmac(Encoding.UTF8.GetBytes("AWS4" + secretAccessKey), Encoding.UTF8.GetBytes(dateStamp)), Encoding.UTF8.GetBytes(region)), Encoding.UTF8.GetBytes("s3")), Encoding.UTF8.GetBytes("aws4_request"));
        string signature = Convert.ToHexString(Hmac(signingKey, Encoding.UTF8.GetBytes(stringToSign))).ToLowerInvariant();

        return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}?{canonicalQuery}&X-Amz-Signature={signature}";
    }

    /// <summary>S3 规范路径：每段先解再按未保留字符集编码，'/' 保留。</summary>
    private static string CanonicalizePath(string absolutePath)
    {
        return string.Join('/', absolutePath.Split('/').Select(Rfc3986EncodeSegment));
    }

    private static string Rfc3986EncodeSegment(string segment)
    {
        return Rfc3986Encode(Uri.UnescapeDataString(segment));
    }

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

    private static string Sha256Hex(byte[] data)
    {
        return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    }

    private static byte[] Hmac(byte[] key, byte[] data)
    {
        return HMACSHA256.HashData(key, data);
    }
}
