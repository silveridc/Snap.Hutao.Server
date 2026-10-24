namespace server.Models.Admin;

/// <summary>
/// 后台单点登录的可用状态，供登录页决定是否显示 SSO 入口。不包含任何密钥信息。
/// </summary>
public sealed class AdminSsoStatus
{
    public bool Enabled { get; set; }

    public string? DisplayName { get; set; }
}

/// <summary>
/// 用一次性票据换来的后台令牌。后台令牌没有刷新令牌，到期后重新走一次 SSO。
/// </summary>
public sealed class AdminSsoTokenResponse
{
    public string AccessToken { get; set; } = default!;

    public int ExpiresIn { get; set; }

    public AdminSsoAccount Principal { get; set; } = default!;
}

/// <summary>
/// 当前后台账号的基本信息。
/// </summary>
public sealed class AdminSsoAccount
{
    public int Id { get; set; }

    public string UserName { get; set; } = default!;

    public string? Email { get; set; }

    public string Provider { get; set; } = default!;

    public string Subject { get; set; } = default!;

    public bool IsBootstrap { get; set; }
}

/// <summary>
/// 后台账号管理页的一行。
/// </summary>
public sealed class AdminAccountItem
{
    public int Id { get; set; }

    public string Provider { get; set; } = default!;

    public string Subject { get; set; } = default!;

    public string UserName { get; set; } = default!;

    public string? Email { get; set; }

    public bool IsEnabled { get; set; }

    public bool IsBootstrap { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>绑定的主站账号 ID，未绑定时为 null，启动器单点登录只允许已绑定的账号。</summary>
    public int? BoundUserId { get; set; }

    /// <summary>绑定的主站账号邮箱。</summary>
    public string? BoundUserName { get; set; }

    /// <summary>是否为当前调用者本人，前端据此禁用「停用自己」。</summary>
    public bool IsCurrent { get; set; }
}

/// <summary>
/// 用一次性票据换取后台令牌的请求体。票据走请求体而不是地址，避免出现在访问日志里。
/// </summary>
public sealed class AdminSsoExchangeRequest
{
    public string? Ticket { get; set; }
}

/// <summary>
/// 新增后台专属账号的请求体。
/// </summary>
public sealed class AdminAccountAddRequest
{
    /// <summary>身份源中的主体标识（OIDC 的 sub）。</summary>
    public string? Subject { get; set; }

    /// <summary>显示名，留空时使用主体标识。</summary>
    public string? UserName { get; set; }

    /// <summary>备注邮箱，可空。</summary>
    public string? Email { get; set; }
}

/// <summary>
/// 把后台账号绑定到主站账号的请求体。
/// </summary>
public sealed class AdminAccountBindRequest
{
    /// <summary>主站账号的邮箱，与注册时填写的一致。</summary>
    public string? UserName { get; set; }
}

/// <summary>
/// 缓存中的后台账号状态，用于权限过滤器与审计日志，避免每次请求都查库。
/// </summary>
/// <param name="IsEnabled">账号是否仍然允许使用后台。</param>
/// <param name="UserName">显示名快照。</param>
public sealed record AdminAccountState(bool IsEnabled, string UserName);

/// <summary>
/// 回调处理结果的阶段。
/// </summary>
public enum AdminSsoCallbackStatus
{
    /// <summary>身份已确认，票据已签发。</summary>
    Success,

    /// <summary>state 不存在或已被使用（可能是伪造的回放）。</summary>
    InvalidState,

    /// <summary>身份源拒绝了授权码交换。</summary>
    UpstreamRejected,

    /// <summary>ID Token 校验失败（签名、签发者、受众、有效期或 nonce 不匹配）。</summary>
    TokenInvalid,

    /// <summary>身份有效，但还没有对应的后台账号。</summary>
    NotProvisioned,

    /// <summary>对应的后台账号存在但已被停用。</summary>
    Disabled,
}

/// <summary>
/// 回调处理结果。
/// </summary>
/// <param name="Status">处理阶段。</param>
/// <param name="Ticket">成功时签发的一次性票据。</param>
/// <param name="ReturnPath">登录发起时携带的后台内路径。</param>
/// <param name="Subject">身份主体标识，未授权时回显给本人以便转交管理员。</param>
/// <param name="Detail">供服务端日志使用的英文说明。</param>
public sealed record AdminSsoCallbackResult(
    AdminSsoCallbackStatus Status,
    string? Ticket = null,
    string? ReturnPath = null,
    string? Subject = null,
    string? Detail = null);
