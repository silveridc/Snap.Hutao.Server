using System.Security.Cryptography;

namespace server.Models.Afdian;

public class QueryOrder
{
    [JsonPropertyName("user_id")]
    public string? UserId
    {
        get; set;
    }

    [JsonPropertyName("params")]
    public string? Params
    {
        get; set;
    }

    [JsonPropertyName("ts")]
    public long Ts
    {
        get; set;
    }

    [JsonPropertyName("sign")]
    public string? Sign
    {
        get; set;
    }

    public static QueryOrder Create(string userId, string tradeNumber, string token)
    {
        string param = JsonSerializer.Serialize(new Param() { OutTradeNo = tradeNumber });

        // Ts 与签名必须同值：以前各取各的 UtcNow，跨秒时刻签名和请求体不一致，爱发电验签偶发失败
        long ts = TimeProvider.System.GetUnixTimeSeconds();
        return new QueryOrder
        {
            UserId = userId,
            Params = param,
            Ts = ts,
            Sign = Signature(userId, token, param, ts),
        };
    }

    private static string Signature(string userId, string token, string param, long ts)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes($"{token}params{param}ts{ts}user_id{userId}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class Param
    {
        [JsonPropertyName("out_trade_no")]
        public string OutTradeNo { get; set; } = default!;
    }
}
