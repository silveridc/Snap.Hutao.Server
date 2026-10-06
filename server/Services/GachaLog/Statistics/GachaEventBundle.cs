using server.Models.Metadata;

namespace server.Services.GachaLog.Statistics
{
    internal sealed class GachaEventBundle
    {
        public GachaEventInfo? AvatarEvent1 { get; set; } = default!;

        public GachaEventInfo? AvatarEvent2 { get; set; } = default!;

        public GachaEventInfo? WeaponEvent { get; set; } = default!;

        public GachaEventInfo? Chronicled { get; set; } = default!;
    }
}