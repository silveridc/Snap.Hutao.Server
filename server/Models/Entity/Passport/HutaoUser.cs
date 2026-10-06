using server.Models.Entity.Redeem;

namespace server.Models.Entity.Passport
{
    public sealed class HutaoUser : IdentityUser<int>
    {
        public long GachaLogExpireAt { get; set; }

        public long CdnExpireAt { get; set; }

        public bool IsLicensedDeveloper { get; set; }

        public bool IsMaintainer { get; set; }

        public List<RedeemCodeUseItem> RedeemCodeUseItems { get; set; } = default!;
    }
}