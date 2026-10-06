namespace server.Models.PublicAPI;

/// <summary>
/// 返回ip的响应实体
/// </summary>
/// <param name="Ip"></param>
/// <param name="Country"></param>
/// <param name="City"></param>
/// <param name="Isp"></param>
public sealed record IpResponse(
    [property: JsonPropertyName("ip")] string Ip,
    [property: JsonPropertyName("country")] string Country,
    [property: JsonPropertyName("province")] string Province,
    [property: JsonPropertyName("city")] string City,
    [property: JsonPropertyName("isp")] string Isp);