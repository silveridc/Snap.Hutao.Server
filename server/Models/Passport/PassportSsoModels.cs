namespace server.Models.Passport;

/// <summary>
/// 启动器单点登录的可用状态。启动器据此决定登录界面显示 SSO 入口还是继续只走邮箱密码登录，
/// 返回内容不含任何密钥信息，匿名可查。
/// </summary>
public sealed class PassportSsoStatus
{
    public bool Enabled { get; set; }

    /// <summary>身份源显示名，为空时由启动器使用自己的文案。</summary>
    public string? DisplayName { get; set; }
}

/// <summary>
/// 用一次性票据换取启动器登录令牌的请求体。票据写在请求体里，避免出现在服务端访问日志中。
/// </summary>
public sealed class PassportSsoExchangeRequest
{
    public string? Ticket { get; set; }
}

/// <summary>
/// 启动器单点登录换来的令牌。与普通邮箱密码登录同构（含刷新令牌），额外带回邮箱，
/// 因为启动器需要用它作为本地保存的账号名。
/// </summary>
public sealed class PassportSsoLoginResult
{
    public string AccessToken { get; set; } = default!;

    public string RefreshToken { get; set; } = default!;

    public int ExpiresIn { get; set; }

    public string UserName { get; set; } = default!;
}

/// <summary>
/// 启动器回调处理的阶段。
/// </summary>
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

/// <summary>
/// 启动器回调处理结果。
/// </summary>
/// <param name="Status">处理阶段。</param>
/// <param name="Ticket">成功时签发的一次性票据。</param>
/// <param name="Subject">身份主体标识，被拒绝时回显给本人以便转交管理员。</param>
/// <param name="Detail">供服务端日志使用的英文说明。</param>
public sealed record PassportSsoCallbackResult(
    PassportSsoCallbackStatus Status,
    string? Ticket = null,
    string? Subject = null,
    string? Detail = null);
