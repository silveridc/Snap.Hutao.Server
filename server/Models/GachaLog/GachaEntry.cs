namespace server.Models.GachaLog
{
    public sealed class GachaEntry
    {
        public string Uid { get; set; } = default!;

        public bool Excluded { get; set; }

        public int ItemCount { get; set; }
    }
}