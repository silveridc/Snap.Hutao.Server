using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using server.Models.Admin;
using server.Models.Entity;
using server.Models.Passport;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace server.Services.Admin;

public sealed class AdminSsoService(
    AppDbContext appDbContext,
    AppOptions appOptions,
    SystemConfigService systemConfig,
    OidcMetadata oidcMetadata,
    IDistributedCache cache,
    IHttpClientFactory httpClientFactory,
    ILogger<AdminSsoService> logger,
    TimeProvider timeProvider)
{
    /// <summary>
    /// The name of the identity provider. Only OIDC is supported today; the field is reserved for future providers.
    /// </summary>
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

    private enum FlowFailure
    {
        /// <summary>The flow completed and the claims are usable.</summary>
        None,

        /// <summary>The state is missing or has already been consumed.</summary>
        InvalidState,

        /// <summary>The identity provider rejected the request or is unreachable.</summary>
        UpstreamRejected,

        /// <summary>The identity token failed validation.</summary>
        TokenInvalid,
    }

    /// <summary>
    /// Gets a value indicating whether admin SSO is enabled. When it is disabled, write-operation gating is skipped
    /// and the back office keeps using Hutao maintainer accounts.
    /// </summary>
    public bool IsEnabled => systemConfig.Oidc.Enabled;

    /// <summary>
    /// Gets the identity provider display name shown on the sign-in button, or <see langword="null"/> when it is not configured.
    /// </summary>
    public string? DisplayName => IsEnabled ? systemConfig.Oidc.DisplayName : null;

    /// <summary>
    /// Gets a value indicating whether launcher (client) SSO is available. Besides the master switch, a separate client
    /// redirect URI must be configured; the launcher uses it to decide whether to show the SSO entry or only password sign-in.
    /// </summary>
    public bool IsClientLoginEnabled => IsEnabled && !string.IsNullOrWhiteSpace(systemConfig.Oidc.ClientRedirectUri);

    /// <summary>
    /// Creates the authorize URL that redirects to the identity provider and stores the nonce, code verifier,
    /// and return path under the generated state.
    /// </summary>
    /// <param name="returnPath">The back-office relative path to return to after sign-in; may be <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authorize URL, or <see langword="null"/> and an English reason when configuration or the provider is unavailable.</returns>
    public Task<(string? Url, string? Error)> CreateAuthorizeUrlAsync(string? returnPath, CancellationToken cancellationToken = default)
    {
        return CreateAuthorizeUrlAsync(systemConfig.Oidc.RedirectUri, _stateKeyPrefix, returnPath, cancellationToken);
    }

    /// <summary>
    /// Creates the authorize URL used for launcher sign-in. The callback URI differs from the admin one,
    /// so both must be registered with the identity provider.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authorize URL, or <see langword="null"/> and an English reason when client login is not enabled or the provider is unavailable.</returns>
    public Task<(string? Url, string? Error)> CreateClientAuthorizeUrlAsync(CancellationToken cancellationToken = default)
    {
        return CreateAuthorizeUrlAsync(systemConfig.Oidc.ClientRedirectUri ?? string.Empty, _clientStateKeyPrefix, null, cancellationToken);
    }

    /// <summary>
    /// Handles the identity provider callback: validates the state, exchanges the authorization code for tokens,
    /// validates the identity token, provisions or loads the admin account, and issues a one-time ticket.
    /// </summary>
    /// <param name="code">The authorization code returned by the identity provider.</param>
    /// <param name="state">The state echoed back by the identity provider.</param>
    /// <param name="error">The error name returned by the identity provider, for example when the user denies authorization.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The callback result.</returns>
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
    /// Handles the identity provider callback for launcher sign-in: validates the state, exchanges and validates the
    /// identity token, resolves which Hutao account the identity is bound to, and issues a one-time ticket.
    /// </summary>
    /// <param name="code">The authorization code returned by the identity provider.</param>
    /// <param name="state">The state echoed back by the identity provider.</param>
    /// <param name="error">The error name returned by the identity provider.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The callback result.</returns>
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
    /// Exchanges a launcher sign-in ticket for the Hutao account to sign in. A ticket can be used only once; the account
    /// and its binding are re-read from the database at exchange time, so accounts disabled or unbound between the
    /// callback and the exchange cannot obtain a token.
    /// </summary>
    /// <param name="ticket">The ticket issued during the callback phase.</param>
    /// <param name="clientIp">The client IP performing the exchange, used for auditing only.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A tuple containing the Hutao account and its admin account; both are <see langword="null"/> when the ticket is
    /// invalid or the identity is no longer usable.
    /// </returns>
    public async Task<(HutaoUser? User, AdminAccount? Account)> TakeClientTicketAsync(string ticket, string? clientIp = null, CancellationToken cancellationToken = default)
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
            Action = "/api/passport/sso/sessions",
            Detail = user.UserName,
            Ip = clientIp,
            Success = true,
            Timestamp = timeProvider.GetUnixTimeSeconds(),
        });
        await appDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (user, account);
    }

    /// <summary>
    /// Gets a value indicating whether the Hutao account is bound to an enabled admin account.
    /// Used by write-operation gating and maintainer checks; the result is cached for 15 minutes.
    /// </summary>
    /// <param name="userId">The Hutao account identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> if the account is bound and enabled; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> IsUserProvisionedAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await cache.GetOrCreateAsync(_boundKeyPrefix + userId, AccountTtl, async () =>
            await appDbContext.AdminAccounts
                .AsNoTracking()
                .AnyAsync(a => a.UserId == userId && a.IsEnabled, cancellationToken)
                .ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <summary>
    /// Exchanges a one-time ticket for an admin token. A ticket is consumed whether or not the exchange succeeds.
    /// </summary>
    /// <param name="ticket">The ticket issued during the callback phase.</param>
    /// <param name="clientIp">The client IP performing the exchange, used for auditing only.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The admin token response, or <see langword="null"/> when the ticket is invalid or the account is no longer usable.</returns>
    public async Task<AdminSsoTokenResponse?> RedeemTicketAsync(string ticket, string? clientIp = null, CancellationToken cancellationToken = default)
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
            Action = "/api/admin/sso/sessions",
            Detail = null,
            Ip = clientIp,
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
    /// Gets the state of an admin account for the permission filters and the audit log; the result is cached for 15 minutes.
    /// </summary>
    /// <param name="accountId">The admin account identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The account state, or <see langword="null"/> (also cached) when the account does not exist.</returns>
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
    /// Determines whether the admin account is allowed to continue performing operations.
    /// </summary>
    /// <param name="accountId">The admin account identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> if the account exists and is enabled; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> IsAccountUsableAsync(int accountId, CancellationToken cancellationToken = default)
    {
        return await GetAccountStateAsync(accountId, cancellationToken).ConfigureAwait(false) is { IsEnabled: true };
    }

    /// <summary>
    /// Gets the admin account list.
    /// </summary>
    /// <param name="currentAccountId">The current caller, used to flag the "self" entry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The admin account items.</returns>
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
    /// Adds an admin account. The identity provider only proves who the user is; access to the back office is decided by this table.
    /// </summary>
    /// <param name="subject">The subject identifier from the identity provider (the OIDC sub claim).</param>
    /// <param name="userName">The display name.</param>
    /// <param name="email">The optional contact email.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A tuple containing the return code, an English message, and the localization key.</returns>
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
    /// Enables or disables an admin account. Disabling the current account or the last enabled account is not allowed.
    /// </summary>
    /// <param name="accountId">The target account.</param>
    /// <param name="enabled">The target state.</param>
    /// <param name="currentAccountId">The current caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A tuple containing the return code, an English message, and the localization key.</returns>
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
    /// Removes an admin account. Removing the current account or every enabled account is not allowed.
    /// </summary>
    /// <param name="accountId">The target account.</param>
    /// <param name="currentAccountId">The current caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A tuple containing the return code, an English message, and the localization key.</returns>
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
    /// Invalidates the cached account state after enable, disable, remove, create, or rebind operations.
    /// The binding cache must be invalidated as well, otherwise write-operation gating would use stale bindings.
    /// </summary>
    /// <param name="account">The affected admin account.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task InvalidateAsync(AdminAccount account, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(_accountKeyPrefix + account.Id, cancellationToken).ConfigureAwait(false);
        if (account.UserId is int userId)
        {
            await cache.RemoveAsync(_boundKeyPrefix + userId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Binds an admin account to a Hutao account so that the admin account can sign in to the launcher via single sign-on.
    /// </summary>
    /// <param name="accountId">The target admin account.</param>
    /// <param name="userName">The email of the Hutao account.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A tuple containing the return code, an English message, and the localization key.</returns>
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

        string normalizedUserName = userName.Trim().ToUpperInvariant();
        HutaoUser? user = await appDbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName, cancellationToken)
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
    /// Removes the binding between an admin account and its Hutao account. Admin sign-in is unaffected;
    /// the account simply can no longer be used for launcher sign-in.
    /// </summary>
    /// <param name="accountId">The target admin account.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A tuple containing the return code, an English message, and the localization key.</returns>
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

    /// <summary>
    /// Maps a flow failure to the admin callback status.
    /// </summary>
    /// <param name="failure">The flow failure.</param>
    /// <returns>The corresponding admin callback status.</returns>
    private static AdminSsoCallbackStatus ToAdminStatus(FlowFailure failure)
    {
        return failure switch
        {
            FlowFailure.UpstreamRejected => AdminSsoCallbackStatus.UpstreamRejected,
            FlowFailure.TokenInvalid => AdminSsoCallbackStatus.TokenInvalid,
            _ => AdminSsoCallbackStatus.InvalidState,
        };
    }

    /// <summary>
    /// Maps a flow failure to the launcher callback status.
    /// </summary>
    /// <param name="failure">The flow failure.</param>
    /// <returns>The corresponding launcher callback status.</returns>
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
    /// Generates a cryptographically random Base64URL segment of the given byte length.
    /// </summary>
    /// <param name="byteLength">The number of random bytes.</param>
    /// <returns>The Base64URL-encoded segment.</returns>
    private static string RandomSegment(int byteLength)
    {
        byte[] bytes = new byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncoder.Encode(bytes);
    }

    /// <summary>
    /// Computes the S256 PKCE code challenge for the given code verifier.
    /// </summary>
    /// <param name="codeVerifier">The code verifier.</param>
    /// <returns>The Base64URL-encoded SHA-256 challenge.</returns>
    private static string CreateCodeChallenge(string codeVerifier)
    {
        return Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
    }

    /// <summary>
    /// Accepts only site-local absolute paths; protocol-relative addresses and overlong values are rejected
    /// to avoid open redirects.
    /// </summary>
    /// <param name="returnPath">The candidate return path.</param>
    /// <returns>The sanitized return path, or <see langword="null"/> when the value is not acceptable.</returns>
    private static string? SanitizeReturnPath(string? returnPath)
    {
        if (string.IsNullOrEmpty(returnPath)
            || returnPath.Length > 256
            || !returnPath.StartsWith('/')
            || returnPath.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        return returnPath;
    }

    /// <summary>
    /// Truncates the value to the given maximum length.
    /// </summary>
    /// <param name="value">The value to truncate.</param>
    /// <param name="maxLength">The maximum allowed length.</param>
    /// <returns>The original value or its truncated prefix.</returns>
    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    /// <summary>
    /// Decodes the payload of a compact JWT into a claim dictionary.
    /// </summary>
    /// <param name="compactJwt">The compact JWT string.</param>
    /// <returns>The claim dictionary, or an empty dictionary when the token is malformed.</returns>
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

    /// <summary>
    /// Gets a claim value, falling back to a case-insensitive match for providers that change the claim casing.
    /// </summary>
    /// <param name="claims">The claim dictionary.</param>
    /// <param name="name">The claim name.</param>
    /// <returns>The claim value, or <see langword="null"/> when the claim is absent.</returns>
    private static string? GetClaim(Dictionary<string, string?> claims, string name)
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

    /// <summary>
    /// Projects an admin account into the token principal.
    /// </summary>
    /// <param name="account">The admin account.</param>
    /// <returns>The token principal.</returns>
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
    /// Runs the business-agnostic "state → authorization code → identity token" protocol steps and returns the validated claims.
    /// </summary>
    /// <param name="code">The authorization code returned by the identity provider.</param>
    /// <param name="state">The state echoed back by the identity provider.</param>
    /// <param name="error">The error name returned by the identity provider.</param>
    /// <param name="stateKeyPrefix">The cache key prefix used for the state of this flow.</param>
    /// <param name="redirectUri">The redirect URI registered for this flow.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The validated claims, the pending return path, and the failure information when the flow did not complete.</returns>
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
    /// After the identity has been validated, resolves which Hutao account this subject may sign in as on the launcher side.
    /// </summary>
    /// <param name="claims">The validated identity claims.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The callback result carrying the one-time ticket or the rejection reason.</returns>
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
    /// Creates an authorize URL for the given redirect URI and state key prefix, storing the pending login state.
    /// </summary>
    /// <param name="redirectUri">The redirect URI registered for this flow.</param>
    /// <param name="stateKeyPrefix">The cache key prefix used for the state of this flow.</param>
    /// <param name="returnPath">The back-office relative path to return to after sign-in; may be <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authorize URL, or <see langword="null"/> and an English reason when the provider is unavailable.</returns>
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

    /// <summary>
    /// Serializes the value and stores it in the distributed cache with an absolute TTL.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="ttl">The absolute time-to-live.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        DistributedCacheEntryOptions options = new()
        {
            AbsoluteExpirationRelativeToNow = ttl,
        };
        await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value), options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads and immediately removes a value from the distributed cache (atomic get-and-delete).
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored value, or <see langword="default"/> when the key does not exist.</returns>
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

    /// <summary>
    /// Exchanges an authorization code for an identity token at the configured token endpoint.
    /// </summary>
    /// <param name="configuration">The OpenID Connect configuration.</param>
    /// <param name="oidc">The OIDC options.</param>
    /// <param name="code">The authorization code.</param>
    /// <param name="codeVerifier">The PKCE code verifier.</param>
    /// <param name="redirectUri">The redirect URI registered for this flow.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The identity token, or <see langword="null"/> and an English reason when the exchange fails.</returns>
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

    /// <summary>
    /// Validates the identity token signature, issuer, audience, and lifetime.
    /// </summary>
    /// <param name="idToken">The compact identity token.</param>
    /// <param name="configuration">The OpenID Connect configuration.</param>
    /// <param name="oidc">The OIDC options.</param>
    /// <returns><see langword="true"/> if the token is valid; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Provisions or loads the admin account for the validated claims and issues a one-time admin ticket.
    /// </summary>
    /// <param name="claims">The validated identity claims.</param>
    /// <param name="returnPath">The back-office relative path to return to after sign-in.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The callback result carrying the one-time ticket or the rejection reason.</returns>
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

    /// <summary>
    /// Creates the signed JWT access token for the admin account.
    /// </summary>
    /// <param name="account">The admin account.</param>
    /// <returns>The signed access token.</returns>
    private string CreateAccessToken(AdminAccount account)
    {
        JwtSecurityTokenHandler handler = new();
        ClaimsIdentity identity = new([new(AdminClaimTypes.AccountId, account.Id.ToString())]);
        SecurityTokenDescriptor descriptor = new()
        {
            Subject = identity,
            Expires = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(_accessTokenExpirationMinutes),
            Issuer = appOptions.Jwt.Issuer,
            SigningCredentials = new(_jwtSigningKey, SecurityAlgorithms.HmacSha256Signature),
        };

        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private sealed record PendingLogin(string Nonce, string CodeVerifier, string? ReturnPath);
}
