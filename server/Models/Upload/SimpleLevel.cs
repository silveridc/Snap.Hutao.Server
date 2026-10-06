namespace server.Models.Upload
{
    public sealed class SimpleLevel
    {
        public int Index { get; set; }

        public int Star { get; set; }

        public List<SimpleBattle> Battles { get; set; } = default!;
    }
}