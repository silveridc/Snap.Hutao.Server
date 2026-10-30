using IP2Region.Net.Abstractions;
using System.Net.Sockets;

namespace server.Services.PublicAPI;

public class IpRegionService([FromKeyedServices("IPv4DB")] ISearcher searcher, [FromKeyedServices("IPv6DB")] ISearcher ipv6Searcher, IDistributedCache cache)
{
    private const string _cacheKeyPrefix = "IpRegion:";

    private static readonly TimeSpan _cacheDuration = TimeSpan.FromDays(7);

    private static readonly string?[] _emptyRegion = [null, null, null, null];

    /// <summary>
    /// 取 IP 的属地四段数组（国家/省份/城市/运营商，缺段为 "0"），是唯一的属地入口：查到的结果（含查不到）按 IP 缓存到 Redis.
    /// IP 为空或非法、内网/回环等查不到的情况返回 null.
    /// </summary>
    public Task<string?[]?> GetRegionAsync(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out IPAddress? address))
        {
            return Task.FromResult<string?[]?>(null);
        }

        return cache.GetOrCreateAsync<string?[]?>($"{_cacheKeyPrefix}{ip}", _cacheDuration, () => Task.FromResult(_Lookup(address)));
    }

    private string?[]? _Lookup(IPAddress ip)
    {
        string?[] region = ip switch
        {
            _ when IPAddress.IsLoopback(ip) => _emptyRegion,
            { AddressFamily: AddressFamily.InterNetwork } => searcher.Search(ip)?.Split('|') ?? _emptyRegion,
            { AddressFamily: AddressFamily.InterNetworkV6 } => ipv6Searcher.Search(ip)?.Split('|') ?? _emptyRegion,
            _ => _emptyRegion,
        };

        return region.Length < 4 || string.IsNullOrWhiteSpace(region[0]) ? null : region[..4];
    }
}
