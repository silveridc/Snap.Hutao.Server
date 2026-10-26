namespace server.Models.Admin;

public sealed class AdminSsoStatus
{
    public bool Enabled { get; set; }

    public string? DisplayName { get; set; }
}

public sealed class AdminSsoTokenResponse
{
    public string AccessToken { get; set; } = default!;

    public int ExpiresIn { get; set; }

    public AdminSsoAccount Principal { get; set; } = default!;
}

public sealed class AdminSsoAccount
{
    public int Id { get; set; }

    public string UserName { get; set; } = default!;

    public string? Email { get; set; }

    public string Provider { get; set; } = default!;

    public string Subject { get; set; } = default!;

    public bool IsBootstrap { get; set; }
}

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

public sealed class AdminSsoExchangeRequest
{
    public string? Ticket { get; set; }
}

public sealed class AdminAccountAddRequest
{
    /// <summary>身份源中的主体标识（OIDC 的 sub）。</summary>
    public string? Subject { get; set; }

    /// <summary>显示名，留空时使用主体标识。</summary>
    public string? UserName { get; set; }

    /// <summary>备注邮箱，可空。</summary>
    public string? Email { get; set; }
}

public sealed class AdminAccountBindRequest
{
    /// <summary>主站账号的邮箱，与注册时填写的一致。</summary>
    public string? UserName { get; set; }
}

public sealed record AdminAccountState(bool IsEnabled, string UserName);

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

public sealed record AdminSsoCallbackResult(
    AdminSsoCallbackStatus Status,
    string? Ticket = null,
    string? ReturnPath = null,
    string? Subject = null,
    string? Detail = null);
