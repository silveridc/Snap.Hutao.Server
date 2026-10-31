using System.ComponentModel;

namespace server.Models.Entity.Announcement;

[Table("announcements")]
[PrimaryKey(nameof(Id), nameof(Locale))]
public sealed class EntityAnnouncement
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id
    {
        get; set;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [StringLength(16)]
    public string Locale { get; set; } = default!;

    public long LastUpdateTime
    {
        get; set;
    }

    [StringLength(256)]
    public string Title { get; set; } = default!;

    public string Content { get; set; } = default!;

    public AnnouncementSeverity Severity
    {
        get; set;
    }

    public string Link { get; set; } = default!;

    public AnnouncementKind Kind
    {
        get; set;
    }

    [StringLength(32)]
    public string? MaxPresentVersion
    {
        get; set;
    }

    public bool Enabled { get; set; } = true;
}
