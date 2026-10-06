using server.Models.Metadata;

namespace server.Models.GachaLog
{
    public sealed class EndIds : Dictionary<string, long>
    {
        public static readonly FrozenSet<GachaConfigType> QueryTypes = FrozenSet.ToFrozenSet(
        [
            GachaConfigType.NewBie,
            GachaConfigType.Standard,
            GachaConfigType.ActivityAvatar,
            GachaConfigType.ActivityWeapon,
            GachaConfigType.ActivityCity,
        ]);

        public void Add(GachaConfigType type, long id)
        {
            Add($"{type:D}", id);
        }

        public IEnumerable<KeyValuePair<GachaConfigType, long>> EnumerateAsNewest()
        {
            foreach ((string type, long endId) in this)
            {
                GachaConfigType configType = Enum.Parse<GachaConfigType>(type);
                long exactEndId = endId == 0 ? long.MaxValue : endId;
                yield return new(configType, exactEndId);
            }
        }
    }
}