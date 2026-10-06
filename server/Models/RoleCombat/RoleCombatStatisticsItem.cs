using server.Models.Legacy;

namespace server.Models.RoleCombat
{
    public sealed class RoleCombatStatisticsItem
    {
        public int ScheduleId { get; set; }

        public int RecordTotal { get; set; }

        public long Timestamp { get; set; }

        public List<ItemRate<uint, double>> BackupAvatarRates { get; set; } = default!;
    }
}