using server.Models.Metadata;
using System.ComponentModel;

namespace server.Models.Entity.GachaLog
{
    [Table("gacha_items")]
    [PrimaryKey(nameof(Uid), nameof(Id))]
    public sealed class EntityGachaItem
    {
        [ForeignKey(nameof(UserId))]
        public HutaoUser User { get; set; } = default!;

        public int UserId { get; set; }

        [StringLength(10, MinimumLength = 9)]
        public string Uid { get; set; } = default!;

        public long Id { get; set; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool IsTrusted { get; set; }

        public GachaConfigType GachaType { get; set; }

        public GachaConfigType QueryType { get; set; }

        public int ItemId { get; set; }

        public DateTimeOffset Time { get; set; }
    }
}