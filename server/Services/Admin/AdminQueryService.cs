using server.Models.Admin;
using server.Models.Entity;
using server.Models.Entity.GachaLog;
using server.Models.Entity.Redeem;
using server.Services.PublicAPI;

namespace server.Services.Admin;

// Scoped
public sealed class AdminQueryService(AppDbContext appDbContext, IpRegionService ipRegionService)
{
    /// <summary>某张兑换码的使用者清单，按使用时间倒序。</summary>
    public Task<List<RedeemCodeUsageItem>> GetRedeemCodeUsesAsync(uint id)
    {
        return appDbContext.RedeemCodeUseItems
            .AsNoTracking()
            .Where(item => item.RedeemCodeId == id)
            .OrderByDescending(item => item.UseTime)
            .Select(item => new RedeemCodeUsageItem
            {
                UserId = item.UsedBy,
                UserName = appDbContext.Users.Where(user => user.Id == item.UsedBy).Select(user => user.UserName).FirstOrDefault(),
                UseTime = item.UseTime,
            })
            .ToListAsync();
    }

    public async Task<PagedResult<RedeemCodeItem>> GetRedeemCodesAsync(int page, int pageSize, string? keyword)
    {
        // 分页
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

        List<AuditLogRow> rows = await baseQuery
            .OrderByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogRow
            {
                Id = a.Id,
                AdminUserId = a.AdminUserId,
                AdminAccountId = a.AdminAccountId,

                // 后台专属账号不在用户表里，显示名直接取写入时的快照
                AdminUserName = a.AdminAccountName ?? appDbContext.Users.Where(u => u.Id == a.AdminUserId).Select(u => u.UserName).FirstOrDefault(),
                Action = a.Action,
                Detail = a.Detail,
                Ip = a.Ip,
                Success = a.Success,
                Timestamp = a.Timestamp,
            })
            .ToListAsync()
            .ConfigureAwait(false);

        // 属地不落库，读的时候按 IP 现查（IpRegionService 带 Redis 缓存），同一个 IP 只查一次
        string[] ips = [.. rows.Select(row => row.Ip).OfType<string>().Distinct()];
        Task<string?[]?>[] lookups = [.. ips.Select(ip => ipRegionService.GetRegionAsync(ip))];
        await Task.WhenAll(lookups).ConfigureAwait(false);
        Dictionary<string, string?[]?> regions = [];
        for (int i = 0; i < ips.Length; i++)
        {
            regions[ips[i]] = lookups[i].Result;
        }

        List<AuditLogItem> items =
        [
            .. rows.Select(row => new AuditLogItem
            {
                Id = row.Id,
                AdminUserId = row.AdminUserId,
                AdminAccountId = row.AdminAccountId,
                AdminUserName = row.AdminUserName,
                Action = row.Action,
                Detail = row.Detail,
                Ip = row.Ip,
                IpRegion = row.Ip is null ? null : regions[row.Ip],
                Success = row.Success,
                Timestamp = row.Timestamp,
            }),
        ];

        return new PagedResult<AuditLogItem> { Total = total, Page = page, PageSize = pageSize, Items = items };
    }

    /// <summary>客户端登录日志：关键字匹配邮箱或 IP，可按结果、时间范围与用户过滤，IP 属地读取时现查。</summary>
    public async Task<PagedResult<UserLoginLogItem>> GetLoginLogsAsync(int page, int pageSize, string? keyword, bool? success, long? from, long? to, int? userId = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<UserLoginLog> query = appDbContext.UserLoginLogs.AsNoTracking();
        if (userId is int ownerId)
        {
            query = query.Where(log => log.UserId == ownerId);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string pattern = keyword.Trim();
            query = query.Where(log => (log.UserName != null && log.UserName.Contains(pattern)) || (log.Ip != null && log.Ip.Contains(pattern)));
        }

        if (success is bool successFilter)
        {
            query = query.Where(log => log.Success == successFilter);
        }

        if (from is long fromRaw)
        {
            DateTimeOffset start = DateTimeOffset.FromUnixTimeSeconds(Math.Clamp(fromRaw, 0L, 253402300799L));
            query = query.Where(log => log.LoginAt >= start);
        }

        if (to is long toRaw)
        {
            DateTimeOffset end = DateTimeOffset.FromUnixTimeSeconds(Math.Clamp(toRaw, 0L, 253402300799L));
            query = query.Where(log => log.LoginAt <= end);
        }

        long total = await query.LongCountAsync().ConfigureAwait(false);
        List<UserLoginLog> rows = await query
            .OrderByDescending(log => log.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync()
            .ConfigureAwait(false);

        string[] ips = [.. rows.Select(row => row.Ip).OfType<string>().Distinct()];
        Task<string?[]?>[] lookups = [.. ips.Select(ip => ipRegionService.GetRegionAsync(ip))];
        await Task.WhenAll(lookups).ConfigureAwait(false);
        Dictionary<string, string?[]?> regions = [];
        for (int i = 0; i < ips.Length; i++)
        {
            regions[ips[i]] = lookups[i].Result;
        }

        List<UserLoginLogItem> items =
        [
            .. rows.Select(row => new UserLoginLogItem
            {
                Id = row.Id,
                UserId = row.UserId,
                UserName = row.UserName,
                Method = row.Method,
                Success = row.Success,
                FailureKey = row.FailureKey,
                Ip = row.Ip,
                IpRegion = row.Ip is null ? null : regions[row.Ip],
                DeviceId = row.DeviceId,
                DeviceName = row.DeviceName,
                OperatingSystem = row.OperatingSystem,
                LoginAt = row.LoginAt,
            }),
        ];

        return new PagedResult<UserLoginLogItem> { Total = total, Page = page, PageSize = pageSize, Items = items };
    }

    private sealed class AuditLogRow
    {
        public long Id { get; init; }

        public int AdminUserId { get; init; }

        public int? AdminAccountId { get; init; }

        public string? AdminUserName { get; init; }

        public string Action { get; init; } = default!;

        public string? Detail { get; init; }

        public string? Ip { get; init; }

        public bool Success { get; init; }

        public long Timestamp { get; init; }
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
