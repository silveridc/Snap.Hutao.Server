namespace server.Models.GachaLog
{
    public class UidAndItems
    {
        public string Uid { get; set; } = default!;

        public List<SimpleGachaItem> Items { get; set; } = default!;
    }
}