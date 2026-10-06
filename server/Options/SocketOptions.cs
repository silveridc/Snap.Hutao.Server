namespace server.Options
{
    public sealed class SocketOptions
    {
        public required bool Status { get; set; } = false;
        public required string Path { get; set; } = "/tmp/server.sock";
    }
}