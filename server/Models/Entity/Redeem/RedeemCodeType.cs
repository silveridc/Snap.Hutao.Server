namespace server.Models.Entity.Redeem
{
    [Flags]
    public enum RedeemCodeType : uint
    {
        None = 0b0000U,
        TimeLimited = 0b0001U,
        TimesLimited = 0b0010U,
    }
}