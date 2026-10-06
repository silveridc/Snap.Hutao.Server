namespace server.Models.Legacy
{
    public class AvatarAppearanceRank
    {
        public int Floor { get; set; }

        public List<ItemRate<int, double>> Ranks { get; set; } = default!;
    }
}