namespace server.Options
{
    public sealed class SmtpOptions
    {
        public string DiagnosticEmailAddress { get; set; } = default!;

        public string Password { get; set; } = default!;

        public string Server { get; set; } = default!;

        public string UserName { get; set; } = default!;
    }
}