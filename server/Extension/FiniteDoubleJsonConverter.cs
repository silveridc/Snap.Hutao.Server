using System.Globalization;

namespace server.Extension;

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
