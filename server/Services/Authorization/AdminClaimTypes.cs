namespace server.Services.Authorization;

/// <summary>
/// 后台专属账号（SSO）令牌的声明类型。这类令牌不含 <see cref="PassportClaimTypes.UserId"/>，
/// 因此天然无法命中任何以主站用户身份取数的代码路径。
/// </summary>
public static class AdminClaimTypes
{
    public const string AccountId = nameof(AccountId);
}
