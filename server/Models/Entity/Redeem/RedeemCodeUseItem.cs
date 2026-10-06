using server.Models.Entity.Passport;

namespace server.Models.Entity.Redeem
{
    [Table("redeem_code_use_items")]
    public sealed class RedeemCodeUseItem
    {
        public uint RedeemCodeId { get; set; }

        [ForeignKey(nameof(RedeemCodeId))]
        public RedeemCode RedeemCode { get; set; } = default!;

        [Key]
        public uint Id { get; set; }

        public int UsedBy { get; set; } = default!;

        [ForeignKey(nameof(UsedBy))]
        public HutaoUser User { get; set; } = default!;

        public DateTimeOffset UseTime { get; set; }
    }
}