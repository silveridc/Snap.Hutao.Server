using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using server.Extension;
using server.Models.Admin;
using server.Models.Entity;
using server.Models.Passport;

namespace server.Services.Admin;

/// <summary>
/// 单点登录：外部 OIDC 授权码流程、后台专属账号、后台令牌签发、启动器登录身份与权限判定所需的状态查询。
/// <para>
/// 授权码流程带 PKCE(S256)；state 与 nonce 存放在分布式缓存中且一次性使用；
/// ID Token 的签名、签发者、受众、有效期由 <see cref="JsonWebTokenHandler"/> 校验，nonce 由本类比对。
/// 后台令牌沿用主站的 HS256 密钥签发，但只含 <see cref="AdminClaimTypes.AccountId"/>，
/// 不含主站用户身份，因此不会误入任何以用户身份取数的代码路径。
/// 启动器登录走同一套协议，但落地身份是绑定的主站账号，令牌由 <see cref="PassportService"/> 签发。
/// </para>
/// </summary>
public sealed class AdminSsoService(
    AppDbContext appDbContext,
    AppOptions appOptions,
    SystemConfigService systemConfig,
    OidcMetadata oidcMetadata,
    IDistributedCache cache,
    IHttpClientFactory httpClientFactory,
    ILogger<AdminSsoService> logger,
    TimeProvider timeProvider
)
{
    /// <summary>当前只对接 OIDC 一种身份源，该字段为将来的其它身份源保留。</summary>
    public const string ProviderName = "oidc";

    private const int _accessTokenExpirationMinutes = 60;
    private const int _subjectMaxLength = 256;
    private const int _userNameMaxLength = 128;
    private const int _emailMaxLength = 256;
    private const string _stateKeyPrefix = "Admin:Sso:State:";
    private const string _ticketKeyPrefix = "Admin:Sso:Ticket:";
    private const string _accountKeyPrefix = "Admin:Account:";
    private const string _clientStateKeyPrefix = "Passport:Sso:State:";
    private const string _clientTicketKeyPrefix = "Passport:Sso:Ticket:";
    private const string _boundKeyPrefix = "Admin:Bound:";

    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TicketTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AccountTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan UpstreamTimeout = TimeSpan.FromSeconds(10);

    private readonly SymmetricSecurityKey _jwtSigningKey = appOptions.GetJwtSecurityKey();

    /// <summary>后台 SSO 是否启用。关闭时不做写操作门禁，后台沿用主站维护者账号。</summary>
    public bool IsEnabled => systemConfig.Oidc.Enabled;

    /// <summary>登录按钮上显示的身份源名称，未配置时为 null。</summary>
    public string? DisplayName => IsEnabled ? systemConfig.Oidc.DisplayName : null;

    /// <summary>
    /// 启动器（客户端）SSO 是否可用。除了总开关，还必须单独配置客户端回调地址，
    /// 启动器据此决定登录界面是显示 SSO 入口还是只走邮箱密码登录。
    /// </summary>
    public bool IsClientLoginEnabled => IsEnabled && !string.IsNullOrWhiteSpace(systemConfig.Oidc.ClientRedirectUri);

    /// <summary>
    /// 生成跳转身份源的授权地址，并把 nonce、code_verifier 与回跳路径按 state 暂存。
    /// </summary>
    /// <param name="returnPath">登录完成后要回到的后台内路径，可为空。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>授权地址；配置或身份源不可用时返回 null 与英文原因。</returns>
    public Task<(string? Url, string? Error)> CreateAuthorizeUrlAsync(string? returnPath, CancellationToken cancellationToken = default)
    {
        return CreateAuthorizeUrlAsync(systemConfig.Oidc.RedirectUri, _stateKeyPrefix, returnPath, cancellationToken);
    }

    /// <summary>
    /// 生成启动器登录用的授权地址。回调地址与后台不同，身份源里要登记成两个。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>授权地址；未启用或身份源不可用时返回 null 与英文原因。</returns>
    public Task<(string? Url, string? Error)> CreateClientAuthorizeUrlAsync(CancellationToken cancellationToken = default)
    {
        return CreateAuthorizeUrlAsync(systemConfig.Oidc.ClientRedirectUri ?? string.Empty, _clientStateKeyPrefix, null, cancellationToken);
    }

    /// <summary>
    /// 处理身份源回调：校验 state → 用授权码换取令牌 → 校验 ID Token → 建立或读取后台账号 → 签发一次性票据。
    /// </summary>
    /// <param name="code">身份源返回的授权码。</param>
    /// <param name="state">身份源原样带回的 state。</param>
    /// <param name="error">身份源直接返回的错误名（用户拒绝授权等）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>处理结果。</returns>
    public async Task<AdminSsoCallbackResult> HandleCallbackAsync(string? code, string? state, string? error, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return new(AdminSsoCallbackStatus.InvalidState, Detail: "Admin SSO is not enabled");
        }

        (Dictionary<string, string?>? claims, string? returnPath, FlowFailure failure, string? detail) =
            await CompleteAuthorizationAsync(code, state, error, _stateKeyPrefix, systemConfig.Oidc.RedirectUri, cancellationToken).ConfigureAwait(false);

        if (failure is not FlowFailure.None)
        {
            return new(ToAdminStatus(failure), ReturnPath: returnPath, Detail: detail);
        }

        return await ProvisionAsync(claims!, returnPath, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 处理启动器登录的身份源回调：校验 state → 换取并校验 ID Token → 确认这个身份绑定了哪个主站账号 → 签发一次性票据。
    /// </summary>
    /// <param name="code">身份源返回的授权码。</param>
    /// <param name="state">身份源原样带回的 state。</param>
    /// <param name="error">身份源直接返回的错误名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>处理结果。</returns>
    public async Task<PassportSsoCallbackResult> HandleClientCallbackAsync(string? code, string? state, string? error, CancellationToken cancellationToken = default)
    {
        if (!IsClientLoginEnabled)
        {
            return new(PassportSsoCallbackStatus.InvalidState, Detail: "Client SSO login is not enabled");
        }

        (Dictionary<string, string?>? claims, string? _, FlowFailure failure, string? detail) =
            await CompleteAuthorizationAsync(code, state, error, _clientStateKeyPrefix, systemConfig.Oidc.ClientRedirectUri ?? string.Empty, cancellationToken).ConfigureAwait(false);

        if (failure is not FlowFailure.None)
        {
            return new(ToClientStatus(failure), Detail: detail);
        }

        return await ProvisionClientTicketAsync(claims!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 用启动器登录的一次性票据换取要登录的主站账号。票据只能用一次，账号与绑定关系在使用时重新读库，
    /// 所以回调与换取之间被停用或解绑的账号换不出令牌。
    /// </summary>
    /// <param name="ticket">回调阶段签发的票据。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>主站账号与对应的后台账号；票据无效或身份已不可用时为 null。</returns>
    public async Task<(HutaoUser? User, AdminAccount? Account)> TakeClientTicketAsync(string ticket, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ticket))
        {
            return (null, null);
        }

        int? accountId = await TakeAsync<int?>(_clientTicketKeyPrefix + ticket, cancellationToken).ConfigureAwait(false);
        if (accountId is null)
        {
            logger.LogWarning("Rejected a client SSO ticket exchange with an unknown ticket");
            return (null, null);
        }

        AdminAccount? account = await appDbContext.AdminAccounts
            .AsNoTracking()
            .Where(a => a.Id == accountId && a.IsEnabled && a.UserId != null)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (account?.UserId is not int boundUserId)
        {
            return (null, null);
        }

        HutaoUser? user = await appDbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == boundUserId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return (null, null);
        }

        appDbContext.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminUserId = 0,
            AdminAccountId = account.Id,
            AdminAccountName = account.UserName,
            Action = "/Passport/Sso/Login",
            Detail = user.UserName,
            Success = true,
            Timestamp = timeProvider.GetUnixTimeSeconds(),
        });
        await appDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (user, account);
    }

    /// <summary>
    /// 主站账号是否被某个可用的后台账号绑定，用于写操作门禁与维护者判定，结果缓存 15 分钟。
    /// </summary>
    /// <param name="userId">主站账号 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>是否绑定。</returns>
    public async Task<bool> IsUserProvisionedAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await cache.GetOrCreateAsync(_boundKeyPrefix + userId, AccountTtl, async () =>
            await appDbContext.AdminAccounts
                .AsNoTracking()
                .AnyAsync(a => a.UserId == userId && a.IsEnabled, cancellationToken)
                .ConfigureAwait(false)).ConfigureAwait(false);
    }

    private static AdminSsoCallbackStatus ToAdminStatus(FlowFailure failure)
    {
        return failure switch
        {
            FlowFailure.UpstreamRejected => AdminSsoCallbackStatus.UpstreamRejected,
            FlowFailure.TokenInvalid => AdminSsoCallbackStatus.TokenInvalid,
            _ => AdminSsoCallbackStatus.InvalidState,
        };
    }

    private static PassportSsoCallbackStatus ToClientStatus(FlowFailure failure)
    {
        return failure switch
        {
            FlowFailure.UpstreamRejected => PassportSsoCallbackStatus.UpstreamRejected,
            FlowFailure.TokenInvalid => PassportSsoCallbackStatus.TokenInvalid,
            _ => PassportSsoCallbackStatus.InvalidState,
        };
    }

    /// <summary>
    /// 走完「state → 授权码 → ID Token」这段与业务无关的协议流程，返回已校验的声明。
    /// </summary>
    private async Task<(Dictionary<string, string?>? Claims, string? ReturnPath, FlowFailure Failure, string? Detail)> CompleteAuthorizationAsync(
        string? code,
        string? state,
        string? error,
        string stateKeyPrefix,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        PendingLogin? pending = string.IsNullOrEmpty(state)
            ? null
            : await TakeAsync<PendingLogin>(stateKeyPrefix + state, cancellationToken).ConfigureAwait(false);
        if (pending is null)
        {
            // 没有对应的 state：伪造回调、过期回调，或者同一个回调地址被刷新过一次
            logger.LogWarning("Rejected an SSO callback with unknown or consumed state");
            return (null, null, FlowFailure.InvalidState, "The login attempt has expired or was already used");
        }

        if (!string.IsNullOrEmpty(error))
        {
            return (null, pending.ReturnPath, FlowFailure.UpstreamRejected, error);
        }

        if (string.IsNullOrEmpty(code))
        {
            return (null, pending.ReturnPath, FlowFailure.UpstreamRejected, "The identity provider returned neither a code nor an error");
        }

        OidcOptions oidc = systemConfig.Oidc;
        try
        {
            OpenIdConnectConfiguration configuration = await oidcMetadata.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            (string? idToken, string? tokenError) = await RequestTokensAsync(configuration, oidc, code, pending.CodeVerifier, redirectUri, cancellationToken).ConfigureAwait(false);
            if (idToken is null)
            {
                return (null, pending.ReturnPath, FlowFailure.UpstreamRejected, tokenError);
            }

            if (!await IsIdTokenValidAsync(idToken, configuration, oidc).ConfigureAwait(false))
            {
                return (null, pending.ReturnPath, FlowFailure.TokenInvalid, "The identity token failed validation");
            }

            // 签名已验证，载荷可以本地解码
            Dictionary<string, string?> claims = ReadClaims(idToken);
            if (!string.Equals(GetClaim(claims, "nonce"), pending.Nonce, StringComparison.Ordinal))
            {
                return (null, pending.ReturnPath, FlowFailure.TokenInvalid, "The identity token nonce does not match the login attempt");
            }

            return (claims, pending.ReturnPath, FlowFailure.None, null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or FormatException or ArgumentException)
        {
            logger.LogWarning(ex, "Failed to complete the SSO authorization code flow");
            return (null, pending.ReturnPath, FlowFailure.UpstreamRejected, "The identity provider could not be reached");
        }
    }

    /// <summary>
    /// 身份有效之后，确认这个主体在启动器侧允许登录成哪个主站账号。
    /// </summary>
    private async Task<PassportSsoCallbackResult> ProvisionClientTicketAsync(Dictionary<string, string?> claims, CancellationToken cancellationToken)
    {
        string? subject = GetClaim(claims, "sub");
        if (string.IsNullOrEmpty(subject) || subject.Length > _subjectMaxLength)
        {
            return new(PassportSsoCallbackStatus.TokenInvalid, Detail: "The identity token has no usable subject claim");
        }

        AdminAccount? account = await appDbContext.AdminAccounts
            .AsNoTracking()
            .Where(a => a.Provider == ProviderName && a.Subject == subject)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            logger.LogWarning("Rejected a client SSO login from unprovisioned subject {Subject}", subject);
            return new(PassportSsoCallbackStatus.NotProvisioned, Subject: subject, Detail: "This identity has no admin account yet");
        }

        if (!account.IsEnabled)
        {
            return new(PassportSsoCallbackStatus.Disabled, Subject: subject, Detail: "This admin account is disabled");
        }

        if (account.UserId is null)
        {
            return new(PassportSsoCallbackStatus.NotBound, Subject: subject, Detail: "This admin account is not bound to a Hutao account");
        }

        string ticket = RandomSegment(48);
        await SetAsync(_clientTicketKeyPrefix + ticket, account.Id, TicketTtl, cancellationToken).ConfigureAwait(false);

        return new(PassportSsoCallbackStatus.Success, Ticket: ticket, Subject: subject);
    }

    /// <summary>
    /// 授权流程失败的阶段，与两个业务侧的结果状态无关。
    /// </summary>
    private enum FlowFailure
    {
        /// <summary>流程走通，声明可用。</summary>
        None,

        /// <summary>state 不存在或已被使用。</summary>
        InvalidState,

        /// <summary>身份源拒绝或无法访问。</summary>
        UpstreamRejected,

        /// <summary>ID Token 校验失败。</summary>
        TokenInvalid,
    }

    /// <summary>
    /// 用一次性票据换取后台令牌。票据无论成功与否都只能使用一次。
    /// </summary>
    /// <param name="ticket">回调阶段签发的票据。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>后台令牌；票据无效或账号已不可用时为 null。</returns>
    public async Task<AdminSsoTokenResponse?> RedeemTicketAsync(string ticket, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(ticket))
        {
            return null;
        }

        int? accountId = await TakeAsync<int?>(_ticketKeyPrefix + ticket, cancellationToken).ConfigureAwait(false);
        if (accountId is null)
        {
            logger.LogWarning("Rejected an admin SSO ticket exchange with an unknown ticket");
            return null;
        }

        AdminAccount? account = await appDbContext.AdminAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == accountId && a.IsEnabled, cancellationToken)
            .ConfigureAwait(false);
        if (account is null)
        {
            return null;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        appDbContext.AdminAuditLogs.Add(new AdminAuditLog
        {
            AdminUserId = 0,
            AdminAccountId = account.Id,
            AdminAccountName = account.UserName,
            Action = "/Admin/Sso/Login",
            Detail = null,
            Success = true,
            Timestamp = now.ToUnixTimeSeconds(),
        });
        await appDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdminSsoTokenResponse
        {
            AccessToken = CreateAccessToken(account),
            ExpiresIn = _accessTokenExpirationMinutes * 60,
            Principal = ToPrincipal(account),
        };
    }

    /// <summary>
    /// 权限过滤器与审计日志使用：后台账号是否存在且仍然启用，结果缓存 15 分钟。
    /// </summary>
    /// <param name="accountId">后台账号 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>账号状态；账号不存在时缓存并返回 null。</returns>
    public async Task<AdminAccountState?> GetAccountStateAsync(int accountId, CancellationToken cancellationToken = default)
    {
        return await cache.GetOrCreateAsync(_accountKeyPrefix + accountId, AccountTtl, async () =>
            await appDbContext.AdminAccounts
                .AsNoTracking()
                .Where(a => a.Id == accountId)
                .Select(a => new AdminAccountState(a.IsEnabled, a.UserName))
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <summary>
    /// 判断后台账号是否允许继续操作。
    /// </summary>
    /// <param name="accountId">后台账号 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>是否可以放行。</returns>
    public async Task<bool> IsAccountUsableAsync(int accountId, CancellationToken cancellationToken = default)
    {
        return await GetAccountStateAsync(accountId, cancellationToken).ConfigureAwait(false) is { IsEnabled: true };
    }

    /// <summary>
    /// 后台账号列表。
    /// </summary>
    /// <param name="currentAccountId">当前调用者，用于标记「本人」。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>账号列表。</returns>
    public async Task<List<AdminAccountItem>> ListAsync(int? currentAccountId, CancellationToken cancellationToken = default)
    {
        List<AdminAccount> accounts = await appDbContext.AdminAccounts
            .AsNoTracking()
            .Include(a => a.User)
            .OrderBy(a => a.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. accounts.Select(a => new AdminAccountItem
            {
                Id = a.Id,
                Provider = a.Provider,
                Subject = a.Subject,
                UserName = a.UserName,
                Email = a.Email,
                IsEnabled = a.IsEnabled,
                IsBootstrap = a.IsBootstrap,
                CreatedAt = a.CreatedAt,
                LastLoginAt = a.LastLoginAt,
                BoundUserId = a.UserId,
                BoundUserName = a.User?.UserName,
                IsCurrent = a.Id == currentAccountId,
            }),
        ];
    }

    /// <summary>
    /// 新增后台账号。身份源只负责证明「你是谁」，能不能进后台由这张表决定。
    /// </summary>
    /// <param name="subject">身份源中的主体标识（OIDC 的 sub）。</param>
    /// <param name="userName">显示名。</param>
    /// <param name="email">备注邮箱，可空。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>返回码、英文说明与本地化键。</returns>
    public async Task<(ReturnCode Code, string Message, string? LocalizationKey)> AddAsync(string subject, string? userName, string? email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return (ReturnCode.InvalidUploadData, "The identity subject is required", ServerKeys.ServerAdminAccountSubjectRequired);
        }

        subject = subject.Trim();
        if (subject.Length > _subjectMaxLength)
        {
            return (ReturnCode.InvalidUploadData, "The identity subject is too long", ServerKeys.ServerAdminAccountSubjectRequired);
        }

        if (await appDbContext.AdminAccounts.AnyAsync(a => a.Provider == ProviderName && a.Subject == subject, cancellationToken).ConfigureAwait(false))
        {
            return (ReturnCode.InvalidUploadData, "An admin account for this identity already exists", ServerKeys.ServerAdminAccountAlreadyExists);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        AdminAccount account = new()
        {
            Provider = ProviderName,
            Subject = subject,
            UserName = string.IsNullOrWhiteSpace(userName) ? subject : Truncate(userName.Trim(), _userNameMaxLength),
            Email = string.IsNullOrWhiteSpace(email) ? null : Truncate(email.Trim(), _emailMaxLength),
            IsEnabled = true,
            CreatedAt = now,
        };

        appDbContext.AdminAccounts.Add(account);
        await appDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await InvalidateAsync(account, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Added admin account {UserName} (id {Id}, subject {Subject})", account.UserName, account.Id, account.Subject);

        return (ReturnCode.Success, "Admin account added successfully", ServerKeys.ServerAdminAccountAddSucceed);
    }

    /// <summary>
    /// 启用或停用后台账号。不允许停用自己，也不允许停用最后一个可用账号。
    /// </summary>
    /// <param name="accountId">目标账号。</param>
    /// <param name="enabled">目标状态。</param>
    /// <param name="currentAccountId">当前调用者。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>返回码、英文说明与本地化键。</returns>
    public async Task<(ReturnCode Code, string Message, string? LocalizationKey)> SetEnabledAsync(int accountId, bool enabled, int currentAccountId, CancellationToken cancellationToken = default)
    {
        AdminAccount? account = await appDbContext.AdminAccounts.SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return (ReturnCode.NotFound, "Admin account does not exist", ServerKeys.ServerAdminAccountNotFound);
        }

        if (!enabled)
        {
            if (accountId == currentAccountId)
            {
                return (ReturnCode.InvalidUploadData, "You cannot disable the admin account you are using", ServerKeys.ServerAdminAccountSelfOperation);
            }

            if (await appDbContext.AdminAccounts.CountAsync(a => a.IsEnabled && a.Id != accountId, cancellationToken).ConfigureAwait(false) is 0)
            {
                return (ReturnCode.InvalidUploadData, "At least one enabled admin account must remain", ServerKeys.ServerAdminAccountLastEnabled);
            }
        }

        account.IsEnabled = enabled;
        await appDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await InvalidateAsync(account, cancellationToken).ConfigureAwait(false);

        return (ReturnCode.Success, enabled
            ? "Admin account enabled successfully"
            : "Admin account disabled successfully", ServerKeys.ServerAdminAccountSetEnabledSucceed);
    }

    /// <summary>
    /// 删除后台账号。不允许删除自己，也不允许删除到没有任何可用账号。
    /// </summary>
    /// <param name="accountId">目标账号。</param>
    /// <param name="currentAccountId">当前调用者。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>返回码、英文说明与本地化键。</returns>
    public async Task<(ReturnCode Code, string Message, string? LocalizationKey)> RemoveAsync(int accountId, int currentAccountId, CancellationToken cancellationToken = default)
    {
        AdminAccount? account = await appDbContext.AdminAccounts.SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return (ReturnCode.NotFound, "Admin account does not exist", ServerKeys.ServerAdminAccountNotFound);
        }

        if (accountId == currentAccountId)
        {
            return (ReturnCode.InvalidUploadData, "You cannot remove the admin account you are using", ServerKeys.ServerAdminAccountSelfOperation);
        }

        if (account.IsEnabled && await appDbContext.AdminAccounts.CountAsync(a => a.IsEnabled && a.Id != accountId, cancellationToken).ConfigureAwait(false) is 0)
        {
            return (ReturnCode.InvalidUploadData, "At least one enabled admin account must remain", ServerKeys.ServerAdminAccountLastEnabled);
        }

        appDbContext.AdminAccounts.Remove(account);
        await appDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await InvalidateAsync(account, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Removed admin account {UserName} (id {Id})", account.UserName, accountId);

        return (ReturnCode.Success, "Admin account removed successfully", ServerKeys.ServerAdminAccountRemoveSucceed);
    }

    /// <summary>
    /// 使账号状态缓存失效（启用/停用/删除/新建/改绑后调用）。
    /// 绑定关系变了也要一起清掉，否则写操作门禁会按旧关系放行或拒绝。
    /// </summary>
    /// <param name="account">受影响的后台账号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>异步任务。</returns>
    public async Task InvalidateAsync(AdminAccount account, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(_accountKeyPrefix + account.Id, cancellationToken).ConfigureAwait(false);
        if (account.UserId is int userId)
        {
            await cache.RemoveAsync(_boundKeyPrefix + userId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 把后台账号绑定到一个主站账号，这个后台账号从此可以用单点登录进启动器。
    /// </summary>
    /// <param name="accountId">目标后台账号。</param>
    /// <param name="userName">主站账号的邮箱。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>返回码、英文说明与本地化键。</returns>
    public async Task<(ReturnCode Code, string Message, string? LocalizationKey)> BindAsync(int accountId, string? userName, CancellationToken cancellationToken = default)
    {
        AdminAccount? account = await appDbContext.AdminAccounts.SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return (ReturnCode.NotFound, "Admin account does not exist", ServerKeys.ServerAdminAccountNotFound);
        }

        if (string.IsNullOrWhiteSpace(userName))
        {
            return (ReturnCode.InvalidUploadData, "The Hutao account is required", ServerKeys.ServerAdminAccountUserRequired);
        }

        HutaoUser? user = await appDbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedUserName == userName.Trim().ToUpperInvariant(), cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return (ReturnCode.NotFound, "The Hutao account does not exist", ServerKeys.ServerAdminAccountUserNotFound);
        }

        if (await appDbContext.AdminAccounts.AnyAsync(a => a.UserId == user.Id && a.Id != accountId, cancellationToken).ConfigureAwait(false))
        {
            return (ReturnCode.InvalidUploadData, "This Hutao account is already bound to another admin account", ServerKeys.ServerAdminAccountUserTaken);
        }

        int? previous = account.UserId;
        account.UserId = user.Id;
        await appDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await InvalidateAsync(account, cancellationToken).ConfigureAwait(false);
        if (previous is int previousId && previousId != user.Id)
        {
            await cache.RemoveAsync(_boundKeyPrefix + previousId, cancellationToken).ConfigureAwait(false);
        }

        logger.LogInformation("Bound admin account {UserName} (id {Id}) to Hutao account {HutaoUserName}", account.UserName, account.Id, user.UserName);

        return (ReturnCode.Success, "Admin account bound successfully", ServerKeys.ServerAdminAccountBindSucceed);
    }

    /// <summary>
    /// 解除后台账号与主站账号的绑定。后台登录不受影响，只是不能再用于启动器登录。
    /// </summary>
    /// <param name="accountId">目标后台账号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>返回码、英文说明与本地化键。</returns>
    public async Task<(ReturnCode Code, string Message, string? LocalizationKey)> UnbindAsync(int accountId, CancellationToken cancellationToken = default)
    {
        AdminAccount? account = await appDbContext.AdminAccounts.SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return (ReturnCode.NotFound, "Admin account does not exist", ServerKeys.ServerAdminAccountNotFound);
        }

        if (account.UserId is not int userId)
        {
            return (ReturnCode.InvalidUploadData, "This admin account is not bound to a Hutao account", ServerKeys.ServerAdminAccountUserUnbound);
        }

        account.UserId = null;
        await appDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await cache.RemoveAsync(_boundKeyPrefix + userId, cancellationToken).ConfigureAwait(false);
        await InvalidateAsync(account, cancellationToken).ConfigureAwait(false);

        return (ReturnCode.Success, "Admin account unbound successfully", ServerKeys.ServerAdminAccountUnbindSucceed);
    }

    private async Task<(string? Url, string? Error)> CreateAuthorizeUrlAsync(string redirectUri, string stateKeyPrefix, string? returnPath, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return (null, "SSO is not enabled");
        }

        OidcOptions oidc = systemConfig.Oidc;
        OpenIdConnectConfiguration configuration;
        try
        {
            configuration = await oidcMetadata.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Failed to load OIDC metadata for SSO");
            return (null, "The identity provider metadata is unavailable");
        }

        if (string.IsNullOrEmpty(configuration.AuthorizationEndpoint))
        {
            return (null, "The identity provider does not publish an authorization endpoint");
        }

        string state = RandomSegment(32);
        string nonce = RandomSegment(32);
        string codeVerifier = RandomSegment(48);
        await SetAsync(stateKeyPrefix + state, new PendingLogin(nonce, codeVerifier, SanitizeReturnPath(returnPath)), StateTtl, cancellationToken).ConfigureAwait(false);

        Dictionary<string, string?> query = new()
        {
            ["client_id"] = oidc.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = oidc.Scope,
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = CreateCodeChallenge(codeVerifier),
            ["code_challenge_method"] = "S256",
        };

        return (QueryHelpers.AddQueryString(configuration.AuthorizationEndpoint, query), null);
    }

    private static string RandomSegment(int byteLength)
    {
        byte[] bytes = new byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncoder.Encode(bytes);
    }

    private static string CreateCodeChallenge(string codeVerifier)
    {
        return Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
    }

    /// <summary>
    /// 只接受站内绝对路径，拒绝协议相对地址与超长值，避免成为开放重定向。
    /// </summary>
    private static string? SanitizeReturnPath(string? returnPath)
    {
        if (string.IsNullOrEmpty(returnPath)
            || returnPath.Length > 256
            || !returnPath.StartsWith("/", StringComparison.Ordinal)
            || returnPath.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        return returnPath;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static Dictionary<string, string?> ReadClaims(string compactJwt)
    {
        string[] segments = compactJwt.Split('.');
        if (segments.Length is not 3)
        {
            return [];
        }

        Dictionary<string, string?> claims = [];
        using JsonDocument document = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(segments[1]));
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            claims[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Null => null,
                _ => property.Value.GetRawText(),
            };
        }

        return claims;
    }

    private static string? GetClaim(IReadOnlyDictionary<string, string?> claims, string name)
    {
        if (claims.TryGetValue(name, out string? value))
        {
            return value;
        }

        // 个别身份源会在自定义声明上改变大小写，这里做一次不区分大小写的兜底
        foreach ((string key, string? claimValue) in claims)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return claimValue;
            }
        }

        return null;
    }

    private async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        DistributedCacheEntryOptions options = new() { AbsoluteExpirationRelativeToNow = ttl };
        await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value), options, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T?> TakeAsync<T>(string key, CancellationToken cancellationToken)
    {
        byte[]? bytes = await cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return default;
        }

        await cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(bytes);
    }

    private async Task<(string? IdToken, string? Error)> RequestTokensAsync(OpenIdConnectConfiguration configuration, OidcOptions oidc, string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(configuration.TokenEndpoint))
        {
            return (null, "The identity provider does not publish a token endpoint");
        }

        Dictionary<string, string> form = new()
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = oidc.ClientId,
            ["client_secret"] = oidc.ClientSecret,
            ["code_verifier"] = codeVerifier,
        };

        using HttpClient httpClient = httpClientFactory.CreateClient(nameof(AdminSsoService));
        httpClient.Timeout = UpstreamTimeout;
        using HttpResponseMessage response = await httpClient.PostAsync(configuration.TokenEndpoint, new FormUrlEncodedContent(form), cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Admin SSO token endpoint returned {Status}", (int)response.StatusCode);
            return (null, "The identity provider rejected the authorization code");
        }

        using JsonDocument document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("id_token", out JsonElement idTokenElement) || idTokenElement.ValueKind is not JsonValueKind.String)
        {
            return (null, "The identity provider did not return an identity token");
        }

        return (idTokenElement.GetString(), null);
    }

    private async Task<bool> IsIdTokenValidAsync(string idToken, OpenIdConnectConfiguration configuration, OidcOptions oidc)
    {
        TokenValidationParameters parameters = new()
        {
            ValidateIssuer = true,
            ValidIssuer = configuration.Issuer,
            ValidateAudience = true,
            ValidAudience = oidc.ClientId,
            RequireAudience = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            SaveSigninToken = false,
        };

        TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(idToken, parameters).ConfigureAwait(false);
        if (!result.IsValid)
        {
            logger.LogWarning("Rejected an admin SSO identity token: {Reason}", result.Exception?.Message ?? "unknown");
            return false;
        }

        return true;
    }

    private async Task<AdminSsoCallbackResult> ProvisionAsync(Dictionary<string, string?> claims, string? returnPath, CancellationToken cancellationToken)
    {
        string? subject = GetClaim(claims, "sub");
        if (string.IsNullOrEmpty(subject) || subject.Length > _subjectMaxLength)
        {
            return new(AdminSsoCallbackStatus.TokenInvalid, ReturnPath: returnPath, Detail: "The identity token has no usable subject claim");
        }

        string displayName = GetClaim(claims, "preferred_username")
            ?? GetClaim(claims, "name")
            ?? GetClaim(claims, "email")
            ?? subject;
        string? email = GetClaim(claims, "email");

        AdminAccount? account = await appDbContext.AdminAccounts
            .SingleOrDefaultAsync(a => a.Provider == ProviderName && a.Subject == subject, cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset now = timeProvider.GetUtcNow();

        if (account is null)
        {
            OidcOptions oidc = systemConfig.Oidc;
            bool matchesBootstrap = !string.IsNullOrEmpty(oidc.BootstrapValue)
                && string.Equals(GetClaim(claims, oidc.BootstrapClaim), oidc.BootstrapValue, StringComparison.Ordinal);
            bool seedTaken = await appDbContext.AdminAccounts.AnyAsync(a => a.Provider == ProviderName && a.IsBootstrap, cancellationToken).ConfigureAwait(false);

            if (!matchesBootstrap || seedTaken)
            {
                logger.LogWarning("Rejected an admin SSO login from unprovisioned subject {Subject} ({DisplayName})", subject, displayName);
                return new(AdminSsoCallbackStatus.NotProvisioned, ReturnPath: returnPath, Subject: subject, Detail: "This identity has no admin account yet");
            }

            account = new AdminAccount
            {
                Provider = ProviderName,
                Subject = subject,
                UserName = Truncate(displayName, _userNameMaxLength),
                Email = string.IsNullOrEmpty(email) ? null : Truncate(email, _emailMaxLength),
                IsEnabled = true,
                IsBootstrap = true,
                CreatedAt = now,
                LastLoginAt = now,
            };

            appDbContext.AdminAccounts.Add(account);
            await appDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Bootstrapped the first admin account {UserName} (id {Id}) from claim {Claim}", account.UserName, account.Id, oidc.BootstrapClaim);
        }
        else if (!account.IsEnabled)
        {
            return new(AdminSsoCallbackStatus.Disabled, ReturnPath: returnPath, Subject: subject, Detail: "This admin account is disabled");
        }
        else
        {
            account.LastLoginAt = now;
            await appDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            // 显示名可能已在身份源里改过，缓存里的旧名字会误导审计
            await InvalidateAsync(account, cancellationToken).ConfigureAwait(false);
        }

        string ticket = RandomSegment(48);
        await SetAsync(_ticketKeyPrefix + ticket, account.Id, TicketTtl, cancellationToken).ConfigureAwait(false);

        return new(AdminSsoCallbackStatus.Success, Ticket: ticket, ReturnPath: returnPath, Subject: subject);
    }

    private string CreateAccessToken(AdminAccount account)
    {
        JwtSecurityTokenHandler handler = new();
        ClaimsIdentity identity = new([new(AdminClaimTypes.AccountId, account.Id.ToString())]);
        SecurityTokenDescriptor descriptor = new()
        {
            Subject = identity,
            Expires = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(_accessTokenExpirationMinutes),
            Issuer = appOptions.JWTIssuer,
            SigningCredentials = new(_jwtSigningKey, SecurityAlgorithms.HmacSha256Signature),
        };

        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private static AdminSsoAccount ToPrincipal(AdminAccount account)
    {
        return new AdminSsoAccount
        {
            Id = account.Id,
            UserName = account.UserName,
            Email = account.Email,
            Provider = account.Provider,
            Subject = account.Subject,
            IsBootstrap = account.IsBootstrap,
        };
    }

    /// <summary>
    /// 发起登录时暂存的上下文，随 state 一起存放，取用即失效。
    /// </summary>
    /// <param name="Nonce">写入授权请求的 nonce。</param>
    /// <param name="CodeVerifier">PKCE 校验器。</param>
    /// <param name="ReturnPath">登录成功后的回跳路径。</param>
    private sealed record PendingLogin(string Nonce, string CodeVerifier, string? ReturnPath);
}
