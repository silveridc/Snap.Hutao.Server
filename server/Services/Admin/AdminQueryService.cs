using server.Models.Admin;
using server.Models.Context;
using server.Models.Entity;
using server.Models.Entity.GachaLog;
using server.Models.Entity.Redeem;

namespace server.Services.Admin
{
    // Scoped
    public sealed class AdminQueryService(AppDbContext appDbContext)
    {
        public async Task<PagedResult<RedeemCodeItem>> GetRedeemCodesAsync(int page, int pageSize, string? keyword)
        {
            //分页
            page = page < 1 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 100);

            IQueryable<RedeemCode> query = appDbContext.RedeemCodes.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string trimmed = keyword.Trim();
                query = query.Where(c => c.Code.Contains(trimmed) || c.Description.Contains(trimmed) || c.Creator.Contains(trimmed));
            }

            long total = await query.LongCountAsync().ConfigureAwait(false);
            List<RedeemCodeItem> items = await query
                .OrderByDescending(c => c.CreateTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new RedeemCodeItem
                {
                    Id = c.Id,
                    Code = c.Code,
                    Type = (uint)c.Type,
                    ServiceType = (uint)c.ServiceType,
                    Value = c.Value,
                    Description = c.Description,
                    Creator = c.Creator,
                    CreateTime = c.CreateTime,
                    ExpireTime = c.ExpireTime,
                    TimesAllowed = c.TimesAllowed,
                    UsedCount = c.UseItems.Count,
                })
                .ToListAsync()
                .ConfigureAwait(false);

