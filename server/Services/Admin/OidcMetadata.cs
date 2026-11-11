using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Collections.Concurrent;

namespace server.Services.Admin;

public sealed class OidcMetadata(IHttpClientFactory httpClientFactory, SystemConfigService systemConfig)
{
    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _managers =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the OpenID Connect discovery document for the configured authority.
    /// Metadata whose issuer does not match the configured authority is rejected, preventing forged metadata from being trusted.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The validated OpenID Connect configuration.</returns>
    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        OidcOptions oidc = systemConfig.Oidc;
        string expectedIssuer = oidc.Authority?.TrimEnd('/') ?? string.Empty;
        if (!oidc.Enabled || expectedIssuer.Length == 0)
        {
            throw new InvalidOperationException("OIDC is not enabled or the authority is not configured");
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
