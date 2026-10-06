namespace server.Models.Entity.Achievement
{
    [Table("achievements")]
    public sealed class EntityAchievement
    {
        public long ArchiveId { get; set; }

        [ForeignKey(nameof(ArchiveId))]
        public EntityAchievementArchive Archive { get; set; } = default!;

        [Key]
        public uint Id { get; set; }

        public uint Current { get; set; }

        public DateTimeOffset Time { get; set; }

        public AchievementStatus Status { get; set; }
    }
}