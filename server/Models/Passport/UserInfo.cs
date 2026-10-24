namespace server.Models.Passport;

public class UserInfo
{
    public string? NormalizedUserName { get; set; }

    public string? UserName { get; set; }

    public bool IsLicensedDeveloper { get; set; }

    public bool IsMaintainer { get; set; }

    /// <summary>身份种类：主站账号为 <c>user</c>，后台专属账号（SSO）为 <c>admin</c>。</summary>
    public string AuthKind { get; set; } = "user";

    /// <summary>当前身份是否可执行后台写操作。收紧老账号后，主站维护者在启用 SSO 时为 false。</summary>
    public bool CanWrite { get; set; }

    public DateTimeOffset GachaLogExpireAt { get; set; }

    public DateTimeOffset CdnExpireAt { get; set; }
}