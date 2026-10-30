namespace server.Core.Protobuf;

public sealed class ProtobufReader(byte[] buffer)
{
    private int _position;

    public bool IsAtEnd => _position >= buffer.Length;

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
            if (_position >= buffer.Length)
            {
                throw new InvalidDataException("Truncated protobuf varint");
            }

            byte current = buffer[_position++];
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
        uint value = (uint)(buffer[_position] | (buffer[_position + 1] << 8) | (buffer[_position + 2] << 16) | (buffer[_position + 3] << 24));
        _position += 4;
        return value;
    }

    public ulong ReadFixed64()
    {
        EnsureAvailable(8);
        ulong value = 0UL;
        for (int index = 7; index >= 0; --index)
        {
            value = (value << 8) | buffer[_position + index];
        }

        _position += 8;
        return value;
    }

    public byte[] ReadLengthDelimited()
    {
        int length = checked((int)ReadVarint());
        EnsureAvailable(length);
        byte[] value = new byte[length];
        Array.Copy(buffer, _position, value, 0, length);
        _position += length;
        return value;
    }

    private void EnsureAvailable(int count)
    {
        if (buffer.Length - _position < count)
        {
            throw new InvalidDataException($"Protobuf message needs {count} more bytes than available");
        }
    }
}
