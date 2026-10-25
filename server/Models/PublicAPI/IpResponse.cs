namespace server.Models.PublicAPI;

/// <summary>
/// 返回ip的响应实体
/// </summary>
/// <param name="Ip">IP 地址。</param>
/// <param name="Country">国家。</param>
/// <param name="Province">省份。</param>
/// <param name="City">城市。</param>
/// <param name="Isp">运营商。</param>
public sealed record IpResponse(
    [property: JsonPropertyName("ip")] string Ip,
    [property: JsonPropertyName("country")] string Country,
    [property: JsonPropertyName("province")] string Province,
    [property: JsonPropertyName("city")] string City,
    [property: JsonPropertyName("isp")] string Isp);
