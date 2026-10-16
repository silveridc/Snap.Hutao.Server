using server.Extension;
using server.Models.Admin;

namespace server.Services.Admin
{
    // Scoped
    public sealed class UserAdminService(AppDbContext appDbContext, IDistributedCache cache, UserManager<HutaoUser> userManager)
    {
        public const string StatisticsCacheKey = "Admin:UserStatistics";
        private static readonly TimeSpan StatisticsCacheDuration = TimeSpan.FromSeconds(60);

        public async Task<UserStatistics> GetStatisticsAsync()
        {
            if (cache.TryGetValue(StatisticsCacheKey, out UserStatistics? cached) && cached is not null)
            {
                return cached;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            UserStatistics statistics = new()
            {
                TotalUsers = await appDbContext.Users.CountAsync().ConfigureAwait(false),
                Maintainers = await appDbContext.Users.CountAsync(u => u.IsMaintainer).ConfigureAwait(false),
                LicensedDevelopers = await appDbContext.Users.CountAsync(u => u.IsLicensedDeveloper).ConfigureAwait(false),
                ActiveGachaLogUsers = await appDbContext.Users.CountAsync(u => u.GachaLogExpireAt > now).ConfigureAwait(false),
                ActiveCdnUsers = await appDbContext.Users.CountAsync(u => u.CdnExpireAt > now).ConfigureAwait(false),
                BannedUids = await appDbContext.BannedList.CountAsync().ConfigureAwait(false),
                GachaItemCount = await appDbContext.GachaItems.LongCountAsync().ConfigureAwait(false),
                SpiralAbyssRecordCount = await appDbContext.Records.CountAsync().ConfigureAwait(false),
                RoleCombatRecordCount = await appDbContext.RoleCombatRecords.CountAsync().ConfigureAwait(false),
            };

            cache.Set(StatisticsCacheKey, statistics, StatisticsCacheDuration);
            return statistics;
        }

        public async Task<PagedResult<UserListItem>> GetUsersAsync(int page, int pageSize, string? keyword, bool? maintainer = null, bool? developer = null, int? expiringInDays = null)
        {
            page = page < 1 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 100);

            IQueryable<HutaoUser> query = appDbContext.Users.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string trimmed = keyword.Trim();
                query = query.Where(u => (u.UserName != null && u.UserName.Contains(trimmed)) || (u.Email != null && u.Email.Contains(trimmed)));
            }

            if (maintainer is { } m)
            {
                query = query.Where(u => u.IsMaintainer == m);
            }

            if (developer is { } d)
            {
                query = query.Where(u => u.IsLicensedDeveloper == d);
            }

            if (expiringInDays is { } days && days > 0)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long threshold = DateTimeOffset.UtcNow.AddDays(days).ToUnixTimeSeconds();
                query = query.Where(u =>
                    (u.GachaLogExpireAt > now && u.GachaLogExpireAt <= threshold) ||
                    (u.CdnExpireAt > now && u.CdnExpireAt <= threshold));
            }

            long total = await query.LongCountAsync().ConfigureAwait(false);
            List<UserListItem> items = await query
                .OrderBy(u => u.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new UserListItem
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    IsMaintainer = u.IsMaintainer,
                    IsLicensedDeveloper = u.IsLicensedDeveloper,
                    GachaLogExpireAt = u.GachaLogExpireAt,
                    CdnExpireAt = u.CdnExpireAt,
                })
                .ToListAsync()
                .ConfigureAwait(false);

            return new PagedResult<UserListItem>
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                Items = items,
            };
        }

        /// <summary>
        /// 更新指定用户的维护者/开发者授权标志，并失效相关缓存。
        /// </summary>
        /// <returns>用户是否存在。</returns>
        public async Task<bool> UpdatePrivilegeAsync(int userId, bool? isMaintainer, bool? isLicensedDeveloper)
        {
            HutaoUser? user = await appDbContext.Users.FirstOrDefaultAsync(u => u.Id == userId).ConfigureAwait(false);
            if (user is null)
            {
                return false;
            }

            if (isMaintainer.HasValue)
            {
                user.IsMaintainer = isMaintainer.Value;
            }

            if (isLicensedDeveloper.HasValue)
            {
                user.IsLicensedDeveloper = isLicensedDeveloper.Value;
            }

            await appDbContext.SaveChangesAsync().ConfigureAwait(false);

            // ValidateMaintainPermission 会缓存维护者判定 15 分钟，这里主动失效使其立即生效
            cache.Remove($"User:IsMaintainer:{userId}");
            cache.Remove(StatisticsCacheKey);
            return true;
        }

        /// <summary>
        /// 以管理员身份重置指定用户的密码。
        /// </summary>
        /// <returns>是否成功，以及失败时的错误描述。</returns>
        public async Task<(bool Ok, string? Error, string? LocalizationKey)> ResetPasswordAsync(int userId, string newPassword)
        {
            HutaoUser? user = await userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
            if (user is null)
            {
                return (false, "User does not exist", ServerKeys.ServerAdminUserNotFound);
            }

            string token = await userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
            IdentityResult result = await userManager.ResetPasswordAsync(user, token, newPassword).ConfigureAwait(false);
            return result.Succeeded
                ? (true, null, null)
                : (false, string.Join("; ", result.Errors.Select(e => e.Description)), null);
        }
    }
}
