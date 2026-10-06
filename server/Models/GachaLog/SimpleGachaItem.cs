using server.Models.Metadata;

namespace server.Models.GachaLog
{
    public class SimpleGachaItem
    {
        public GachaConfigType GachaType { get; set; }

        public GachaConfigType QueryType { get; set; }

        public int ItemId { get; set; }

        public DateTimeOffset Time { get; set; }

        public long Id { get; set; }
    }
}