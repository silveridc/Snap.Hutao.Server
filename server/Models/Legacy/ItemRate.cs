namespace server.Models.Legacy
{
    public class ItemRate<TItem, TRate>(TItem item, TRate rate)
    {
        public TItem Item { get; set; } = item;

        public TRate Rate { get; set; } = rate;
    }
}