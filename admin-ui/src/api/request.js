import axios from 'axios'
import { i18n } from '@/locales'
import { getAccessToken, getRefreshToken, setTokens, clearTokens, emitAuthExpired } from '@/utils/token'
import { getDeviceHeaders } from '@/utils/device'
import { rsaEncrypt } from '@/utils/crypto'

// 服务端返回消息本地化：l10nKey → pages/server.json（与 launcher 的 SH.resx 同源，scripts/extract-server-l10n.mjs 生成）
const SERVER_KEY_PREFIX = 'pages.server.'

// 优先按 l10nKey 查本地化文案，查不到回退服务端 message（英文）
function localizedMessage(envelope) {
  const key = envelope?.l10nKey
  if (key && i18n.global.te(SERVER_KEY_PREFIX + key)) {
    return i18n.global.t(SERVER_KEY_PREFIX + key)
  }
  return envelope?.message
}

// 后端返回码 (Models/Response/ReturnCode.cs)
export const RET_SUCCESS = 0
export const RET_LOGIN_FAIL = 514002 // 未登录 / 令牌失效
export const RET_NOT_MAINTAINER = 516001 // 非官方人员

const http = axios.create({ timeout: 20000 })

// 独立实例用于刷新令牌，避免触发拦截器造成递归
const bare = axios.create({ timeout: 20000 })

http.interceptors.request.use((config) => {
  const token = getAccessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

let refreshPromise = null

async function refreshTokens() {
  const refreshToken = getRefreshToken()
  if (!refreshToken) {
    return false
  }
  if (!refreshPromise) {
    refreshPromise = (async () => {
      try {
        const encrypted = await rsaEncrypt(refreshToken)
        const { data: envelope } = await bare.post(
          '/Passport/v2/RefreshToken',
          { RefreshToken: encrypted },
          { headers: getDeviceHeaders() },
        )
        if (envelope?.retcode === RET_SUCCESS && envelope.data?.AccessToken) {
          setTokens({
            accessToken: envelope.data.AccessToken,
            refreshToken: envelope.data.RefreshToken,
            expiresIn: envelope.data.ExpiresIn,
          })
          return true
        }
        return false
      } catch {
        return false
      } finally {
        refreshPromise = null
      }
    })()
  }
  return refreshPromise
}

function toError(envelope) {
  const err = new Error(localizedMessage(envelope) || i18n.global.t(`${SERVER_KEY_PREFIX}requestFailed`))
  err.retcode = envelope?.retcode
  err.l10nKey = envelope?.l10nKey
  return err
}

http.interceptors.response.use(
  async (response) => {
    const envelope = response.data
    // 非标准信封 (例如透传的第三方数据) 直接返回
    if (!envelope || typeof envelope.retcode === 'undefined') {
      return envelope
    }
    if (envelope.retcode === RET_SUCCESS) {
      envelope.message = localizedMessage(envelope)
      return envelope
    }
    if (envelope.retcode === RET_LOGIN_FAIL && !response.config._retried) {
      const ok = await refreshTokens()
      if (ok) {
        response.config._retried = true
        return http.request(response.config)
      }
      clearTokens()
      emitAuthExpired()
      throw toError(envelope)
    }
    if (envelope.retcode === RET_LOGIN_FAIL) {
      clearTokens()
      emitAuthExpired()
    }
    throw toError(envelope)
  },
  (error) => {
    const message = error.response
      ? i18n.global.t(`${SERVER_KEY_PREFIX}httpError`, { code: error.response.status })
      : error.message || i18n.global.t(`${SERVER_KEY_PREFIX}networkError`)
    return Promise.reject(new Error(message))
  },
)

export default http
export { bare }
