using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using server.Controllers;
using server.Filter;
using server.Models.Admin;
using server.Models.Context;
using server.Models.Entity;
using server.Models.Entity.Passport;
using server.Models.Passport;
using server.Models.Response;
using server.Options;
using server.Services.Admin;
using server.Services.Authorization;
using server.Services.PublicAPI;
using server.Tests.Infrastructure;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Cryptography;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class SsoTests : IDisposable
{
    private const string Issuer = "https://idp.test";
    private const string ClientId = "hutao-admin";
    private const string KeyId = "sso-test-key";
    private const string AdminRedirectUri = "https://admin.test/Admin/Sso/Callback";
    private const string LauncherRedirectUri = "https://launcher.test/Passport/Sso/Callback";

    private readonly HutaoServerFactory factory;
    private readonly IServiceScope scope;
    private readonly RSA idpRsa;

    public SsoTests(HutaoServerFactory factory)
    {
        this.factory = factory;
        scope = factory.Services.CreateScope();
        idpRsa = RSA.Create(2048);
        RegisterIdentityProviderStubs();
    }

    public void Dispose()
    {
        idpRsa.Dispose();
        scope.Dispose();
    }

    /// <summary>
    /// 建立 AdminSsoService，种子声明值固定为 <paramref name="bootstrapValue"/>；<paramref name="enabled"/> 控制 SSO 总开关，
    /// <paramref name="clientRedirectUri"/> 非空时同时打开启动器侧的登录入口.
    /// </summary>
    private AdminSsoService CreateService(string bootstrapValue, bool enabled = true, string? clientRedirectUri = null)
    {
        AppOptions host = factory.GetService<AppOptions>();
        AppOptions options = new()
        {
            JWTIssuer = host.JWTIssuer,
            JwtRaw = host.JwtRaw,
            Oidc = new OidcOptions
            {
                Enabled = enabled,
                Authority = Issuer,
                ClientId = ClientId,
                ClientSecret = "sso-test-secret",
                RedirectUri = AdminRedirectUri,
                ClientRedirectUri = clientRedirectUri,
                DisplayName = "Test IdP",
                BootstrapClaim = "sub",
                BootstrapValue = bootstrapValue,
                RequireHttpsMetadata = false,
            },
        };

        IHttpClientFactory http = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        // SSO 配置现在从 SystemConfigService 读（后台可改，App:Oidc 兜底）——这里给测试专用实例，
        // 兜底源正是本用例的 options，因此不写任何 Oidc.* 键的既有用例行为不变
        SystemConfigService systemConfig = new(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<SystemConfigService>.Instance,
            TimeProvider.System);
        OidcMetadata metadata = new(http, systemConfig);

        return new AdminSsoService(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            options,
            systemConfig,
            metadata,
            scope.ServiceProvider.GetRequiredService<IDistributedCache>(),
            http,
            NullLogger<AdminSsoService>.Instance,
            TimeProvider.System);
    }

    private async Task PurgeAdminAccountsAsync(CancellationToken cancellationToken)
    {
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.AdminAccounts.ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>登记发现文档与 JWKS；令牌端点在具体用例里按需追加.</summary>
    private void RegisterIdentityProviderStubs()
    {
        RSAParameters parameters = idpRsa.ExportParameters(false);
        string jwks = $$"""
            {"keys":[{"kty":"RSA","use":"sig","alg":"RS256","kid":"{{KeyId}}","n":"{{Base64UrlEncoder.Encode(parameters.Modulus!)}}","e":"{{Base64UrlEncoder.Encode(parameters.Exponent!)}}"}]}
            """;
        string discovery = $$"""
            {"issuer":"{{Issuer}}","authorization_endpoint":"{{Issuer}}/authorize","token_endpoint":"{{Issuer}}/token","jwks_uri":"{{Issuer}}/jwks","response_types_supported":["code"],"subject_types_supported":["public"],"id_token_signing_alg_values_supported":["RS256"],"jwks":{{jwks}}}
            """;

        factory.StubHttp
            .MapJson($"{Issuer}/.well-known/openid-configuration", discovery)
            .MapJson($"{Issuer}/jwks", jwks);
    }

    /// <summary>用身份源的私钥签发一个 ID Token.</summary>
    private string MintIdToken(string subject, string nonce)
    {
        RsaSecurityKey key = new(idpRsa) { KeyId = KeyId };
        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = Issuer,
            Audience = ClientId,
            Subject = new System.Security.Claims.ClaimsIdentity([
                new("sub", subject),
                new("nonce", nonce),
                new("preferred_username", subject),
                new("email", $"{subject}@example.com"),
            ]),
            Expires = DateTime.UtcNow.AddMinutes(5),
            // 密钥的 KeyId 会被自动写进 JWT 头的 kid，这里不能重复声明
            SigningCredentials = new(key, SecurityAlgorithms.RsaSha256),
        };

        return new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static string GetQuery(string url, string name)
    {
        string query = url.Split('?', count: 2)[1];
        foreach (string pair in query.Split('&'))
        {
            string[] kv = pair.Split('=', 2);
            if (kv.Length == 2 && kv[0] == name)
            {
                return Uri.UnescapeDataString(kv[1]);
            }
        }

        throw new InvalidOperationException($"Query parameter '{name}' not found in {url}");
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>造一个带指定身份声明、指定动词的动作执行上下文，用来直接驱动权限过滤器.</summary>
    private static ActionExecutingContext BuildContext(string method, params System.Security.Claims.Claim[] claims)
    {
        DefaultHttpContext httpContext = new()
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims)),
        };
        httpContext.Request.Method = method;

        ControllerActionDescriptor descriptor = new()
        {
            ControllerName = "Service",
            ActionName = "Write",
            ControllerTypeInfo = typeof(SsoTests).GetTypeInfo(),
            MethodInfo = typeof(object).GetMethod(nameof(object.ToString), Type.EmptyTypes)!,
        };

        return new ActionExecutingContext(
            new ActionContext { HttpContext = httpContext, RouteData = new RouteData(), ActionDescriptor = descriptor },
            [],
            new Dictionary<string, object?>(),
            controller: null!);
    }

    /// <summary>跑一次过滤器，返回「内层动作是否执行」与过滤器写入的响应.</summary>
    private static async Task<(bool InnerRan, IActionResult? Result)> RunFilterAsync(IAsyncActionFilter filter, ActionExecutingContext context)
    {
        bool innerRan = false;

        // 过滤器只在放行时调用 next，这里无需真实的 ActionExecutedContext
        await filter.OnActionExecutionAsync(context, () =>
        {
            innerRan = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        return (innerRan, context.Result);
    }

    private static void AssertRetcode(IActionResult? result, ReturnCode expected)
    {
        JsonResult json = Assert.IsType<JsonResult>(result);
        Assert.Equal(expected, Assert.IsType<Response<object?>>(json.Value).Code);
    }

    /// <summary>走完一次「授权 → 回调 → 兑换票据」，返回后台令牌里的账号 ID.</summary>
    private async Task<int> SignInAsync(AdminSsoService service, string subject, CancellationToken token)
    {
        AdminSsoCallbackResult callback = await CompleteLoginAsync(service, subject, token);
        AdminSsoTokenResponse? redeemed = await service.RedeemTicketAsync(callback.Ticket!, cancellationToken: token);
        Assert.NotNull(redeemed);

        JwtSecurityToken decoded = new JwtSecurityTokenHandler().ReadJwtToken(redeemed!.AccessToken);
        Assert.False(decoded.Claims.Any(c => c.Type == PassportClaimTypes.UserId), "后台令牌不应携带主站用户身份");

        return int.Parse(decoded.Claims.Single(c => c.Type == AdminClaimTypes.AccountId).Value);
    }

    [Fact]
    public async Task BootstrapSubject_CompletesTheFullLoginAndGetsAnAdminToken()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);
        string subject = $"bootstrap-{Guid.NewGuid():N}";
        AdminSsoService service = CreateService(subject);

        (string? authorizeUrl, string? error) = await service.CreateAuthorizeUrlAsync("/dashboard", token);
        Assert.Null(error);
        Assert.NotNull(authorizeUrl);
        Assert.StartsWith($"{Issuer}/authorize?", authorizeUrl, StringComparison.Ordinal);
        Assert.Contains("code_challenge_method=S256", authorizeUrl, StringComparison.Ordinal);
        Assert.Contains($"client_id={ClientId}", authorizeUrl, StringComparison.Ordinal);

        string state = GetQuery(authorizeUrl!, "state");
        string nonce = GetQuery(authorizeUrl!, "nonce");

        string idToken = MintIdToken(subject, nonce);
        factory.StubHttp.MapJson($"{Issuer}/token", $$"""{"id_token":"{{idToken}}","token_type":"Bearer","access_token":"irrelevant"}""");

        AdminSsoCallbackResult callback = await service.HandleCallbackAsync("the-code", state, null, token);
        Assert.Equal(AdminSsoCallbackStatus.Success, callback.Status);
        Assert.NotNull(callback.Ticket);

        AdminSsoTokenResponse? redeemed = await service.RedeemTicketAsync(callback.Ticket!, cancellationToken: token);
        Assert.NotNull(redeemed);
        JwtSecurityToken decoded = new JwtSecurityTokenHandler().ReadJwtToken(redeemed!.AccessToken);
        string? accountId = decoded.Claims.SingleOrDefault(c => c.Type == AdminClaimTypes.AccountId)?.Value;
        Assert.False(string.IsNullOrEmpty(accountId));
        Assert.False(decoded.Claims.Any(c => c.Type == PassportClaimTypes.UserId), "后台令牌不应携带主站用户身份");

        // 一次性票据用第二次必须失败
        Assert.Null(await service.RedeemTicketAsync(callback.Ticket!, cancellationToken: token));

        // 账号确实落库并可被后续请求命中
        int parsedId = int.Parse(accountId!);
        AdminAccountState? accountState = await service.GetAccountStateAsync(parsedId, token);
        Assert.NotNull(accountState);
        Assert.True(accountState!.IsEnabled);
        Assert.True(await service.IsAccountUsableAsync(parsedId, token));
    }

    [Fact]
    public async Task CallbackWithUnknownState_IsRejected()
    {
        AdminSsoService service = CreateService($"nobody-{Guid.NewGuid():N}");

        AdminSsoCallbackResult callback = await service.HandleCallbackAsync("code", $"forged-{Guid.NewGuid():N}", null, Token);
        Assert.Equal(AdminSsoCallbackStatus.InvalidState, callback.Status);
    }

    [Fact]
    public async Task ReplayedState_CannotBeUsedTwice()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);
        string subject = $"replay-{Guid.NewGuid():N}";
        AdminSsoService service = CreateService(subject);

        (string? authorizeUrl, _) = await service.CreateAuthorizeUrlAsync(null, token);
        string state = GetQuery(authorizeUrl!, "state");
        string idToken = MintIdToken(subject, GetQuery(authorizeUrl!, "nonce"));
        factory.StubHttp.MapJson($"{Issuer}/token", $$"""{"id_token":"{{idToken}}","token_type":"Bearer","access_token":"irrelevant"}""");

        Assert.Equal(AdminSsoCallbackStatus.Success, (await service.HandleCallbackAsync("code", state, null, token)).Status);

        // state 是一次性的：同一个 state 再次回调必须落到 InvalidState 分支
        AdminSsoCallbackResult replay = await service.HandleCallbackAsync("code", state, null, token);
        Assert.Equal(AdminSsoCallbackStatus.InvalidState, replay.Status);
    }

    [Fact]
    public async Task IdentityTokenWithWrongNonce_IsRejected()
    {
        CancellationToken token = Token;
        AdminSsoService service = CreateService($"anyone-{Guid.NewGuid():N}");

        (string? authorizeUrl, _) = await service.CreateAuthorizeUrlAsync(null, token);
        string state = GetQuery(authorizeUrl!, "state");

        // nonce 与登录时暂存的不一致
        string idToken = MintIdToken($"anyone-{Guid.NewGuid():N}", "wrong-nonce");
        factory.StubHttp.MapJson($"{Issuer}/token", $$"""{"id_token":"{{idToken}}","token_type":"Bearer","access_token":"irrelevant"}""");

        AdminSsoCallbackResult callback = await service.HandleCallbackAsync("code", state, null, token);
        Assert.Equal(AdminSsoCallbackStatus.TokenInvalid, callback.Status);
    }

    [Fact]
    public async Task UnprovisionedSubject_IsRejectedButEchoesItsSubject()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);

        // 先让种子账号占位，之后另一个 sub 登录就不会再触发种子建立
        string bootstrap = $"owner-{Guid.NewGuid():N}";
        AdminSsoService service = CreateService(bootstrap);
        await CompleteLoginAsync(service, bootstrap, token);

        string stranger = $"stranger-{Guid.NewGuid():N}";
        AdminSsoCallbackResult callback = await CompleteLoginAsync(service, stranger, token, expect: AdminSsoCallbackStatus.NotProvisioned);
        Assert.Equal(stranger, callback.Subject);
    }

    [Fact]
    public async Task ProvisionedAfterAdd_CanThenLogIn()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);

        string bootstrap = $"owner-{Guid.NewGuid():N}";
        AdminSsoService service = CreateService(bootstrap);
        await CompleteLoginAsync(service, bootstrap, token);

        string colleague = $"colleague-{Guid.NewGuid():N}";

        // 还没被加进账号表之前，身份有效也进不来
        await CompleteLoginAsync(service, colleague, token, expect: AdminSsoCallbackStatus.NotProvisioned);

        (ReturnCode code, _, _) = await service.AddAsync(colleague, "Colleague", null, token);
        Assert.Equal(ReturnCode.Success, code);

        await CompleteLoginAsync(service, colleague, token);
    }

    [Fact]
    public async Task DisabledAccount_CannotBeUsed()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);

        string bootstrap = $"owner-{Guid.NewGuid():N}";
        AdminSsoService service = CreateService(bootstrap);
        await CompleteLoginAsync(service, bootstrap, token);

        int targetId = (await service.ListAsync(null, token))[0].Id;

        // 加第二个账号，避免停用时撞上「至少保留一个」
        string other = $"other-{Guid.NewGuid():N}";
        Assert.Equal(ReturnCode.Success, (await service.AddAsync(other, "Other", null, token)).Code);

        Assert.Equal(ReturnCode.Success, (await service.SetEnabledAsync(targetId, enabled: false, currentAccountId: 0, token)).Code);
        Assert.False(await service.IsAccountUsableAsync(targetId, token));
    }

    [Fact]
    public async Task CannotDisableOrRemoveYourself()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);

        string bootstrap = $"owner-{Guid.NewGuid():N}";
        AdminSsoService service = CreateService(bootstrap);
        await CompleteLoginAsync(service, bootstrap, token);

        int self = (await service.ListAsync(null, token))[0].Id;

        Assert.Equal(ReturnCode.InvalidUploadData, (await service.SetEnabledAsync(self, enabled: false, currentAccountId: self, token)).Code);
        Assert.Equal(ReturnCode.InvalidUploadData, (await service.RemoveAsync(self, currentAccountId: self, token)).Code);
    }

    /// <summary>
    /// 老账号只读、后台账号可写：这是「收紧老账号」的核心断言.
    /// 未启用单点登录时门禁整体放行，配置没配好之前不会把自己关在门外.
    /// </summary>
    [Fact]
    public async Task WriteGate_KeepsLegacyAccountsReadOnly()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);

        string subject = $"gate-{Guid.NewGuid():N}";
        AdminSsoService service = CreateService(subject);
        int accountId = await SignInAsync(service, subject, token);
        ValidateAdminWritePermission gate = new(service);

        (bool maintainerRan, IActionResult? maintainerResult) = await RunFilterAsync(gate, BuildContext("POST", new System.Security.Claims.Claim(PassportClaimTypes.UserId, "1")));
        Assert.False(maintainerRan);
        AssertRetcode(maintainerResult, ReturnCode.WriteRequiresSsoAdmin);

        (bool adminRan, IActionResult? adminResult) = await RunFilterAsync(gate, BuildContext("POST", new System.Security.Claims.Claim(AdminClaimTypes.AccountId, $"{accountId}")));
        Assert.True(adminRan);
        Assert.Null(adminResult);

        (bool readRan, IActionResult? readResult) = await RunFilterAsync(gate, BuildContext("GET", new System.Security.Claims.Claim(PassportClaimTypes.UserId, "1")));
        Assert.True(readRan);
        Assert.Null(readResult);

        ValidateAdminWritePermission off = new(CreateService(subject, enabled: false));
        (bool inertRan, IActionResult? inertResult) = await RunFilterAsync(off, BuildContext("POST"));
        Assert.True(inertRan);
        Assert.Null(inertResult);
    }

    /// <summary>后台专属账号在只读门禁里等同于维护者，停用后连读也进不来.</summary>
    [Fact]
    public async Task MaintainGate_AdmitsUsableAdminAccounts()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);

        string subject = $"reader-{Guid.NewGuid():N}";
        AdminSsoService service = CreateService(subject);
        int accountId = await SignInAsync(service, subject, token);

        ValidateMaintainPermission gate = new(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            scope.ServiceProvider.GetRequiredService<IDistributedCache>(),
            service);

        (bool ran, IActionResult? result) = await RunFilterAsync(gate, BuildContext("GET", new System.Security.Claims.Claim(AdminClaimTypes.AccountId, $"{accountId}")));
        Assert.True(ran);
        Assert.Null(result);

        // 停用时先补一个可用账号，避免撞上「至少保留一个」
        Assert.Equal(ReturnCode.Success, (await service.AddAsync($"spare-{Guid.NewGuid():N}", "Spare", null, token)).Code);
        Assert.Equal(ReturnCode.Success, (await service.SetEnabledAsync(accountId, enabled: false, currentAccountId: 0, token)).Code);

        (bool disabledRan, IActionResult? disabledResult) = await RunFilterAsync(gate, BuildContext("GET", new System.Security.Claims.Claim(AdminClaimTypes.AccountId, $"{accountId}")));
        Assert.False(disabledRan);
        AssertRetcode(disabledResult, ReturnCode.ServiceKeyInvalid);
    }

    /// <summary>
    /// 启动器侧的开关：总开关开着但客户端回调地址没配时，客户端能力仍然关闭，
    /// 而且回调不会去访问身份源——客户端据此继续只显示邮箱密码登录.
    /// </summary>
    [Fact]
    public async Task ClientLogin_IsAdvertisedOnlyWithAClientRedirectUri()
    {
        CancellationToken token = Token;
        AdminSsoService withoutUri = CreateService($"uri-{Guid.NewGuid():N}");
        Assert.False(withoutUri.IsClientLoginEnabled);
        Assert.Equal(PassportSsoCallbackStatus.InvalidState, (await withoutUri.HandleClientCallbackAsync("code", "any-state", null, token)).Status);

        Assert.True(CreateService($"uri-{Guid.NewGuid():N}", clientRedirectUri: LauncherRedirectUri).IsClientLoginEnabled);
        Assert.False(CreateService($"uri-{Guid.NewGuid():N}", enabled: false, clientRedirectUri: LauncherRedirectUri).IsClientLoginEnabled);
    }

    /// <summary>
    /// 客户端链路的完整跑通：授权地址用的是客户端回调地址 → 回调换到一次性票据 →
    /// 票据换来的是<b>主站令牌</b>（含 UserId 与刷新令牌，与邮箱密码登录同构），票据用第二次即失效.
    /// </summary>
    [Fact]
    public async Task ClientLogin_ExchangesTheTicketForAnOrdinaryUserToken()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);
        string subject = $"client-{Guid.NewGuid():N}";
        AdminSsoService service = CreateService(subject, clientRedirectUri: LauncherRedirectUri);
        (int accountId, TestUser bound) = await ProvisionBoundAccountAsync(service, subject, token);

        (string? clientAuthorizeUrl, _) = await service.CreateClientAuthorizeUrlAsync(token);
        Assert.NotNull(clientAuthorizeUrl);
        Assert.Equal(LauncherRedirectUri, GetQuery(clientAuthorizeUrl!, "redirect_uri"));

        (string? adminAuthorizeUrl, _) = await service.CreateAuthorizeUrlAsync(null, token);
        Assert.NotNull(adminAuthorizeUrl);
        Assert.Equal(AdminRedirectUri, GetQuery(adminAuthorizeUrl!, "redirect_uri"));

        PassportSsoCallbackResult callback = await CompleteClientLoginAsync(service, subject, token);
        PassportSsoController controller = CreateController(service);

        JsonResult exchanged = Assert.IsType<JsonResult>(await controller.ExchangeAsync(new PassportSsoExchangeRequest { Ticket = callback.Ticket }, token));
        Response<PassportSsoLoginResult> body = Assert.IsType<Response<PassportSsoLoginResult>>(exchanged.Value);
        Assert.Equal(ReturnCode.Success, body.Code);
        Assert.Equal(bound.UserName, body.Data!.UserName);
        Assert.False(string.IsNullOrEmpty(body.Data.RefreshToken));

        JwtSecurityToken decoded = new JwtSecurityTokenHandler().ReadJwtToken(body.Data.AccessToken);
        Assert.Equal($"{bound.Id}", decoded.Claims.Single(c => c.Type == PassportClaimTypes.UserId).Value);
        Assert.False(decoded.Claims.Any(c => c.Type == AdminClaimTypes.AccountId), "启动器换来的应是主站令牌，不携带后台账号身份");

        // 一次性票据用第二次必须失败
        AssertRetcode(await controller.ExchangeAsync(new PassportSsoExchangeRequest { Ticket = callback.Ticket }, token), ReturnCode.SsoTicketInvalid);

        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Contains(
            await db.AdminAuditLogs.AsNoTracking().Where(log => log.AdminAccountId == accountId).ToListAsync(token),
            log => log.Action == "/api/passport/sso/sessions" && log.Detail == bound.UserName);
    }

    /// <summary>回跳只落到启动器拦截的哨兵地址：成功带 ticket，失败带 error 与回显的主体标识.</summary>
    [Fact]
    public async Task ClientCallback_RedirectsToTheLauncherSentinel()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);
        string subject = $"sentinel-{Guid.NewGuid():N}";
        AdminSsoService service = CreateService(subject, clientRedirectUri: LauncherRedirectUri);
        (int accountId, _) = await ProvisionBoundAccountAsync(service, subject, token);
        PassportSsoController controller = CreateController(service);

        (string? authorizeUrl, _) = await service.CreateClientAuthorizeUrlAsync(token);
        StubIdTokenFor(authorizeUrl!, subject);
        StatusCodeResult redirect = Assert.IsType<StatusCodeResult>(await controller.CallbackAsync("code", GetQuery(authorizeUrl!, "state"), null, token));
        Assert.Equal(StatusCodes.Status302Found, redirect.StatusCode);
        Assert.StartsWith($"{PassportSsoController.LauncherCallbackUrl}?ticket=", controller.Response.Headers.Location.ToString(), StringComparison.Ordinal);

        // 解绑后同一个身份还能登录后台，但客户端回跳只带失败原因
        Assert.Equal(ReturnCode.Success, (await service.UnbindAsync(accountId, token)).Code);
        (string? reboundUrl, _) = await service.CreateClientAuthorizeUrlAsync(token);
        StubIdTokenFor(reboundUrl!, subject);
        await controller.CallbackAsync("code", GetQuery(reboundUrl!, "state"), null, token);

        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(controller.Response.Headers.Location.ToString()[$"{PassportSsoController.LauncherCallbackUrl}?".Length..]);
        Assert.Equal("not_bound", query["error"].ToString());
        Assert.Equal(subject, query["subject"].ToString());
    }

    /// <summary>
    /// 换码请求（token endpoint）表单里的 redirect_uri 必须与授权时用的同一个回调地址.
    /// 回归：曾经硬编码后台回调地址，客户端链路被身份源按 <c>invalid_grant redirect_uri mismatch</c>
    /// 拒收，启动器 SSO 登录全灭——桩身份源原本不校验这个字段，只有真 IdP 能抓到.
    /// </summary>
    [Fact]
    public async Task TokenExchange_SendsTheRedirectUriUsedAtAuthorizeTime()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);
        string adminSubject = $"grant-admin-{Guid.NewGuid():N}";
        string clientSubject = $"grant-client-{Guid.NewGuid():N}";
        AdminSsoService service = CreateService(adminSubject, clientRedirectUri: LauncherRedirectUri);

        // 后台链路：换码表单带后台回调地址
        int beforeAdmin = factory.StubHttp.Requests.Count;
        await CompleteLoginAsync(service, adminSubject, token);
        string adminForm = TokenEndpointFormSince(beforeAdmin);
        Assert.Contains($"redirect_uri={Uri.EscapeDataString(AdminRedirectUri)}", adminForm, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.EscapeDataString(LauncherRedirectUri), adminForm, StringComparison.Ordinal);

        // 客户端链路：同一个服务实例必须改带客户端回调地址
        await ProvisionBoundAccountAsync(service, clientSubject, token);
        int beforeClient = factory.StubHttp.Requests.Count;
        await CompleteClientLoginAsync(service, clientSubject, token);
        string clientForm = TokenEndpointFormSince(beforeClient);
        Assert.Contains($"redirect_uri={Uri.EscapeDataString(LauncherRedirectUri)}", clientForm, StringComparison.Ordinal);
        Assert.DoesNotContain(Uri.EscapeDataString(AdminRedirectUri), clientForm, StringComparison.Ordinal);
    }

    private string TokenEndpointFormSince(int from)
    {
        return factory.StubHttp.Requests.Skip(from)
            .Single(r => r.Url.Contains("/token", StringComparison.Ordinal)).Body!;
    }

    /// <summary>身份有效但还没绑定主站账号时，客户端登录停在「未绑定」，签不出票据.</summary>
    [Fact]
    public async Task ClientLogin_WithoutABoundHutaoAccount_IsRejected()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);
        string subject = $"unbound-{Guid.NewGuid():N}";
        AdminSsoService service = CreateService(subject, clientRedirectUri: LauncherRedirectUri);
        Assert.Equal(ReturnCode.Success, (await service.AddAsync(subject, "Unbound", null, token)).Code);

        PassportSsoCallbackResult callback = await CompleteClientLoginAsync(service, subject, token, PassportSsoCallbackStatus.NotBound);
        Assert.Null(callback.Ticket);
        Assert.Equal(subject, callback.Subject);
    }

    /// <summary>这个身份连后台账号都没有，客户端登录停在「未开通」.</summary>
    [Fact]
    public async Task ClientLogin_UnprovisionedSubject_IsRejectedButEchoesItsSubject()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);
        AdminSsoService service = CreateService($"owner-{Guid.NewGuid():N}", clientRedirectUri: LauncherRedirectUri);
        string stranger = $"stranger-{Guid.NewGuid():N}";

        PassportSsoCallbackResult callback = await CompleteClientLoginAsync(service, stranger, token, PassportSsoCallbackStatus.NotProvisioned);
        Assert.Equal(stranger, callback.Subject);
    }

    /// <summary>后台账号被停用后，客户端登录停在「已停用」.</summary>
    [Fact]
    public async Task ClientLogin_DisabledAccount_IsRejected()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);
        string subject = $"disabled-{Guid.NewGuid():N}";
        AdminSsoService service = CreateService(subject, clientRedirectUri: LauncherRedirectUri);
        (int accountId, _) = await ProvisionBoundAccountAsync(service, subject, token);

        // 先补一个可用账号，避免撞上「至少保留一个」
        Assert.Equal(ReturnCode.Success, (await service.AddAsync($"spare-{Guid.NewGuid():N}", "Spare", null, token)).Code);
        Assert.Equal(ReturnCode.Success, (await service.SetEnabledAsync(accountId, enabled: false, currentAccountId: 0, token)).Code);

        await CompleteClientLoginAsync(service, subject, token, PassportSsoCallbackStatus.Disabled);
    }

    /// <summary>
    /// 票据与「还能不能登录」是分开判定的：回调之后被解绑或停用，先前签出的票据换不出令牌，
    /// 免得拿着旧票据绕过已经收回的权限.
    /// </summary>
    [Fact]
    public async Task TicketTakenAfterBindingRevoked_DoesNotIssueAToken()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);
        string subject = $"revoked-{Guid.NewGuid():N}";
        AdminSsoService service = CreateService(subject, clientRedirectUri: LauncherRedirectUri);
        (int accountId, TestUser bound) = await ProvisionBoundAccountAsync(service, subject, token);

        string unboundTicket = (await CompleteClientLoginAsync(service, subject, token)).Ticket!;
        Assert.Equal(ReturnCode.Success, (await service.UnbindAsync(accountId, token)).Code);
        (HutaoUser? unboundUser, AdminAccount? unboundAccount) = await service.TakeClientTicketAsync(unboundTicket, cancellationToken: token);
        Assert.Null(unboundUser);
        Assert.Null(unboundAccount);

        Assert.Equal(ReturnCode.Success, (await service.BindAsync(accountId, bound.UserName, token)).Code);
        string disabledTicket = (await CompleteClientLoginAsync(service, subject, token)).Ticket!;
        Assert.Equal(ReturnCode.Success, (await service.AddAsync($"spare-{Guid.NewGuid():N}", "Spare", null, token)).Code);
        Assert.Equal(ReturnCode.Success, (await service.SetEnabledAsync(accountId, enabled: false, currentAccountId: 0, token)).Code);
        Assert.Null((await service.TakeClientTicketAsync(disabledTicket, cancellationToken: token)).User);
    }

    /// <summary>
    /// 「有权限的可以直接 SSO 登录」在写门禁里的落点：启动器只携带主站令牌，
    /// 门禁按「这个用户是否绑定了可用的后台账号」放行；解绑后同一个令牌立刻回到只读.
    /// </summary>
    [Fact]
    public async Task WriteGate_AdmitsUsersBoundToAnAdminAccount()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);
        string subject = $"bound-gate-{Guid.NewGuid():N}";
        AdminSsoService service = CreateService(subject, clientRedirectUri: LauncherRedirectUri);
        (int accountId, TestUser bound) = await ProvisionBoundAccountAsync(service, subject, token);
        ValidateAdminWritePermission gate = new(service);
        System.Security.Claims.Claim[] userClaim = [new(PassportClaimTypes.UserId, $"{bound.Id}")];

        Assert.True(await service.IsUserProvisionedAsync(bound.Id, token));
        (bool boundRan, IActionResult? boundResult) = await RunFilterAsync(gate, BuildContext("POST", userClaim));
        Assert.True(boundRan);
        Assert.Null(boundResult);

        Assert.Equal(ReturnCode.Success, (await service.UnbindAsync(accountId, token)).Code);
        Assert.False(await service.IsUserProvisionedAsync(bound.Id, token));
        (bool unboundRan, IActionResult? unboundResult) = await RunFilterAsync(gate, BuildContext("POST", userClaim));
        Assert.False(unboundRan);
        AssertRetcode(unboundResult, ReturnCode.WriteRequiresSsoAdmin);
    }

    /// <summary>绑定了后台账号的主站用户在只读门禁里也等同于维护者，否则启动器登录后读不到维护接口.</summary>
    [Fact]
    public async Task MaintainGate_AdmitsUsersBoundToAnAdminAccount()
    {
        CancellationToken token = Token;
        await PurgeAdminAccountsAsync(token);
        string subject = $"bound-reader-{Guid.NewGuid():N}";
        AdminSsoService service = CreateService(subject, clientRedirectUri: LauncherRedirectUri);
        (int accountId, TestUser bound) = await ProvisionBoundAccountAsync(service, subject, token);
        ValidateMaintainPermission gate = new(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            scope.ServiceProvider.GetRequiredService<IDistributedCache>(),
            service);
        System.Security.Claims.Claim[] userClaim = [new(PassportClaimTypes.UserId, $"{bound.Id}")];

        (bool ran, IActionResult? result) = await RunFilterAsync(gate, BuildContext("GET", userClaim));
        Assert.True(ran);
        Assert.Null(result);

        Assert.Equal(ReturnCode.Success, (await service.UnbindAsync(accountId, token)).Code);
        (bool revokedRan, IActionResult? revokedResult) = await RunFilterAsync(gate, BuildContext("GET", userClaim));
        Assert.False(revokedRan);
        AssertRetcode(revokedResult, ReturnCode.ServiceKeyInvalid);
    }

    /// <summary>建一个后台账号并绑定到全新的主站账号，返回两边的 ID 与登录用的邮箱.</summary>
    private async Task<(int AccountId, TestUser User)> ProvisionBoundAccountAsync(AdminSsoService service, string subject, CancellationToken token)
    {
        TestUser user = await factory.Users.CreateUniqueAsync("sso-bind");
        Assert.Equal(ReturnCode.Success, (await service.AddAsync(subject, "Bound Admin", null, token)).Code);

        int accountId = (await service.ListAsync(null, token)).Single(a => a.Subject == subject).Id;
        Assert.Equal(ReturnCode.Success, (await service.BindAsync(accountId, user.UserName, token)).Code);
        AdminAccountItem bound = (await service.ListAsync(null, token)).Single(a => a.Id == accountId);
        Assert.Equal<int?>(user.Id, bound.BoundUserId);
        Assert.Equal(user.UserName, bound.BoundUserName);

        return (accountId, user);
    }

    /// <summary>让桩身份源接受这次授权：按 state 里的 nonce 现签一个 ID Token.</summary>
    private void StubIdTokenFor(string authorizeUrl, string subject)
    {
        string idToken = MintIdToken(subject, GetQuery(authorizeUrl, "nonce"));
        factory.StubHttp.MapJson($"{Issuer}/token", $$"""{"id_token":"{{idToken}}","token_type":"Bearer","access_token":"irrelevant"}""");
    }

    /// <summary>构造一个带设备头的 PassportSsoController，直接驱动客户端的两个对外接口.</summary>
    private PassportSsoController CreateController(AdminSsoService service)
    {
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers["x-hutao-device-id"] = $"sso-device-{Guid.NewGuid():N}";
        httpContext.Request.Headers["x-hutao-device-name"] = "Test Device";
        httpContext.Request.Headers["x-hutao-device-os"] = "Windows";

        return new PassportSsoController(
            service,
            scope.ServiceProvider.GetRequiredService<PassportService>(),
            scope.ServiceProvider.GetRequiredService<UserLoginLogService>(),
            NullLogger<PassportSsoController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    /// <summary>走完一次客户端侧的「授权 → 回调」，返回回调结果；<paramref name="expect"/> 用于断言失败分支.</summary>
    private async Task<PassportSsoCallbackResult> CompleteClientLoginAsync(AdminSsoService service, string subject, CancellationToken token, PassportSsoCallbackStatus expect = PassportSsoCallbackStatus.Success)
    {
        (string? authorizeUrl, _) = await service.CreateClientAuthorizeUrlAsync(token);
        Assert.NotNull(authorizeUrl);
        StubIdTokenFor(authorizeUrl!, subject);

        PassportSsoCallbackResult result = await service.HandleClientCallbackAsync("code", GetQuery(authorizeUrl!, "state"), null, token);
        Assert.Equal(expect, result.Status);
        return result;
    }

    /// <summary>后台是真实路径路由，回跳参数直接追加在 /admin/sso 之后.</summary>
    [Fact]
    public void CallbackUrl_PutsTheQueryOnTheRealPath()
    {
        Dictionary<string, string?> query = new() { ["ticket"] = "T/s+K=", ["return"] = "/metadata/index" };
        string url = AdminSsoController.CallbackUrl(query);

        Assert.StartsWith("/admin/sso?", url);
        Assert.DoesNotContain("#", url);
        Dictionary<string, StringValues> parsed = QueryHelpers.ParseQuery(url[url.IndexOf('?')..]);
        Assert.Equal("T/s+K=", parsed["ticket"].ToString());
        Assert.Equal("/metadata/index", parsed["return"].ToString());
    }

    /// <summary>走完一次「授权 → 回调」，返回回调结果；<paramref name="expect"/> 用于断言失败分支.</summary>
    private async Task<AdminSsoCallbackResult> CompleteLoginAsync(AdminSsoService service, string subject, CancellationToken token, AdminSsoCallbackStatus expect = AdminSsoCallbackStatus.Success)
    {
        (string? authorizeUrl, _) = await service.CreateAuthorizeUrlAsync(null, token);
        string state = GetQuery(authorizeUrl!, "state");
        string idToken = MintIdToken(subject, GetQuery(authorizeUrl!, "nonce"));
        factory.StubHttp.MapJson($"{Issuer}/token", $$"""{"id_token":"{{idToken}}","token_type":"Bearer","access_token":"irrelevant"}""");

        AdminSsoCallbackResult result = await service.HandleCallbackAsync("code", state, null, token);
        Assert.Equal(expect, result.Status);
        return result;
    }
}