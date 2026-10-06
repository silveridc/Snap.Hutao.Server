using IP2Region.Net.Abstractions;
using System.Net.Sockets;
namespace server.Services.PublicAPI;

public class IpRegionService([FromKeyedServices("IPv4DB")] ISearcher searcher, [FromKeyedServices("IPv6DB")] ISearcher ipv6Searcher)
{
    private static readonly string?[] EmptyRegion = [null, null, null, null];
    public string?[] GetRegion(IPAddress clientIp)
    {
        return clientIp switch
        {
            _ when IPAddress.IsLoopback(clientIp) => EmptyRegion,
            { AddressFamily: AddressFamily.InterNetwork } => searcher.Search(clientIp)?.Split('|') ?? EmptyRegion,
            { AddressFamily: AddressFamily.InterNetworkV6 } => ipv6Searcher.Search(clientIp)?.Split('|') ?? EmptyRegion,
            _ => EmptyRegion
        };
    }
}
