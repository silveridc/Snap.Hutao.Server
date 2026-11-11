namespace server.Extension;

internal static class ClientIpExtension
{
    private const string _unknownIp = "unknown";

    /// <summary>
    /// 取本次请求的客户端 IP 字符串.RemoteIpAddress 已由 ForwardedHeaders 按 App:RealIp 配置还原，
    /// 反向代理后面拿到的是真实出口地址，而不是网关地址.
    /// </summary>
    public static string? GetClientIp(this HttpContext context)
    {
        IPAddress? ip = context.Connection.RemoteIpAddress;
        return ip is null ? null : Normalize(ip);
    }

    // IPv4 映射的 IPv6（::ffff:1.2.3.4）统一还原成 IPv4，避免日志里出现两种写法
    private static string Normalize(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        string text = ip.ToString();
        return text.Length is 0 ? _unknownIp : text;
    }
}
