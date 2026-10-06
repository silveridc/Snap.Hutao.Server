namespace server.Services.Expire
{
    public class TermExtendResult(TermExtendResultKind kind, DateTimeOffset expiredAt = default)
    {
        public TermExtendResultKind Kind { get; set; } = kind;

        public DateTimeOffset ExpiredAt { get; set; } = expiredAt;
    }
}