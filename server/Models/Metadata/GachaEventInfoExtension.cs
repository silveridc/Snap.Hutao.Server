using server.Models.Entity.GachaLog;

namespace server.Models.Metadata
{
    public static class GachaEventInfoExtension
    {
        public static IEnumerable<int> GetUpOrangeItems(this GachaEventInfo info)
        {
            return typeof(GachaEventInfo)
                .GetProperties()
                .Where(p => p.Name.StartsWith("UpOrangeItem"))
                .Select(p => (uint)p.GetValue(info)!)
                .Where(v => v is not 0U)
                .Select(v => (int)v);
        }

        public static bool ItemInThisEvent(this GachaEventInfo info, EntityGachaItem item)
        {
            if (item.GachaType == info.Type && item.Time >= info.From && item.Time <= info.To)
            {
                return true;
            }

            return false;
        }
    }
}