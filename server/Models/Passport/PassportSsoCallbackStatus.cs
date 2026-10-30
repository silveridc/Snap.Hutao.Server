namespace server.Models.Passport;

public enum PassportSsoCallbackStatus
{
    /// <summary>身份已确认且允许登录，票据已签发.</summary>
    Success,

    /// <summary>state 不存在或已被使用（可能是伪造的回放）.</summary>
    InvalidState,

    /// <summary>身份源拒绝了授权码交换.</summary>
    UpstreamRejected,

    /// <summary>ID Token 校验失败.</summary>
    TokenInvalid,

    /// <summary>身份有效，但还没有对应的后台账号.</summary>
    NotProvisioned,

    /// <summary>对应的后台账号存在但已被停用.</summary>
    Disabled,

    /// <summary>对应的后台账号还没有绑定主站账号.</summary>
    NotBound,
}
