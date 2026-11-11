namespace server.Core.Protobuf;

public readonly record struct ProtobufField(int Number, ProtobufWireKind Kind, ulong Value, byte[] Data)
{
    public uint UInt32 => checked((uint)Value);

    public string StringValue => Encoding.UTF8.GetString(Data);

    public List<ProtobufField> Message => ProtobufReader.Parse(Data);
}