            return new PagedResult<RedeemCodeItem> { Total = total, Page = page, PageSize = pageSize, Items = items };
        }

        public async Task<bool> UpdateRedeemCodeAsync(RedeemCodeUpdateRequest request)
        {
            RedeemCode? code = await appDbContext.RedeemCodes.FirstOrDefaultAsync(c => c.Id == request.Id).ConfigureAwait(false);
            if (code is null)
            {
                return false;
            }

            code.ServiceType = (RedeemCodeTargetServiceType)request.ServiceType;
            code.Type = (RedeemCodeType)request.Type;
            code.Value = request.Value;
            code.Description = request.Description ?? string.Empty;
            code.ExpireTime = request.ExpireTime;
            code.TimesAllowed = request.TimesAllowed;

            await appDbContext.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task<bool> DeleteRedeemCodeAsync(uint id)
        {
            await appDbContext.RedeemCodeUseItems.Where(u => u.RedeemCodeId == id).ExecuteDeleteAsync().ConfigureAwait(false);
            int removed = await appDbContext.RedeemCodes.Where(c => c.Id == id).ExecuteDeleteAsync().ConfigureAwait(false);
            return removed > 0;
        }
        public async Task<PagedResult<LicenseApplicationItem>> GetLicenseApplicationsAsync(int page, int pageSize, bool onlyPending)
        {
            page = page < 1 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 100);

            IQueryable<LicenseApplicationRecord> query = appDbContext.Licenses.AsNoTracking();
            if (onlyPending)
            {
                query = query.Where(w => !w.IsApproved);
            }

            long total = await query.LongCountAsync().ConfigureAwait(false);
            List<LicenseApplicationItem> items = await query
                .OrderByDescending(o => o.PrimaryId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new LicenseApplicationItem
                {
                    PrimaryId = s.PrimaryId,
                    UserId = s.UserId,
                    UserName = s.User.UserName,
                    ProjectUrl = s.ProjectUrl,
                    ApprovalCode = s.ApprovalCode,
                    IsApproved = s.IsApproved,
                })
                .ToListAsync()
                .ConfigureAwait(false);

            return new PagedResult<LicenseApplicationItem> { Total = total, Page = page, PageSize = pageSize, Items = items };
        }

        public Task<List<string>> GetAllowedVersionsAsync()
        {
            return appDbContext.AllowedVersions.AsNoTracking().OrderBy(v => v.Header).Select(v => v.Header).ToListAsync();
        }

        public async Task<bool> AddAllowedVersionAsync(string header)
        {
            if (await appDbContext.AllowedVersions.AnyAsync(v => v.Header == header).ConfigureAwait(false))
            {
                return false;
            }

            appDbContext.AllowedVersions.Add(new AllowedVersion { Header = header });
            await appDbContext.SaveChangesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task<bool> RemoveAllowedVersionAsync(string header)
        {
            int removed = await appDbContext.AllowedVersions.Where(v => v.Header == header).ExecuteDeleteAsync().ConfigureAwait(false);
            return removed > 0;
        }

        public async Task<PagedResult<string>> GetInvalidGachaUidsAsync(int page, int pageSize, string? keyword)
        {
            page = page < 1 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 100);

            IQueryable<InvalidGachaUid> query = appDbContext.InvalidGachaUids.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string trimmed = keyword.Trim();
                query = query.Where(u => u.Uid.Contains(trimmed));
            }

            long total = await query.LongCountAsync().ConfigureAwait(false);
            List<string> items = await query
                .OrderBy(u => u.Uid)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => u.Uid)
                .ToListAsync()
                .ConfigureAwait(false);

            return new PagedResult<string> { Total = total, Page = page, PageSize = pageSize, Items = items };
        }

        public async Task<bool> RemoveInvalidGachaUidAsync(string uid)
        {
            int removed = await appDbContext.InvalidGachaUids.Where(u => u.Uid == uid).ExecuteDeleteAsync().ConfigureAwait(false);
            return removed > 0;
        }

        public async Task<long> BatchDeleteRedeemCodesAsync(List<uint> ids)
        {
            if (ids.Count == 0)
            {
                return 0;
            }

            await appDbContext.RedeemCodeUseItems.Where(u => ids.Contains(u.RedeemCodeId)).ExecuteDeleteAsync().ConfigureAwait(false);
            return await appDbContext.RedeemCodes.Where(c => ids.Contains(c.Id)).ExecuteDeleteAsync().ConfigureAwait(false);
        }

        public async Task<PagedResult<AuditLogItem>> GetAuditLogsAsync(int page, int pageSize)
        {
            page = page < 1 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 100);

            IQueryable<AdminAuditLog> baseQuery = appDbContext.AdminAuditLogs.AsNoTracking();
            long total = await baseQuery.LongCountAsync().ConfigureAwait(false);

            List<AuditLogItem> items = await baseQuery
                .OrderByDescending(a => a.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AuditLogItem
                {
                    Id = a.Id,
                    AdminUserId = a.AdminUserId,
                    AdminUserName = appDbContext.Users.Where(u => u.Id == a.AdminUserId).Select(u => u.UserName).FirstOrDefault(),
                    Action = a.Action,
                    Detail = a.Detail,
                    Success = a.Success,
                    Timestamp = a.Timestamp,
                })
                .ToListAsync()
                .ConfigureAwait(false);

            return new PagedResult<AuditLogItem> { Total = total, Page = page, PageSize = pageSize, Items = items };
        }

        public async Task<UserDetail?> GetUserDetailAsync(int userId)
        {
            HutaoUser? user = await appDbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId).ConfigureAwait(false);
            if (user is null)
            {
                return null;
            }

            List<UserGachaUid> gachaUids = await appDbContext.GachaItems.AsNoTracking()
                .Where(i => i.UserId == userId)
                .GroupBy(i => i.Uid)
                .Select(g => new UserGachaUid { Uid = g.Key, ItemCount = g.Count() })
                .ToListAsync()
                .ConfigureAwait(false);

            int redeemUsed = await appDbContext.RedeemCodeUseItems.AsNoTracking().CountAsync(r => r.UsedBy == userId).ConfigureAwait(false);

            return new UserDetail
            {
                Id = user.Id,
                UserName = user.UserName,
                IsMaintainer = user.IsMaintainer,
                IsLicensedDeveloper = user.IsLicensedDeveloper,
                GachaLogExpireAt = user.GachaLogExpireAt,
                CdnExpireAt = user.CdnExpireAt,
                RedeemUsedCount = redeemUsed,
                GachaUids = gachaUids,
            };
        }
    }
}
