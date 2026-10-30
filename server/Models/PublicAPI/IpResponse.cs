namespace server.Models.PublicAPI;

public sealed record IpResponse(
    [property: JsonPropertyName("ip")] string Ip,
    [property: JsonPropertyName("country")] string Country,
    [property: JsonPropertyName("province")] string Province,
    [property: JsonPropertyName("city")] string City,
    [property: JsonPropertyName("isp")] string Isp);
