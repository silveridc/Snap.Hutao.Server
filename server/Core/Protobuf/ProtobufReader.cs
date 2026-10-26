namespace server.Core.Protobuf;

public sealed class ProtobufReader
{
    private readonly byte[] _buffer;
    private int _position;

    public ProtobufReader(byte[] buffer)
    {
        _buffer = buffer;
    }

    public bool IsAtEnd => _position >= _buffer.Length;

    public static List<ProtobufField> Parse(byte[] data)
    {
        ProtobufReader reader = new(data);
        List<ProtobufField> fields = [];
        while (!reader.IsAtEnd)
        {
            fields.Add(reader.ReadField());
        }

        return fields;
    }

    public ProtobufField ReadField()
    {
        ulong tag = ReadVarint();
        int number = (int)(tag >> 3);
        int wireType = (int)(tag & 0x7UL);
        return wireType switch
        {
            0 => new ProtobufField(number, ProtobufWireKind.Varint, ReadVarint(), []),
            1 => new ProtobufField(number, ProtobufWireKind.Fixed64, ReadFixed64(), []),
            2 => new ProtobufField(number, ProtobufWireKind.LengthDelimited, 0UL, ReadLengthDelimited()),
            5 => new ProtobufField(number, ProtobufWireKind.Fixed32, ReadFixed32(), []),
            _ => throw new InvalidDataException($"Unsupported protobuf wire type {wireType} on field {number}"),
        };
    }

    public ulong ReadVarint()
    {
        ulong result = 0UL;
        int shift = 0;
        while (true)
        {
            if (_position >= _buffer.Length)
            {
                throw new InvalidDataException("Truncated protobuf varint");
            }

            byte current = _buffer[_position++];
            result |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) is 0)
            {
                return result;
            }

            shift += 7;
            if (shift >= 64)
            {
                throw new InvalidDataException("Protobuf varint longer than 64 bits");
            }
        }
    }

    public uint ReadFixed32()
    {
        EnsureAvailable(4);
        uint value = (uint)(_buffer[_position] | (_buffer[_position + 1] << 8) | (_buffer[_position + 2] << 16) | (_buffer[_position + 3] << 24));
        _position += 4;
        return value;
    }

    public ulong ReadFixed64()
    {
        EnsureAvailable(8);
        ulong value = 0UL;
        for (int index = 7; index >= 0; --index)
        {
            value = (value << 8) | _buffer[_position + index];
        }

        _position += 8;
        return value;
    }

    public byte[] ReadLengthDelimited()
    {
        int length = checked((int)ReadVarint());
        EnsureAvailable(length);
        byte[] value = new byte[length];
        Array.Copy(_buffer, _position, value, 0, length);
        _position += length;
        return value;
    }

    private void EnsureAvailable(int count)
    {
        if (_buffer.Length - _position < count)
        {
            throw new InvalidDataException($"Protobuf message needs {count} more bytes than available");
        }
    }
}

public readonly record struct ProtobufField(int Number, ProtobufWireKind Kind, ulong Value, byte[] Data)
{
    public uint UInt32 => checked((uint)Value);

    public string StringValue => Encoding.UTF8.GetString(Data);

    public List<ProtobufField> Message => ProtobufReader.Parse(Data);
}

public enum ProtobufWireKind
{
    Varint,
    Fixed64,
    LengthDelimited,
    Fixed32,
}

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

    /// <summary>取标量字段，缺失时返回默认值（proto3 的缺省语义）。</summary>
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

    /// <summary>把 map 字段的重复 entry 子消息展开成「键 → 值消息」。</summary>
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
