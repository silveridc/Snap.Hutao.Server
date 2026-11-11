namespace server.Core.Protobuf;

public static class ProtobufFieldExtensions
{
    public static ProtobufField? Find(this List<ProtobufField> fields, int number)
    {
        return fields.FirstOrDefault(field => field.Number == number);
    }

    public static List<ProtobufField> All(this List<ProtobufField> fields, int number)
    {
        return [.. fields.Where(field => field.Number == number)];
    }

    /// <summary>取标量字段，缺失时返回默认值（proto3 的缺省语义）.</summary>
    public static uint UInt32(this List<ProtobufField> fields, int number, uint defaultValue = 0U)
    {
        return fields.Find(number) is { Kind: ProtobufWireKind.Varint or ProtobufWireKind.Fixed32 } field ? field.UInt32 : defaultValue;
    }

    public static string String(this List<ProtobufField> fields, int number, string defaultValue = "")
    {
        return fields.Find(number) is { Kind: ProtobufWireKind.LengthDelimited } field ? field.StringValue : defaultValue;
    }

    public static List<ProtobufField> Message(this List<ProtobufField> fields, int number)
    {
        return fields.Find(number) is { Kind: ProtobufWireKind.LengthDelimited } field ? field.Message : [];
    }

    /// <summary>把 map 字段的重复 entry 子消息展开成「键 → 值消息」.</summary>
    public static Dictionary<uint, List<ProtobufField>> Map(this List<ProtobufField> fields, int number)
    {
        Dictionary<uint, List<ProtobufField>> result = [];
        foreach (ProtobufField entry in fields.All(number))
        {
            List<ProtobufField> pair = entry.Message;
            uint key = pair.UInt32(1);
            result[key] = pair.Find(2) is { } value && value.Kind is ProtobufWireKind.LengthDelimited ? value.Message : [];
        }

        return result;
    }

    public static Dictionary<uint, string> StringMap(this List<ProtobufField> fields, int number)
    {
        Dictionary<uint, string> result = [];
        foreach (ProtobufField entry in fields.All(number))
        {
            List<ProtobufField> pair = entry.Message;
            result[pair.UInt32(1)] = pair.String(2);
        }

        return result;
    }
}
