namespace server.Models.Entity;

[Table("allowed_versions")]
public class AllowedVersion
{
    [Key]
    [StringLength(64)]
    public string Header { get; set; } = default!;
}
