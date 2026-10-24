using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace server.Extension;

/// <summary>
/// NaN/Infinity 的响应序列化兜底：JSON 标准没有这两个字面量，MVC 默认 options 写出时直接抛
/// （历史毒行读回后让 /Statistics/Overview 稳定 500）。写侧把非有限值归零——统计里的除零
/// 平均值没有可区分的含义；读侧对字符串形态的 NaN/Infinity 字面量按存储侧
/// AllowNamedFloatingPointLiterals 的语义接受，其余行为不变。
/// </summary>
public sealed class FiniteDoubleJsonConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType is JsonTokenType.String
            ? double.Parse(reader.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture)
            : reader.GetDouble();
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(double.IsFinite(value) ? value : 0);
    }
}

public sealed class FiniteSingleJsonConverter : JsonConverter<float>
{
    public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType is JsonTokenType.String
            ? float.Parse(reader.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture)
            : reader.GetSingle();
    }

    public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(float.IsFinite(value) ? value : 0);
    }
}
