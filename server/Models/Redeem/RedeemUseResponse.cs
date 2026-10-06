using server.Models.Entity.Redeem;

namespace server.Models.Redeem
{
    public sealed class RedeemUseResponse(RedeemStatus status, RedeemCodeTargetServiceType type, int value, DateTimeOffset expireTime)
    {
        public RedeemUseResponse(RedeemStatus status)
            : this(status, default, default, default)
        {
        }

        public RedeemStatus Status { get; set; } = status;

        public RedeemCodeTargetServiceType Type { get; set; } = type;

        public int Value { get; set; } = value;

        public DateTimeOffset ExpireTime { get; set; } = expireTime;
    }
}