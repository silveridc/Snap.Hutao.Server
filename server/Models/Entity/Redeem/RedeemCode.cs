namespace server.Models.Entity.Redeem;

[Table("redeem_codes")]
public sealed class RedeemCode
{
    [Key]
    public uint Id
    {
        get; set;
    }

    [StringLength(24)]
    public string Code { get; set; } = default!;

    public RedeemCodeType Type
    {
        get; set;
    }

    public RedeemCodeTargetServiceType ServiceType
    {
        get; set;
    }

    public int Value
    {
        get; set;
    }

    [StringLength(1024)]
    public string Description { get; set; } = default!;

    [StringLength(128)]
    public string Creator { get; set; } = default!;

    public DateTimeOffset CreateTime
    {
        get; set;
    }

    public List<RedeemCodeUseItem> UseItems { get; set; } = default!;

    public DateTimeOffset ExpireTime
    {
        get; set;
    }

    public uint TimesAllowed
    {
        get; set;
    }
}
