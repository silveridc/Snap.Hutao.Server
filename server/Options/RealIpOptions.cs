namespace server.Options
{
    public sealed class RealIpOptions
    {
        public bool Status { get; set; } = false;
        public string Header { get; set; } = "X-Real-IP";
    }
}