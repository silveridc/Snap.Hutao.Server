namespace server.Models.Legacy
{
    public class WeaponCollocation(int weaponId) : WeaponBuild(weaponId)
    {
        public List<ItemRate<int, double>> Avatars { get; set; } = default!;
    }
}