using server.Models.Admin;
using server.Models.Cache;
using server.Models.Entity.Announcement;
using server.Models.Entity.Redeem;
using server.Models.Redeem;
using server.Models.Upload;

namespace server.Tests.Infrastructure;

public sealed record ServiceEndpoint(string Method, string Url, string? Json, int? MaintainerRetcode);

public static class ServiceEndpoints
{
    /// <summary>占位用的、不存在的账号名。</summary>
    public const string UnknownUser = "nobody@example.com";

    /// <summary>占位用的、不存在的自增主键。</summary>
    public const long UnknownId = long.MaxValue;

    /// <summary>测试专用的封禁 Uid 号段，避免与其它用例相互影响。</summary>
    public const string BanAddUid = "900000009";

    public const string BanRemoveUid = "900000008";

    public const string BanBatchUid = "900000010";

    /// <summary>系统配置用例使用的键，避免污染 appsettings.json 中的真实配置项。</summary>
    public const string ConfigUpdateKey = "HutaoTest.Update";

    public const string ConfigResetKey = "HutaoTest.Reset";

    /// <summary>版本放行用例使用的 Header。</summary>
    public const string AllowedVersionHeader = "Smoke Hutao/0.0.0.1";

    /// <summary>客户端版本发布用例使用的版本号，与其它用例的版本号互不重叠。</summary>
    public const string PublishVersion = "1.17.2.0";

