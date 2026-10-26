namespace server.Models.Passport;

public sealed class PassportSsoStatus
{
    public bool Enabled { get; set; }

    /// <summary>身份源显示名，为空时由启动器使用自己的文案。</summary>
    public string? DisplayName { get; set; }
}

public sealed class PassportSsoExchangeRequest
{
    public string? Ticket { get; set; }
}

public sealed class PassportSsoLoginResult
{
    public string AccessToken { get; set; } = default!;

    public string RefreshToken { get; set; } = default!;

    public int ExpiresIn { get; set; }

    public string UserName { get; set; } = default!;
}

public enum PassportSsoCallbackStatus
{
    /// <summary>身份已确认且允许登录，票据已签发。</summary>
    Success,

    /// <summary>state 不存在或已被使用（可能是伪造的回放）。</summary>
    InvalidState,

    /// <summary>身份源拒绝了授权码交换。</summary>
    UpstreamRejected,

    /// <summary>ID Token 校验失败。</summary>
    TokenInvalid,

    /// <summary>身份有效，但还没有对应的后台账号。</summary>
    NotProvisioned,

    /// <summary>对应的后台账号存在但已被停用。</summary>
    Disabled,

    /// <summary>对应的后台账号还没有绑定主站账号。</summary>
    NotBound,
}

public sealed record PassportSsoCallbackResult(
    PassportSsoCallbackStatus Status,
    string? Ticket = null,
    string? Subject = null,
    string? Detail = null);
