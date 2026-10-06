namespace server.Models.Entity
{
    [Table("system_settings")]
    public class SystemSetting
    {
        [Key]
        [MaxLength(128)]
        public string Key { get; set; } = default!;

        [Column(TypeName = "longtext")]
        public string? Value { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public int UpdatedBy { get; set; }
    }
}
