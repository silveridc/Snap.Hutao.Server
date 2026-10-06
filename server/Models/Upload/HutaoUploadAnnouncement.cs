using server.Models.Entity.Announcement;

namespace server.Models.Upload
{
    public sealed class HutaoUploadAnnouncement
    {
        public string Title { get; set; } = default!;

        public string Content { get; set; } = default!;

        public AnnouncementSeverity Severity { get; set; }

        public string Link { get; set; } = default!;

        public string? MaxPresentVersion { get; set; }
    }
}