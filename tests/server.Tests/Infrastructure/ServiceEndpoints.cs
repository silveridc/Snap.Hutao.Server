using server.Models.Admin;
using server.Models.Cache;
using server.Models.Entity.Announcement;
using server.Models.Entity.Redeem;
using server.Models.Redeem;
using server.Models.Upload;

namespace server.Tests.Infrastructure;

/// <summary>
/// <c>/Service/*</c> 维护接口清单中的一条路由。
/// </summary>
/// <param name="Method">HTTP 方法。</param>
/// <param name="Url">相对地址（含查询串）。</param>
/// <param name="Json">请求体；GET 或无需请求体时为 <see langword="null"/>。</param>
/// <param name="MaintainerRetcode">
/// 维护者身份调用时期望的返回码。取值只使用「不依赖其它用例副作用」的输入：
/// 主键一律用 <see cref="int.MaxValue"/> 之类的越界值，Uid 用测试专用号段，
/// 因此无论用例以何种顺序执行结果都一致。
/// 有状态的接口（同一份数据重复调用会走到不同分支）取 <see langword="null"/>，
/// 表示只要求它通过权限过滤器，业务分支交给专门的用例覆盖。
/// </param>
public sealed record ServiceEndpoint(string Method, string Url, string? Json, int? MaintainerRetcode);

