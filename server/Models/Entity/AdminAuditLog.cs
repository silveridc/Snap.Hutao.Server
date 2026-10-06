namespace server.Models.Entity
{
    [Table("admin_audit_logs")]
    public sealed class AdminAuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public int AdminUserId { get; set; }

        public string Action { get; set; } = default!;

        public string? Detail { get; set; }

        public bool Success { get; set; }

        public long Timestamp { get; set; }
    }
}
