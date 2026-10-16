using server.Extension;
using server.Models.Admin;
using server.Models.Entity.Announcement;
using server.Models.Upload;

namespace server.Services.Announcement
{
    // Scoped
    public sealed class AnnouncementService(AppDbContext appDbContext, IDistributedCache cache)
    {
        private const string SnapHutaoClientHeaderPrefix = "Snap Hutao/";
        private const string AnnouncementCachePrefix = "Announcements:";
        private static readonly TimeSpan MinAnnouncementTimeThreshold = TimeSpan.FromDays(30);
        private static readonly TimeSpan AnnouncementCacheDuration = TimeSpan.FromMinutes(5);

        public async ValueTask<List<EntityAnnouncement>> GetAnnouncementsAsync(Version currentVersion, string locale, IReadOnlySet<long> excludedIds)
        {
            List<EntityAnnouncement> anns = await GetRecentAnnouncementsAsync(locale).ConfigureAwait(false);

            List<EntityAnnouncement> result = [];
            foreach (EntityAnnouncement ann in anns)
            {
                if (ann.TryGetMaxPresentVersion(out Version? maxPresentVersion) && maxPresentVersion < currentVersion)
                {
                    continue;
                }

                if (!excludedIds.Contains(ann.Id))
                {
                    result.Add(ann);
                }
            }

            return result;
        }

        private async ValueTask<List<EntityAnnouncement>> GetRecentAnnouncementsAsync(string locale)
        {
            string cacheKey = $"{AnnouncementCachePrefix}{locale}";
            if (cache.TryGetValue(cacheKey, out List<EntityAnnouncement>? cached) && cached is not null)
            {
                return cached;
            }

            long limit = (DateTimeOffset.UtcNow - MinAnnouncementTimeThreshold).ToUnixTimeSeconds();

            List<EntityAnnouncement> anns = await appDbContext.Announcements
                .AsNoTracking()
                .OrderByDescending(ann => ann.LastUpdateTime)
                .Where(ann => ann.Locale == locale)
                .Where(ann => ann.Enabled)
                .Where(ann => ann.LastUpdateTime >= limit)
                .ToListAsync()
                .ConfigureAwait(false);

            cache.Set(cacheKey, anns, AnnouncementCacheDuration);
            return anns;
        }

        public async ValueTask ProcessUploadAnnouncementAsync(HutaoUploadAnnouncement announcement)
        {
            EntityAnnouncement entity = new()
            {
                Locale = "ALL",
                LastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                Title = announcement.Title,
                Content = announcement.Content,
                Severity = announcement.Severity,
                Link = announcement.Link,
                MaxPresentVersion = announcement.MaxPresentVersion,
            };

            appDbContext.Announcements.Add(entity);

            await appDbContext.SaveChangesAsync().ConfigureAwait(false);
            cache.Remove($"{AnnouncementCachePrefix}{entity.Locale}");
        }

        public async Task<PagedResult<EntityAnnouncement>> GetAllAsync(int page, int pageSize)
        {
            page = page < 1 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 100);

            IQueryable<EntityAnnouncement> query = appDbContext.Announcements.AsNoTracking().OrderByDescending(a => a.LastUpdateTime);

            long total = await query.LongCountAsync().ConfigureAwait(false);
            List<EntityAnnouncement> items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync()
                .ConfigureAwait(false);

            return new PagedResult<EntityAnnouncement> { Total = total, Page = page, PageSize = pageSize, Items = items };
        }

        public async Task<bool> DeleteAsync(long id, string locale)
        {
            int removed = await appDbContext.Announcements
                .Where(a => a.Id == id && a.Locale == locale)
                .ExecuteDeleteAsync()
                .ConfigureAwait(false);

            if (removed > 0)
            {
                cache.Remove($"{AnnouncementCachePrefix}{locale}");
            }

            return removed > 0;
        }

        public async Task<bool> UpdateAsync(AnnouncementUpdateRequest request)
        {
            EntityAnnouncement? entity = await appDbContext.Announcements
                .FirstOrDefaultAsync(a => a.Id == request.Id && a.Locale == request.Locale)
                .ConfigureAwait(false);

            if (entity is null)
            {
                return false;
            }

            entity.Title = request.Title;
            entity.Content = request.Content;
            entity.Severity = (AnnouncementSeverity)request.Severity;
            entity.Link = request.Link;
            entity.MaxPresentVersion = string.IsNullOrWhiteSpace(request.MaxPresentVersion) ? null : request.MaxPresentVersion;
            entity.LastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds();

            await appDbContext.SaveChangesAsync().ConfigureAwait(false);
            cache.Remove($"{AnnouncementCachePrefix}{request.Locale}");
            return true;
        }

        public async Task<bool> SetEnabledAsync(long id, string locale, bool enabled)
        {
            int affected = await appDbContext.Announcements
                .Where(a => a.Id == id && a.Locale == locale)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.Enabled, enabled))
                .ConfigureAwait(false);

            if (affected > 0)
            {
                cache.Remove($"{AnnouncementCachePrefix}{locale}");
            }

            return affected > 0;
        }
    }
}