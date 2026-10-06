namespace server.Models.Entity.RoleCombat
{
    [Table("role_combat_avatars")]
    public sealed class RoleCombatAvatar
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long PrimaryId { get; set; }

        public long RecordId { get; set; }

        [ForeignKey(nameof(RecordId))]
        public RoleCombatRecord Record { get; set; } = null!;

        public uint AvatarId { get; set; }
    }
}