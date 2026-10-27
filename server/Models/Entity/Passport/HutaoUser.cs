using server.Models.Entity.Redeem;

namespace server.Models.Entity.Passport;

public sealed class HutaoUser : IdentityUser<int>
{
    public long GachaLogExpireAt { get; set; }

    public long CdnExpireAt { get; set; }

    public bool IsLicensedDeveloper { get; set; }

    public bool IsMaintainer { get; set; }

    /// <summary>注册时间的 unix 秒；本次改动之前的老账号没有记录，为 0。</summary>
    public long CreatedAt { get; set; }

    public List<RedeemCodeUseItem> RedeemCodeUseItems { get; set; } = default!;
}