    public static IReadOnlyList<ServiceEndpoint> Rows { get; } =
    [
        new("GET", "/api/Service/Statistics/Run", null, ReturnCodes.Success),

        // 补偿接口用 days=0：只会把已过期账号的到期时间补到当前时刻，不会凭空发放时长
        new("GET", "/api/Service/GachaLog/Compensation?days=0", null, ReturnCodes.Success),
        new("GET", $"/api/Service/GachaLog/Designation?userName={Uri.EscapeDataString(UnknownUser)}&days=0", null, ReturnCodes.UserNameNotExists),
        new("GET", "/api/Service/Distribution/Compensation?days=0", null, ReturnCodes.Success),
        new("GET", $"/api/Service/Distribution/Designation?userName={Uri.EscapeDataString(UnknownUser)}&days=0", null, ReturnCodes.UserNameNotExists),

        new("GET", "/api/Service/System/Overview", null, ReturnCodes.Success),
        new("GET", "/api/Service/Analytics/Gacha?days=30", null, ReturnCodes.Success),
        new("GET", "/api/Service/Analytics/Abyss", null, ReturnCodes.Success),
        new("GET", "/api/Service/Analytics/Damage?top=10", null, ReturnCodes.Success),
        new("GET", "/api/Service/User/Statistics", null, ReturnCodes.Success),
        new("GET", "/api/Service/ClientVersion/List", null, ReturnCodes.Success),
        new("GET", "/api/Service/User/List?page=1&pageSize=10&keyword=example.com", null, ReturnCodes.Success),
        new("GET", $"/api/Service/User/Detail?userId={int.MaxValue}", null, ReturnCodes.UserNameNotExists),
        new("POST", "/api/Service/User/Privilege", JsonSerializer.Serialize(new UserPrivilegeRequest { UserId = int.MaxValue }), ReturnCodes.UserNameNotExists),
        new("POST", "/api/Service/User/ResetPassword", JsonSerializer.Serialize(new UserResetPasswordRequest { UserId = int.MaxValue, NewPassword = "PlaceholderPassword1" }), ReturnCodes.InvalidUploadData),

        new("GET", "/api/Service/Ban/List?page=1&pageSize=10", null, ReturnCodes.Success),
        new("POST", $"/api/Service/Ban/Add?uid={BanAddUid}&reason=smoke", null, ReturnCodes.Success),
        new("POST", $"/api/Service/Ban/Remove?uid={BanRemoveUid}", null, ReturnCodes.Success),
        new("POST", "/api/Service/Ban/BatchAdd", JsonSerializer.Serialize(new BatchUidRequest { Uids = [BanBatchUid] }), ReturnCodes.Success),

        new("GET", "/api/Service/Announcement/List?page=1&pageSize=10", null, ReturnCodes.Success),
        new("POST", "/api/Service/Announcement/Upload", JsonSerializer.Serialize(new HutaoUploadAnnouncement
        {
            Title = "Smoke Announcement",
            Content = "Smoke content",
            Severity = AnnouncementSeverity.Informational,
            Link = "https://example.com",
        }), ReturnCodes.Success),
        new("POST", "/api/Service/Announcement/Update", JsonSerializer.Serialize(new AnnouncementUpdateRequest
        {
            Id = UnknownId,
            Locale = "ALL",
            Title = "Smoke",
            Content = "Smoke content",
            Link = "https://example.com",
        }), ReturnCodes.NotFound),
        new("POST", $"/api/Service/Announcement/SetEnabled?id={UnknownId}&enabled=true", null, ReturnCodes.NotFound),
        new("POST", $"/api/Service/Announcement/Delete?id={UnknownId}", null, ReturnCodes.NotFound),

        new("GET", "/api/Service/Redeem/List?page=1&pageSize=10", null, ReturnCodes.Success),
        new("GET", "/api/Service/Redeem/Uses?id=1", null, ReturnCodes.Success),
        new("POST", "/api/Service/Redeem/Generate", JsonSerializer.Serialize(new RedeemGenerateRequest
        {
            Count = 1,
            Type = RedeemCodeType.TimeLimited,
            ServiceType = RedeemCodeTargetServiceType.GachaLog,
            Value = 1,
            Description = "smoke",
            ExpireTime = DateTimeOffset.UtcNow.AddDays(1),
            Times = 1,
        }), ReturnCodes.Success),
        new("POST", "/api/Service/Redeem/Update", JsonSerializer.Serialize(new RedeemCodeUpdateRequest
        {
            Id = uint.MaxValue,
            Type = (uint)RedeemCodeType.TimeLimited,
            ServiceType = (uint)RedeemCodeTargetServiceType.GachaLog,
            Value = 1,
            Description = "smoke",
            ExpireTime = DateTimeOffset.UtcNow.AddDays(1),
            TimesAllowed = 1,
        }), ReturnCodes.NotFound),
        new("POST", $"/api/Service/Redeem/Delete?id={uint.MaxValue}", null, ReturnCodes.NotFound),
        new("POST", "/api/Service/Redeem/BatchDelete", JsonSerializer.Serialize(new BatchIdRequest { Ids = [uint.MaxValue] }), ReturnCodes.Success),

        new("GET", "/api/Service/License/Applications?page=1&pageSize=10", null, ReturnCodes.Success),
        new("POST", $"/api/Service/License/Approve?userName={Uri.EscapeDataString(UnknownUser)}&code=PLACEHOLDER", null, ReturnCodes.LicenseApprovalFailed),

        new("GET", "/api/Service/AllowedVersion/List", null, ReturnCodes.Success),
        new("POST", $"/api/Service/AllowedVersion/Add?header={Uri.EscapeDataString(AllowedVersionHeader)}", null, ReturnCodes.Success),
        new("POST", $"/api/Service/AllowedVersion/Remove?header={Uri.EscapeDataString(AllowedVersionHeader)}", null, ReturnCodes.Success),

        new("GET", "/api/Service/InvalidGachaUid/List?page=1&pageSize=10", null, ReturnCodes.Success),
        new("POST", "/api/Service/InvalidGachaUid/Remove?uid=100000001", null, ReturnCodes.Success),

        new("GET", "/api/Service/Audit/List?page=1&pageSize=10", null, ReturnCodes.Success),
        new("GET", "/api/Service/Cache/Keys?pattern=*&limit=10", null, ReturnCodes.Success),
        new("POST", "/api/Service/Cache/Invalidate", JsonSerializer.Serialize(new CacheInvalidationRequest
        {
            Keys = ["HutaoTest.Placeholder"],
            Patterns = ["HutaoTest.Placeholder:*"],
        }), ReturnCodes.Success),

        new("GET", "/api/Service/Rank/Keys", null, ReturnCodes.Success),
        new("GET", "/api/Service/Rank/Leaderboard?key=Hutao.Damage.Avatar.All&count=10", null, ReturnCodes.Success),

        new("GET", "/api/Service/Release/Current", null, ReturnCodes.Success),
        new("GET", "/api/Service/Release/List?page=1&pageSize=10", null, ReturnCodes.Success),
        // 发布有状态：同一版本号不可重复发布，且发布会立即切换当前版本，
        // 成功与「重复发布被拒」两个分支由 ReleasePublish_ThenActivate_MakesTheVersionCurrent 断言
        new("POST", "/api/Service/Release/Publish", TestData.ReleasePublishJson(PublishVersion), null),
        new("POST", "/api/Service/Release/Activate", JsonSerializer.Serialize(new HutaoReleaseActivateRequest { Id = UnknownId }), ReturnCodes.NotFound),

        new("GET", "/api/Service/Config/Get", null, ReturnCodes.Success),
        new("POST", "/api/Service/Config/Update", JsonSerializer.Serialize(new Dictionary<string, string?> { [ConfigUpdateKey] = "1" }), ReturnCodes.Success),
        new("POST", $"/api/Service/Config/Reset?key={ConfigResetKey}", null, ReturnCodes.NotFound),

        new("GET", "/api/Service/Yae/Metadata", null, ReturnCodes.Success),

        // Yae 同步有状态：成功分支会写库，且依赖外部 bucket 的桩数据，
        // 两个分支由 YaeTests 断言，这里只要求它穿过维护者权限过滤器
        new("POST", "/api/Service/Yae/SyncMetadata", null, null),

        // 后台专属账号（SSO）：SSO 未启用的宿主里写门禁整体放行，维护者可到达业务逻辑。
        // Add 有状态（唯一 subject，重复调用会走到「已存在」分支），只要求穿过权限过滤器；
        // SetEnabled/Remove 用越界 accountId，稳定落在「账号不存在」分支。
        new("GET", "/api/Service/AdminAccount/List", null, ReturnCodes.Success),
        new("POST", "/api/Service/AdminAccount/Add", JsonSerializer.Serialize(new AdminAccountAddRequest { Subject = AdminAccountSubject }), null),
        new("POST", $"/api/Service/AdminAccount/SetEnabled?accountId={int.MaxValue}&enabled=false", null, ReturnCodes.NotFound),
        new("POST", $"/api/Service/AdminAccount/Remove?accountId={int.MaxValue}", null, ReturnCodes.NotFound),
        // Bind/Unbind 同样用越界 accountId 落在「账号不存在」分支，绑定的成功分支见 SsoTests
        new("POST", $"/api/Service/AdminAccount/Bind?accountId={int.MaxValue}", JsonSerializer.Serialize(new AdminAccountBindRequest { UserName = "no-such-user@example.com" }), ReturnCodes.NotFound),
        new("POST", $"/api/Service/AdminAccount/Unbind?accountId={int.MaxValue}", null, ReturnCodes.NotFound),
    ];

    /// <summary>后台账号用例使用的 subject，与真实身份源签发的好区分。</summary>
    public const string AdminAccountSubject = "smoke-admin-subject";

    /// <summary>xUnit <see cref="TheoryData{T1,T2,T3}"/> 形式，供 <c>[MemberData]</c> 直接引用。</summary>
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
