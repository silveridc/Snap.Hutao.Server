namespace server.Models.Entity.GachaLog
{
    [Table("invalid_gacha_uids")]
    public class InvalidGachaUid
    {
        [Key]
        [StringLength(10, MinimumLength = 9)]
        public string Uid { get; set; } = default!;
    }
}