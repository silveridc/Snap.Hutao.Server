namespace server.Services
{
    public sealed class MailOptions
    {
        public required string Address { get; set; }

        public required string Subject { get; set; }

        public required string Title { get; set; }

        public required string Footer { get; set; }

        public required string RawContent { get; set; }
    }
}