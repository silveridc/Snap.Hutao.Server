using System.Collections.Concurrent;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace server.Services.Admin;

public sealed class OidcMetadata(IHttpClientFactory httpClientFactory, SystemConfigService systemConfig)
{
    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _managers =
        new(StringComparer.Ordinal);

    /// <summary>
    /// 取发现文档。签发者与当前 Authority 不一致时直接拒绝，避免被伪造的元数据带偏。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>发现文档。</returns>
    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        OidcOptions oidc = systemConfig.Oidc;
        string expectedIssuer = oidc.Authority?.TrimEnd('/') ?? string.Empty;
        if (!oidc.Enabled || expectedIssuer.Length == 0)
        {
            throw new InvalidOperationException("App:Oidc is not enabled");
        }

        ConfigurationManager<OpenIdConnectConfiguration> configurationManager = _managers.GetOrAdd(
            $"{expectedIssuer}|{oidc.RequireHttpsMetadata}",
            _ =>
            {
                // 与出网代理共用 IHttpClientFactory，测试里可整体替换为桩处理器
                HttpClient httpClient = httpClientFactory.CreateClient(nameof(OidcMetadata));
                return new(
                    $"{expectedIssuer}/.well-known/openid-configuration",
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever(httpClient) { RequireHttps = oidc.RequireHttpsMetadata });
            });

        OpenIdConnectConfiguration configuration = await configurationManager.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(configuration.Issuer.TrimEnd('/'), expectedIssuer, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"OIDC metadata issuer {configuration.Issuer} does not match configured authority {expectedIssuer}");
        }

        return configuration;
    }
}
