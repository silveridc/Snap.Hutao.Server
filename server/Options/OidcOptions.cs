namespace server.Options;

public sealed class OidcOptions
{
    /// <summary>Gets or sets a value indicating whether 总开关.关闭时 <c>/api/admin/sso/*</c> 只返回未启用，写门禁对所有身份放行.</summary>
    public bool Enabled
    {
        get; set;
    }

    /// <summary>Gets or sets 身份源的 Issuer 地址，需能访问 <c>/.well-known/openid-configuration</c>.</summary>
    public string Authority { get; set; } = default!;

    public string ClientId { get; set; } = default!;

    public string ClientSecret { get; set; } = default!;

    /// <summary>Gets or sets 回调绝对地址，必须与身份源里登记的完全一致.</summary>
    public string RedirectUri { get; set; } = default!;

    /// <summary>
    /// Gets or sets 启动器（客户端）单点登录的回调绝对地址，同样要在身份源里登记，与 <see cref="RedirectUri"/> 是两个不同的地址.
    /// 留空时启动器查询 <c>/api/passport/sso/status</c> 得到未启用，登录界面继续只显示邮箱密码登录.
    /// </summary>
    public string? ClientRedirectUri
    {
        get; set;
    }

    /// <summary>Gets or sets 请求的 scope，默认取标准身份 scope.</summary>
    public string Scope { get; set; } = "openid profile email";

    /// <summary>Gets or sets 登录按钮上显示的身份源名称，为空时由前端使用自己的文案.</summary>
    public string? DisplayName
    {
        get; set;
    }

    /// <summary>Gets or sets 种子账号使用的声明名，例如 <c>sub</c>、<c>preferred_username</c>、<c>email</c>.</summary>
    public string BootstrapClaim { get; set; } = "sub";

    /// <summary>Gets or sets 种子账号的声明值.仅当后台还没有任何种子账号时，首次登录会建立账号.</summary>
    public string BootstrapValue { get; set; } = default!;

    /// <summary>Gets or sets a value indicating whether 是否要求身份源的元数据与密钥地址为 HTTPS.本地 http 联调时置为 false.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;
}
