using server.Models.Admin;
using server.Models.Cache;
using server.Models.Entity.Announcement;
using server.Models.Entity.Redeem;
using server.Models.PublicAPI;
using server.Models.Redeem;
using server.Models.Upload;

namespace server.Tests.Infrastructure;

public sealed record ServiceEndpoint(string Method, string Url, string? Json, int? MaintainerRetcode);

public static class ServiceEndpoints
{
    /// <summary>占位用的、不存在的账号名.</summary>
    public const string UnknownUser = "nobody@example.com";

    /// <summary>占位用的、不存在的自增主键.</summary>
    public const long UnknownId = long.MaxValue;

    /// <summary>测试专用的封禁 Uid 号段，避免与其它用例相互影响.</summary>
    public const string BanAddUid = "900000009";

    public const string BanRemoveUid = "900000008";

    public const string BanBatchUid = "900000010";

    /// <summary>系统配置用例使用的键，避免污染 appsettings.json 中的真实配置项.</summary>
    public const string ConfigUpdateKey = "HutaoTest.Update";

    public const string ConfigResetKey = "HutaoTest.Reset";

    /// <summary>版本放行用例使用的 Header.</summary>
    public const string AllowedVersionHeader = "Smoke Hutao/0.0.0.1";

    /// <summary>客户端版本发布用例使用的版本号，与其它用例的版本号互不重叠.</summary>
    public const string PublishVersion = "1.17.2.0";

