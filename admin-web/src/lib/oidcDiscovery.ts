import { t } from '@/i18n';

// 与 server/Options/OidcOptions.cs 的 Scope 默认值一致，只回填身份源真正支持的项
const STANDARD_SCOPES = ['openid', 'profile', 'email'];
const TIMEOUT_MS = 8000;

interface DiscoveryDocument {
  issuer?: unknown;
  scopes_supported?: unknown;
}

export type DiscoveryPatch = Record<string, string | boolean>;

// 允许直接粘贴发现文档地址，也容忍结尾多出来的斜杠
function normalizeIssuer(raw: string): URL {
  const trimmed = raw.trim().replace(/\/(\.well-known\/openid-configuration)?\/?$/i, '');
  let issuer: URL;
  try {
    issuer = new URL(trimmed);
  } catch {
    throw new Error(t('settings.messages.discoverInvalid'));
  }
  if (issuer.protocol !== 'http:' && issuer.protocol !== 'https:') {
    throw new Error(t('settings.messages.discoverInvalid'));
  }
  return issuer;
}

// 读取身份源的 openid 发现文档，返回可直接写进配置项的差集。
// 服务端加载元数据时会核对 issuer 与 Authority 是否一致，这里同样只接受一致的结果。
export async function readOidcDiscovery(authority: string): Promise<DiscoveryPatch> {
  const issuer = normalizeIssuer(authority);
  const base = issuer.href.replace(/\/+$/, '');
  const documentUrl = `${base}/.well-known/openid-configuration`;

  let response: Response;
  try {
    response = await fetch(documentUrl, { signal: AbortSignal.timeout(TIMEOUT_MS) });
  } catch (error) {
    if ((error as Error).name === 'TimeoutError') {
      throw new Error(t('settings.messages.discoverTimeout', { url: documentUrl }));
    }
    throw new Error(t('settings.messages.discoverNetworkFail', { url: documentUrl }));
  }

  if (!response.ok) {
    throw new Error(t('settings.messages.discoverHttpStatus', { url: documentUrl, status: response.status }));
  }

  let discovery: DiscoveryDocument;
  try {
    discovery = (await response.json()) as DiscoveryDocument;
  } catch {
    throw new Error(t('settings.messages.discoverBadJson', { url: documentUrl }));
  }

  const discovered = typeof discovery.issuer === 'string' ? discovery.issuer.trim().replace(/\/+$/, '') : '';
  if (!discovered) {
    throw new Error(t('settings.messages.discoverIssuerMissing'));
  }
  if (discovered !== base) {
    throw new Error(t('settings.messages.discoverIssuerMismatch', { actual: discovered, expected: base }));
  }

  const patch: DiscoveryPatch = { 'Oidc.Authority': discovered };

  const supported = Array.isArray(discovery.scopes_supported) ? discovery.scopes_supported : undefined;
  if (supported) {
    const usable = STANDARD_SCOPES.filter((scope) => supported.includes(scope));
    if (usable.length) {
      patch['Oidc.Scope'] = usable.join(' ');
    }
  }

  // http 身份源在 RequireHttpsMetadata 为 true 时连元数据都加载不了，本地联调必须关掉
  if (issuer.protocol === 'http:') {
    patch['Oidc.RequireHttpsMetadata'] = false;
  }

  return patch;
}
