using System.Collections.Concurrent;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace server.Services.Admin;

/// <summary>
/// OIDC 发现文档与签名密钥的进程内缓存。
/// <see cref="ConfigurationManager{T}"/> 自带定时刷新，并在遇到未知 kid 时立即重取，
/// 因此按 Authority+RequireHttps 键控缓存复用。SSO 配置来自后台（SystemConfigService，
/// App:Oidc 兜底），Authority 改动后下一次取元数据自动按新地址重建，无需重启。
/// </summary>
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