    public static IReadOnlyList<ServiceEndpoint> Rows { get; } =
    [
        new("POST", "/api/admin/statistics/refresh", null, ReturnCodes.Success),
        // 任务名不存在，稳定落在 NotFound 分支，不会真的触发任务
        new("POST", "/api/admin/jobs/NotExistingJob/run", null, ReturnCodes.NotFound),

        // 补偿接口用 days=0：只会把已过期账号的到期时间补到当前时刻，不会凭空发放时长
        new("POST", "/api/admin/gacha-logs/compensations?days=0", null, ReturnCodes.Success),
        new("POST", $"/api/admin/gacha-logs/compensations/designation?userName={Uri.EscapeDataString(UnknownUser)}&days=0", null, ReturnCodes.UserNameNotExists),
        new("POST", "/api/admin/cdn/compensations?days=0", null, ReturnCodes.Success),
        new("POST", $"/api/admin/cdn/compensations/designation?userName={Uri.EscapeDataString(UnknownUser)}&days=0", null, ReturnCodes.UserNameNotExists),

        new("GET", "/api/admin/system/overview", null, ReturnCodes.Success),
        new("GET", "/api/admin/analytics/gacha?days=30", null, ReturnCodes.Success),
        new("GET", "/api/admin/analytics/abyss", null, ReturnCodes.Success),
        new("GET", "/api/admin/analytics/damage?top=10", null, ReturnCodes.Success),
        new("GET", "/api/admin/users/statistics", null, ReturnCodes.Success),
        new("GET", "/api/admin/client-versions", null, ReturnCodes.Success),
        new("GET", "/api/admin/users?page=1&pageSize=10&keyword=example.com", null, ReturnCodes.Success),
        new("GET", $"/api/admin/users/{int.MaxValue}", null, ReturnCodes.UserNameNotExists),
        new("PATCH", $"/api/admin/users/{int.MaxValue}/privileges", JsonSerializer.Serialize(new UserPrivilegeRequest { UserId = int.MaxValue }), ReturnCodes.UserNameNotExists),
        new("PUT", $"/api/admin/users/{int.MaxValue}/password", JsonSerializer.Serialize(new UserResetPasswordRequest { UserId = int.MaxValue, NewPassword = "PlaceholderPassword1" }), ReturnCodes.InvalidUploadData),

        new("GET", "/api/admin/bans?page=1&pageSize=10", null, ReturnCodes.Success),
        new("POST", $"/api/admin/bans?uid={BanAddUid}&reason=smoke", null, ReturnCodes.Success),
        new("DELETE", $"/api/admin/bans/{BanRemoveUid}", null, ReturnCodes.Success),
        new("POST", "/api/admin/bans/batch", JsonSerializer.Serialize(new BatchUidRequest { Uids = [BanBatchUid] }), ReturnCodes.Success),

        new("GET", "/api/admin/announcements?page=1&pageSize=10", null, ReturnCodes.Success),
        new("POST", "/api/admin/announcements", JsonSerializer.Serialize(new HutaoUploadAnnouncement
        {
            Title = "Smoke Announcement",
            Content = "Smoke content",
            Severity = AnnouncementSeverity.Informational,
            Link = "https://example.com",
        }), ReturnCodes.Success),
        new("PUT", $"/api/admin/announcements/{UnknownId}", JsonSerializer.Serialize(new AnnouncementUpdateRequest
        {
            Id = UnknownId,
            Locale = "ALL",
            Title = "Smoke",
            Content = "Smoke content",
            Link = "https://example.com",
        }), ReturnCodes.NotFound),
        new("PATCH", $"/api/admin/announcements/{UnknownId}?enabled=true", null, ReturnCodes.NotFound),
        new("DELETE", $"/api/admin/announcements/{UnknownId}", null, ReturnCodes.NotFound),

        new("GET", "/api/admin/redeem-codes?page=1&pageSize=10", null, ReturnCodes.Success),
        new("GET", "/api/admin/redeem-codes/1/usages", null, ReturnCodes.Success),
        new("POST", "/api/admin/redeem-codes/generation", JsonSerializer.Serialize(new RedeemGenerateRequest
        {
            Count = 1,
            Type = RedeemCodeType.TimeLimited,
            ServiceType = RedeemCodeTargetServiceType.GachaLog,
            Value = 1,
            Description = "smoke",
            ExpireTime = DateTimeOffset.UtcNow.AddDays(1),
            Times = 1,
        }), ReturnCodes.Success),
        new("PUT", $"/api/admin/redeem-codes/{uint.MaxValue}", JsonSerializer.Serialize(new RedeemCodeUpdateRequest
        {
            Id = uint.MaxValue,
            Type = (uint)RedeemCodeType.TimeLimited,
            ServiceType = (uint)RedeemCodeTargetServiceType.GachaLog,
            Value = 1,
            Description = "smoke",
            ExpireTime = DateTimeOffset.UtcNow.AddDays(1),
            TimesAllowed = 1,
        }), ReturnCodes.NotFound),
        new("DELETE", $"/api/admin/redeem-codes/{uint.MaxValue}", null, ReturnCodes.NotFound),
        new("POST", "/api/admin/redeem-codes/batch-deletion", JsonSerializer.Serialize(new BatchIdRequest { Ids = [uint.MaxValue] }), ReturnCodes.Success),

        new("GET", "/api/admin/license-applications?page=1&pageSize=10", null, ReturnCodes.Success),
        new("POST", $"/api/admin/license-applications/approval?userName={Uri.EscapeDataString(UnknownUser)}&code=PLACEHOLDER", null, ReturnCodes.LicenseApprovalFailed),

        new("GET", "/api/admin/allowed-client-versions", null, ReturnCodes.Success),
        new("POST", $"/api/admin/allowed-client-versions?header={Uri.EscapeDataString(AllowedVersionHeader)}", null, ReturnCodes.Success),
        new("DELETE", $"/api/admin/allowed-client-versions?header={Uri.EscapeDataString(AllowedVersionHeader)}", null, ReturnCodes.Success),

        new("GET", "/api/admin/invalid-gacha-uids?page=1&pageSize=10", null, ReturnCodes.Success),
        new("DELETE", "/api/admin/invalid-gacha-uids/100000001", null, ReturnCodes.Success),

        new("GET", "/api/admin/audit-logs?page=1&pageSize=10", null, ReturnCodes.Success),
        new("GET", "/api/admin/login-logs?page=1&pageSize=10", null, ReturnCodes.Success),
        new("GET", "/api/admin/job-logs?page=1&pageSize=10", null, ReturnCodes.Success),
        new("GET", "/api/admin/cache/keys?pattern=*&limit=10", null, ReturnCodes.Success),
        new("DELETE", "/api/admin/cache", JsonSerializer.Serialize(new CacheInvalidationRequest
        {
            Keys = ["HutaoTest.Placeholder"],
            Patterns = ["HutaoTest.Placeholder:*"],
        }), ReturnCodes.Success),

        new("GET", "/api/admin/ranks", null, ReturnCodes.Success),
        new("GET", "/api/admin/ranks/Hutao.Damage.Avatar.All?count=10", null, ReturnCodes.Success),

        new("GET", "/api/admin/releases/current", null, ReturnCodes.Success),
        new("GET", "/api/admin/releases?page=1&pageSize=10", null, ReturnCodes.Success),
        // 发布有状态：同一版本号不可重复发布，且发布会立即切换当前版本，
        // 成功与「重复发布被拒」两个分支由 ReleasePublish_ThenActivate_MakesTheVersionCurrent 断言
        new("POST", "/api/admin/releases", TestData.ReleasePublishJson(PublishVersion), null),
        new("PUT", $"/api/admin/releases/{UnknownId}/activation", JsonSerializer.Serialize(new HutaoReleaseActivateRequest { Id = UnknownId }), ReturnCodes.NotFound),
        new("DELETE", $"/api/admin/releases/{UnknownId}/activation", null, ReturnCodes.NotFound),
        // 从存储桶发布：S3 未配置时稳定落在「未配置」分支；哈希与下发断言见 ReleasePublishFromBucket_* 用例
        new("GET", "/api/admin/releases/bucket-objects", null, ReturnCodes.InvalidUploadData),
        new("POST", "/api/admin/releases/from-bucket", JsonSerializer.Serialize(new HutaoReleasePublishFromBucketRequest { Key = "patch/Snap.Hutao.1.0.0.0.msix" }), ReturnCodes.InvalidUploadData),

        new("GET", "/api/admin/settings", null, ReturnCodes.Success),
        new("PATCH", "/api/admin/settings", JsonSerializer.Serialize(new Dictionary<string, string?> { [ConfigUpdateKey] = "1" }), ReturnCodes.Success),
        new("DELETE", $"/api/admin/settings/{ConfigResetKey}", null, ReturnCodes.NotFound),

        new("GET", "/api/admin/mail/template", null, ReturnCodes.Success),
        // 保存有状态：缺少占位符稳定落在校验失败分支，不覆盖测试库里的模板
        new("PUT", "/api/admin/mail/template", JsonSerializer.Serialize(new MailTemplateUpdateRequest { Template = "<html>missing placeholders</html>" }), ReturnCodes.InvalidUploadData),
        // 恢复默认有状态（是否自定义过取决于其它用例的执行顺序），只要求穿过权限过滤器
        new("DELETE", "/api/admin/mail/template", null, null),

        new("GET", "/api/admin/mail/contents", null, ReturnCodes.Success),
        new("GET", "/api/admin/mail/content/RegisterCode?language=CHS", null, ReturnCodes.Success),
        // 保存有状态：缺少运行时变量的请求稳定落在校验失败分支，不覆盖测试库里的文案
        new("PUT", "/api/admin/mail/content/RegisterCode", JsonSerializer.Serialize(new MailContentUpdateRequest
        {
            Language = "CHS",
            Subject = "Smoke Subject",
            Title = "Smoke Title",
            RawContent = "<p>missing variables</p>",
            Footer = "Smoke Footer",
        }), ReturnCodes.InvalidUploadData),
        // 恢复默认有状态（是否自定义过取决于其它用例的执行顺序），只要求穿过权限过滤器
        new("DELETE", "/api/admin/mail/content/RegisterCode?language=CHS", null, null),

        new("GET", "/api/admin/wallpaper-pool", null, ReturnCodes.Success),
        // 保存有状态：用 http 直链稳定落在校验失败分支，不覆盖测试库里的壁纸池
        new("PUT", "/api/admin/wallpaper-pool", JsonSerializer.Serialize(new[] { new WallpaperPoolItem { Url = "http://insecure.example.com/wallpaper.png" } }), ReturnCodes.InvalidUploadData),

        new("GET", "/api/admin/yae/metadata", null, ReturnCodes.Success),

        // Yae 同步有状态：成功分支会写库，且依赖外部 bucket 的桩数据，
        // 两个分支由 YaeTests 断言，这里只要求它穿过维护者权限过滤器
        new("POST", "/api/admin/yae/metadata", null, null),

        // 后台专属账号（SSO）：SSO 未启用的宿主里写门禁整体放行，维护者可到达业务逻辑.
        // Add 有状态（唯一 subject，重复调用会走到「已存在」分支），只要求穿过权限过滤器；
        // SetEnabled/Remove 用越界 accountId，稳定落在「账号不存在」分支.
        new("GET", "/api/admin/accounts", null, ReturnCodes.Success),
        new("POST", "/api/admin/accounts", JsonSerializer.Serialize(new AdminAccountAddRequest { Subject = AdminAccountSubject }), null),
        new("PATCH", $"/api/admin/accounts/{int.MaxValue}?enabled=false", null, ReturnCodes.NotFound),
        new("DELETE", $"/api/admin/accounts/{int.MaxValue}", null, ReturnCodes.NotFound),
        // Bind/Unbind 同样用越界 accountId 落在「账号不存在」分支，绑定的成功分支见 SsoTests
        new("PUT", $"/api/admin/accounts/{int.MaxValue}/binding", JsonSerializer.Serialize(new AdminAccountBindRequest { UserName = "no-such-user@example.com" }), ReturnCodes.NotFound),
        new("DELETE", $"/api/admin/accounts/{int.MaxValue}/binding", null, ReturnCodes.NotFound),
    ];

    /// <summary>后台账号用例使用的 subject，与真实身份源签发的好区分.</summary>
    public const string AdminAccountSubject = "smoke-admin-subject";

    /// <summary>xUnit <see cref="TheoryData{T1,T2,T3}"/> 形式，供 <c>[MemberData]</c> 直接引用.</summary>
    public static TheoryData<string, string, string?> TheoryData
    {
        get
        {
            TheoryData<string, string, string?> data = [];
            foreach (ServiceEndpoint row in Rows)
            {
                data.Add(row.Method, row.Url, row.Json);
            }

            return data;
        }
    }
}