/// <summary>
/// <c>/Service/*</c> 维护者后台的全部路由。冒烟测试与鉴权用例共用同一份清单，
/// 保证每个接口都会被「未登录 / 非维护者 / 维护者」三种身份各跑一遍。
/// </summary>
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
        new("GET", "/Service/Statistics/Run", null, ReturnCodes.Success),

        // 补偿接口用 days=0：只会把已过期账号的到期时间补到当前时刻，不会凭空发放时长
        new("GET", "/Service/GachaLog/Compensation?days=0", null, ReturnCodes.Success),
        new("GET", $"/Service/GachaLog/Designation?userName={Uri.EscapeDataString(UnknownUser)}&days=0", null, ReturnCodes.UserNameNotExists),
        new("GET", "/Service/Distribution/Compensation?days=0", null, ReturnCodes.Success),
        new("GET", $"/Service/Distribution/Designation?userName={Uri.EscapeDataString(UnknownUser)}&days=0", null, ReturnCodes.UserNameNotExists),

        new("GET", "/Service/User/Statistics", null, ReturnCodes.Success),
        new("GET", "/Service/User/List?page=1&pageSize=10&keyword=example.com", null, ReturnCodes.Success),
        new("GET", $"/Service/User/Detail?userId={int.MaxValue}", null, ReturnCodes.UserNameNotExists),
        new("POST", "/Service/User/Privilege", JsonSerializer.Serialize(new UserPrivilegeRequest { UserId = int.MaxValue }), ReturnCodes.UserNameNotExists),
        new("POST", "/Service/User/ResetPassword", JsonSerializer.Serialize(new UserResetPasswordRequest { UserId = int.MaxValue, NewPassword = "PlaceholderPassword1" }), ReturnCodes.InvalidUploadData),

        new("GET", "/Service/Ban/List?page=1&pageSize=10", null, ReturnCodes.Success),
        new("POST", $"/Service/Ban/Add?uid={BanAddUid}&reason=smoke", null, ReturnCodes.Success),
        new("POST", $"/Service/Ban/Remove?uid={BanRemoveUid}", null, ReturnCodes.Success),
        new("POST", "/Service/Ban/BatchAdd", JsonSerializer.Serialize(new BatchUidRequest { Uids = [BanBatchUid] }), ReturnCodes.Success),

        new("GET", "/Service/Announcement/List?page=1&pageSize=10", null, ReturnCodes.Success),
        new("POST", "/Service/Announcement/Upload", JsonSerializer.Serialize(new HutaoUploadAnnouncement
        {
            Title = "Smoke Announcement",
            Content = "Smoke content",
            Severity = AnnouncementSeverity.Informational,
            Link = "https://example.com",
        }), ReturnCodes.Success),
        new("POST", "/Service/Announcement/Update", JsonSerializer.Serialize(new AnnouncementUpdateRequest
        {
            Id = UnknownId,
            Locale = "ALL",
            Title = "Smoke",
            Content = "Smoke content",
            Link = "https://example.com",
        }), ReturnCodes.NotFound),
        new("POST", $"/Service/Announcement/SetEnabled?id={UnknownId}&enabled=true", null, ReturnCodes.NotFound),
        new("POST", $"/Service/Announcement/Delete?id={UnknownId}", null, ReturnCodes.NotFound),

        new("GET", "/Service/Redeem/List?page=1&pageSize=10", null, ReturnCodes.Success),
        new("POST", "/Service/Redeem/Generate", JsonSerializer.Serialize(new RedeemGenerateRequest
        {
            Count = 1,
            Type = RedeemCodeType.TimeLimited,
            ServiceType = RedeemCodeTargetServiceType.GachaLog,
            Value = 1,
            Description = "smoke",
            ExpireTime = DateTimeOffset.UtcNow.AddDays(1),
            Times = 1,
        }), ReturnCodes.Success),
        new("POST", "/Service/Redeem/Update", JsonSerializer.Serialize(new RedeemCodeUpdateRequest
        {
            Id = uint.MaxValue,
            Type = (uint)RedeemCodeType.TimeLimited,
            ServiceType = (uint)RedeemCodeTargetServiceType.GachaLog,
            Value = 1,
            Description = "smoke",
            ExpireTime = DateTimeOffset.UtcNow.AddDays(1),
            TimesAllowed = 1,
        }), ReturnCodes.NotFound),
        new("POST", $"/Service/Redeem/Delete?id={uint.MaxValue}", null, ReturnCodes.NotFound),
        new("POST", "/Service/Redeem/BatchDelete", JsonSerializer.Serialize(new BatchIdRequest { Ids = [uint.MaxValue] }), ReturnCodes.Success),

        new("GET", "/Service/License/Applications?page=1&pageSize=10", null, ReturnCodes.Success),
        new("POST", $"/Service/License/Approve?userName={Uri.EscapeDataString(UnknownUser)}&code=PLACEHOLDER", null, ReturnCodes.LicenseApprovalFailed),

        new("GET", "/Service/AllowedVersion/List", null, ReturnCodes.Success),
        new("POST", $"/Service/AllowedVersion/Add?header={Uri.EscapeDataString(AllowedVersionHeader)}", null, ReturnCodes.Success),
        new("POST", $"/Service/AllowedVersion/Remove?header={Uri.EscapeDataString(AllowedVersionHeader)}", null, ReturnCodes.Success),

        new("GET", "/Service/InvalidGachaUid/List?page=1&pageSize=10", null, ReturnCodes.Success),
        new("POST", "/Service/InvalidGachaUid/Remove?uid=100000001", null, ReturnCodes.Success),

        new("GET", "/Service/Audit/List?page=1&pageSize=10", null, ReturnCodes.Success),
        new("GET", "/Service/Cache/Keys?pattern=*&limit=10", null, ReturnCodes.Success),
        new("POST", "/Service/Cache/Invalidate", JsonSerializer.Serialize(new CacheInvalidationRequest
        {
            Keys = ["HutaoTest.Placeholder"],
            Patterns = ["HutaoTest.Placeholder:*"],
        }), ReturnCodes.Success),

        new("GET", "/Service/Rank/Keys", null, ReturnCodes.Success),
        new("GET", "/Service/Rank/Leaderboard?key=Hutao.Damage.Avatar.All&count=10", null, ReturnCodes.Success),

        new("GET", "/Service/Release/Current", null, ReturnCodes.Success),
        new("GET", "/Service/Release/List?page=1&pageSize=10", null, ReturnCodes.Success),
        // 发布有状态：同一版本号不可重复发布，且发布会立即切换当前版本，
        // 成功与「重复发布被拒」两个分支由 ReleasePublish_ThenActivate_MakesTheVersionCurrent 断言
        new("POST", "/Service/Release/Publish", TestData.ReleasePublishJson(PublishVersion), null),
        new("POST", "/Service/Release/Activate", JsonSerializer.Serialize(new HutaoReleaseActivateRequest { Id = UnknownId }), ReturnCodes.NotFound),

        new("GET", "/Service/Config/Get", null, ReturnCodes.Success),
        new("POST", "/Service/Config/Update", JsonSerializer.Serialize(new Dictionary<string, string?> { [ConfigUpdateKey] = "1" }), ReturnCodes.Success),
        new("POST", $"/Service/Config/Reset?key={ConfigResetKey}", null, ReturnCodes.NotFound),
    ];

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
