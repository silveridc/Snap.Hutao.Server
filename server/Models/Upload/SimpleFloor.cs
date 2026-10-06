namespace server.Models.Upload
{
    public sealed class SimpleFloor
    {
        public int Index { get; set; }

        public int Star { get; set; }

        public List<SimpleLevel> Levels { get; set; } = default!;
    }
}