using server.Models.Admin;
using server.Models.Entity.SpiralAbyss;

namespace server.Services.Admin;

// Scoped
public sealed class BanService(AppDbContext appDbContext, IDistributedCache cache, TimeProvider timeProvider)
{
    /// <summary>
    /// Gets a page of banned users, optionally filtered by a UID keyword.
    /// </summary>
    /// <param name="page">The one-based page number; values below 1 are treated as 1.</param>
    /// <param name="pageSize">The page size, clamped to the range 1-100.</param>
    /// <param name="keyword">The optional UID keyword used to filter the list.</param>
    /// <returns>The requested page of banned users.</returns>
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
    /// Bans the specified UID.
    /// </summary>
    /// <param name="uid">The UID to ban.</param>
    /// <param name="reason">The optional ban reason.</param>
    /// <returns>
    /// <see langword="true"/> if a new ban entry was added;
    /// <see langword="false"/> if the UID was already banned.
    /// </returns>
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
            BanTime = timeProvider.GetUnixTimeSeconds(),
        });
        await appDbContext.SaveChangesAsync().ConfigureAwait(false);
        cache.Remove(UserAdminService.StatisticsCacheKey);
        return true;
    }

    /// <summary>
    /// Removes the ban entry of the specified UID.
    /// </summary>
    /// <param name="uid">The UID to unban.</param>
    /// <returns>
    /// <see langword="true"/> if a ban entry was removed; otherwise, <see langword="false"/>.
    /// </returns>
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
    /// Bans multiple UIDs at once, skipping UIDs that are already banned.
    /// </summary>
    /// <param name="uids">The UIDs to ban; blank entries are ignored.</param>
    /// <param name="reason">The optional ban reason applied to every newly added entry.</param>
    /// <returns>The number of newly added ban entries.</returns>
    public async Task<int> BatchBanAsync(IEnumerable<string> uids, string? reason)
    {
        long now = timeProvider.GetUnixTimeSeconds();
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
