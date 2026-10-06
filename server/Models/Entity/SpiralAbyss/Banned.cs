namespace server.Models.Entity.SpiralAbyss
{
    [Table("banned")]
    public class Banned
    {
        [Key]
        [StringLength(10, MinimumLength = 9)]
        public string Uid { get; set; } = default!;

        public string? Reason { get; set; }

        public long BanTime { get; set; }
    }
}