using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace server.Models.Context.Configuration
{
    /// <summary>
    /// Json文本转换器
    /// </summary>
    /// <typeparam name="TProperty">实体类型</typeparam>
    internal class JsonTextValueConverter<TProperty>() : ValueConverter<TProperty, string>(
            obj => JsonSerializer.Serialize(obj, default(JsonSerializerOptions)),
            str => JsonSerializer.Deserialize<TProperty>(str, default(JsonSerializerOptions))!)
    {
    }
}