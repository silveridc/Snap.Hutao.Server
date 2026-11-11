namespace server.Options;

public sealed class SocketOptions
{
    public bool Status { get; set; } = false;

    public string Path { get; set; } = "/tmp/server.sock";
}
