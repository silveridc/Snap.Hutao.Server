using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace server.Models.Context.Configuration;

internal class JsonTextValueConverter<TProperty>() : ValueConverter<TProperty, string>(
        obj => JsonSerializer.Serialize(obj, default(JsonSerializerOptions)),
        str => JsonSerializer.Deserialize<TProperty>(str, default(JsonSerializerOptions))!)
{
}
