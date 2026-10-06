using server.Models.Patch;

namespace server.Models.Entity
{
    [Table("hutao_releases")]
    [Index(nameof(Version), IsUnique = true)]
    [Index(nameof(IsActive))]
    public sealed class HutaoRelease
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [MaxLength(32)]
        public string Version { get; set; } = default!;

        [MaxLength(64)]
        public string Validation { get; set; } = default!;

        [Column(TypeName = "longtext")]
        public string MirrorsJson { get; set; } = default!;

        [Column(TypeName = "longtext")]
        public string? ReleaseNotes { get; set; }

        public bool IsActive { get; set; }

        public DateTimeOffset PublishedAt { get; set; }

        public int PublishedBy { get; set; }

        public HutaoPackageInformation ToPackageInformation()
        {
            return new()
            {
                Version = Version,
                Validation = Validation,
                Mirrors = JsonSerializer.Deserialize<List<server.Models.Patch.HutaoPackageMirror>>(MirrorsJson) ?? [],
            };
        }
    }
}
