namespace server.Models.Entity.SpiralAbyss
{
    [Table("avatars")]
    public class EntityAvatar
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long PrimaryId { get; set; }

        public long RecordId { get; set; }

        [ForeignKey(nameof(RecordId))]
        public EntityRecord Record { get; set; } = null!;

        public int AvatarId { get; set; }

        public int WeaponId { get; set; }

        [StringLength(50)]
        public string ReliquarySet { get; set; } = default!;

        public int ActivedConstellationNumber { get; set; }
    }
}