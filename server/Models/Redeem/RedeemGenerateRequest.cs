using server.Models.Entity.Redeem;

namespace server.Models.Redeem
{
    public sealed class RedeemGenerateRequest
    {
        public uint Count { get; set; }

        public RedeemCodeType Type { get; set; }

        public RedeemCodeTargetServiceType ServiceType { get; set; }

        public int Value { get; set; }

        public string Description { get; set; } = default!;

        public DateTimeOffset ExpireTime { get; set; }

        public uint Times { get; set; }

        [JsonIgnore]
        public string Creator { get; set; } = default!;
    }
}