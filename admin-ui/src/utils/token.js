// 令牌持久化 (localStorage)。与 Pinia store 解耦，供 axios 拦截器在模块加载期安全引用。
const ACCESS_KEY = 'hutao-admin-access-token'
const REFRESH_KEY = 'hutao-admin-refresh-token'
const EXPIRES_KEY = 'hutao-admin-expires-at'

function read(key) {
  try {
    return localStorage.getItem(key)
  } catch {
    return null
  }
}

function write(key, value) {
  try {
    if (value == null) {
      localStorage.removeItem(key)
    } else {
      localStorage.setItem(key, value)
    }
  } catch {
    /* ignore */
  }
}

export function getAccessToken() {
  return read(ACCESS_KEY)
}

export function getRefreshToken() {
  return read(REFRESH_KEY)
}

export function getExpiresAt() {
  const raw = read(EXPIRES_KEY)
  return raw ? Number(raw) : 0
}

// expiresIn 单位为秒，来自后端 TokenResponse.ExpiresIn。
export function setTokens({ accessToken, refreshToken, expiresIn }) {
  write(ACCESS_KEY, accessToken)
  if (refreshToken) {
    write(REFRESH_KEY, refreshToken)
  }
  if (expiresIn) {
    write(EXPIRES_KEY, String(Date.now() + expiresIn * 1000))
  }
}

export function clearTokens() {
  write(ACCESS_KEY, null)
  write(REFRESH_KEY, null)
  write(EXPIRES_KEY, null)
}

export function hasToken() {
  return Boolean(getAccessToken())
}

export const AUTH_EXPIRED_EVENT = 'hutao-admin:auth-expired'

export function emitAuthExpired() {
  window.dispatchEvent(new CustomEvent(AUTH_EXPIRED_EVENT))
}
