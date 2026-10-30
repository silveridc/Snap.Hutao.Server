namespace server.Models.Admin;

public enum AdminSsoCallbackStatus
{
    /// <summary>身份已确认，票据已签发.</summary>
    Success,

    /// <summary>state 不存在或已被使用（可能是伪造的回放）.</summary>
    InvalidState,

    /// <summary>身份源拒绝了授权码交换.</summary>
    UpstreamRejected,

    /// <summary>ID Token 校验失败（签名、签发者、受众、有效期或 nonce 不匹配）.</summary>
    TokenInvalid,

    /// <summary>身份有效，但还没有对应的后台账号.</summary>
    NotProvisioned,

    /// <summary>对应的后台账号存在但已被停用.</summary>
    Disabled,
}
