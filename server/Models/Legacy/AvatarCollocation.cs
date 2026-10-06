namespace server.Models.Legacy
{
    public class AvatarCollocation(int avatarId) : AvatarBuild(avatarId)
    {
        public List<ItemRate<int, double>> Avatars { get; set; } = default!;

        public List<ItemRate<int, double>> Weapons { get; set; } = default!;

        public List<ItemRate<string, double>> Reliquaries { get; set; } = default!;
    }
}