namespace server.Models.Entity.RoleCombat
{
    [Table("role_combat_statistics")]
    public class RoleCombatStatistics
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long PrimaryId { get; set; }

        public int ScheduleId { get; set; } = default!;

        public string Data { get; set; } = default!;
    }
}