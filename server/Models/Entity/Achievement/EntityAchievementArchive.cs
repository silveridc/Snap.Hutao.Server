namespace server.Models.Entity.Achievement
{
    [Table("achievement_archives")]
    public sealed class EntityAchievementArchive
    {
        [ForeignKey(nameof(UserId))]
        public HutaoUser User { get; set; } = default!;

        public int UserId { get; set; }

        [Key]
        public long Id { get; set; }

        public string Name { get; set; } = default!;
    }
}