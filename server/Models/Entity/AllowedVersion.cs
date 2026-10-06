namespace server.Models.Entity
{
    [Table("allowed_versions")]
    public class AllowedVersion
    {
        [Key]
        public string Header { get; set; } = default!;
    }
}