namespace server.Models.Legacy
{
    public class AvatarConstellationInfo(int avatarId) : AvatarBuild(avatarId)
    {
        public double HoldingRate { get; set; }

        public List<ItemRate<int, double>> Constellations { get; set; } = default!;
    }
}