namespace server.Models.Legacy
{
    public abstract class WeaponBuild(int weaponId)
    {
        public int WeaponId { get; set; } = weaponId;
    }
}