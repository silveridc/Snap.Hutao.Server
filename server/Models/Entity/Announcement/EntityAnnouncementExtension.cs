namespace server.Models.Entity.Announcement
{
    public static class EntityAnnouncementExtension
    {
        public static bool TryGetMaxPresentVersion(this EntityAnnouncement announcement, [NotNullWhen(true)] out Version? version)
        {
            return Version.TryParse(announcement.MaxPresentVersion, out version);
        }
    }
}