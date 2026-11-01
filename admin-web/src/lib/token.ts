const ACCESS_KEY = 'hutao-admin-access-token';
const REFRESH_KEY = 'hutao-admin-refresh-token';
const EXPIRES_KEY = 'hutao-admin-expires-at';

function read(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function write(key: string, value: string | null): void {
  try {
    if (value === null) {
      localStorage.removeItem(key);
    } else {
      localStorage.setItem(key, value);
    }
  } catch {
    /* 隐私模式下 localStorage 会抛异常，忽略即可 */
  }
}

export function getAccessToken(): string | null {
  return read(ACCESS_KEY);
}

export function getRefreshToken(): string | null {
  return read(REFRESH_KEY);
}

export interface TokenInput {
  accessToken: string;
  refreshToken?: string;
  expiresIn?: number;
}

// expiresIn 是后端 TokenResponse.ExpiresIn 的秒数
export function setTokens({ accessToken, refreshToken, expiresIn }: TokenInput): void {
  write(ACCESS_KEY, accessToken);
  if (refreshToken) {
    write(REFRESH_KEY, refreshToken);
  }
  if (expiresIn) {
    write(EXPIRES_KEY, String(Date.now() + expiresIn * 1000));
  }
}

export function clearTokens(): void {
  write(ACCESS_KEY, null);
  write(REFRESH_KEY, null);
  write(EXPIRES_KEY, null);
}

export function hasToken(): boolean {
  return Boolean(getAccessToken());
}

export const AUTH_EXPIRED_EVENT = 'hutao-admin:auth-expired';

export function emitAuthExpired(): void {
  window.dispatchEvent(new CustomEvent(AUTH_EXPIRED_EVENT));
}
