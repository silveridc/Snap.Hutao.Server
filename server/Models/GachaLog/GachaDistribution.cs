namespace server.Models.GachaLog
{
    internal sealed class GachaDistribution
    {
        public long TotalValidPulls { get; set; }

        public List<PullCount> Distribution { get; set; } = default!;
    }
}