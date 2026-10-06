namespace server.Models.Entity.Redeem
{
    public static class RedeemCodeTargetServiceTypeExtension
    {
        public static string ToServiceName(this RedeemCodeTargetServiceType type)
        {
            return type switch
            {
                RedeemCodeTargetServiceType.GachaLog => "抽卡记录",
                RedeemCodeTargetServiceType.Cdn => "CDN",
                _ => throw new ArgumentOutOfRangeException(),
            };
        }
    }
}