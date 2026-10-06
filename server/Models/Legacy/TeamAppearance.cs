namespace server.Models.Legacy
{
    public class TeamAppearance
    {
        public int Floor { get; set; }

        public List<ItemRate<string, int>> Up { get; set; } = default!;

        public List<ItemRate<string, int>> Down { get; set; } = default!;
    }
}