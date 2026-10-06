using server.Extension;
using server.Models.Admin;
using server.Models.Context;
using server.Models.Entity.SpiralAbyss;

namespace server.Services.Admin
{
    // Scoped
    public sealed class BanService(AppDbContext appDbContext, IDistributedCache cache)
    {
        public async Task<PagedResult<BanItem>> GetBannedAsync(int page, int pageSize, string? keyword)
        {
            page = page < 1 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 100);

            IQueryable<Banned> query = appDbContext.BannedList.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string trimmed = keyword.Trim();
                query = query.Where(b => b.Uid.Contains(trimmed));
            }

            long total = await query.LongCountAsync().ConfigureAwait(false);
            List<BanItem> items = await query
                .OrderByDescending(b => b.BanTime)
                .ThenBy(b => b.Uid)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new BanItem { Uid = b.Uid, Reason = b.Reason, BanTime = b.BanTime })
                .ToListAsync()
                .ConfigureAwait(false);

            return new PagedResult<BanItem>
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                Items = items,
            };
        }

        /// <summary>
        /// 封禁指定 Uid。返回是否为新增（已存在则返回 false）。
        /// </summary>
        public async Task<bool> BanAsync(string uid, string? reason)
        {
            if (await appDbContext.BannedList.AnyAsync(b => b.Uid == uid).ConfigureAwait(false))
            {
                return false;
            }

            appDbContext.BannedList.Add(new Banned
            {
                Uid = uid,
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                BanTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            });
            await appDbContext.SaveChangesAsync().ConfigureAwait(false);
            cache.Remove(UserAdminService.StatisticsCacheKey);
            return true;
        }

        /// <summary>
        /// 解封指定 Uid。返回是否有记录被删除。
        /// </summary>
        public async Task<bool> UnbanAsync(string uid)
        {
            int removed = await appDbContext.BannedList.Where(b => b.Uid == uid).ExecuteDeleteAsync().ConfigureAwait(false);
            if (removed > 0)
            {
                cache.Remove(UserAdminService.StatisticsCacheKey);
            }

            return removed > 0;
        }

        /// <summary>
        /// 批量封禁多个 Uid，返回新增封禁的数量。
        /// </summary>
        public async Task<int> BatchBanAsync(IEnumerable<string> uids, string? reason)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string? normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

            HashSet<string> distinct = [.. uids.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim())];
            if (distinct.Count == 0)
            {
                return 0;
            }

            HashSet<string> existing = [.. await appDbContext.BannedList
                .Where(b => distinct.Contains(b.Uid))
                .Select(b => b.Uid)
                .ToListAsync()
                .ConfigureAwait(false)];

            List<Banned> toAdd = [.. distinct
                .Where(uid => !existing.Contains(uid))
                .Select(uid => new Banned { Uid = uid, Reason = normalizedReason, BanTime = now })];

            if (toAdd.Count == 0)
            {
                return 0;
            }

            appDbContext.BannedList.AddRange(toAdd);
            await appDbContext.SaveChangesAsync().ConfigureAwait(false);
            cache.Remove(UserAdminService.StatisticsCacheKey);
            return toAdd.Count;
        }
    }
}
