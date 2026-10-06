using System.ComponentModel;

namespace server.Models.Entity.Announcement
{
    [Table("announcements")]
    [PrimaryKey(nameof(Id), nameof(Locale))]
    public sealed class EntityAnnouncement
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public string Locale { get; set; } = default!;

        public long LastUpdateTime { get; set; }

        public string Title { get; set; } = default!;

        public string Content { get; set; } = default!;

        public AnnouncementSeverity Severity { get; set; }

        public string Link { get; set; } = default!;

        public AnnouncementKind Kind { get; set; }

        public string? MaxPresentVersion { get; set; }

        public bool Enabled { get; set; } = true;
    }
}