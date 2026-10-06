namespace server.Models.Entity.Telemetry
{
    [Table("hutao_logs")]
    public class HutaoLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long PrimaryId { get; set; }

        public string Info { get; set; } = default!;

        public int Count { get; set; } = default!;

        public bool Resolved { get; set; }

        public string Version { get; set; } = default!;

        [NotMapped]
        public long Time { get; set; }
    }
}