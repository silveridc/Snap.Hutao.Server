// API 冒烟测试。凭据从环境变量读取，不落盘。
//   BASE=https://snap-api.sliveridc1.cn EMAIL=... PASSWORD=... node scripts/api-smoke.mjs
const BASE = process.env.BASE || 'https://snap-api.sliveridc1.cn'
const EMAIL = process.env.EMAIL
const PASSWORD = process.env.PASSWORD
const PUBKEY_B64 =
  'MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA0Uv7IhpG+gBdwz8PFs0JGxblkYV+KIc8Yv1iNpXmT8mxLfNIStsn3ypSWlsdfikVU1CoPNGQUQWy6RAq1/SDOtHxUR1b/2E4HgPiGSwQRNnvdmi9FcgQx0pc6baNuHaF1HDSQRPh4a/LrJ2qGnMws0CZFNsFvXD0FbmJHfxJTZc+V+CZW28OLLeD576HuJO2LI6pHWj7yFjTxYKj2jfYq9PdjsJmOtMZwmazYhrWuiuoXriFmAMQ+MxcqRAUZGHbkMh3qUJJJi6baB99/xFKaStB7P/8FFy8P1zbKGNanZtZER6Ji7YmIikllS6hi4WUcXu0314ZDFu4K2ephJzudwIDAQAB'

if (!EMAIL || !PASSWORD) {
  console.error('missing EMAIL / PASSWORD env')
  process.exit(1)
}

const { webcrypto } = await import('node:crypto')
const key = await webcrypto.subtle.importKey('spki', Buffer.from(PUBKEY_B64, 'base64'), { name: 'RSA-OAEP', hash: 'SHA-1' }, false, ['encrypt'])
const enc = async (s) =>
  Buffer.from(await webcrypto.subtle.encrypt({ name: 'RSA-OAEP' }, key, new TextEncoder().encode(s))).toString('base64')

async function tryLogin(label, url, withDeviceHeaders) {
  const headers = { 'content-type': 'application/json' }
  if (withDeviceHeaders) {
    headers['x-hutao-device-id'] = 'test-device-0001'
    headers['x-hutao-device-name'] = 'Hutao Admin Web Test'
    headers['x-hutao-device-os'] = 'node-test'
  }
  const [UserName, Password] = await Promise.all([enc(EMAIL), enc(PASSWORD)])
  let body, status
  try {
    const r = await fetch(url, { method: 'POST', headers, body: JSON.stringify({ UserName, Password }) })
    status = r.status
    body = await r.text()
  } catch (e) {
    console.log(`${label}: FETCH ERROR ${e.message}`)
    return
  }
  let parsed
  try { parsed = JSON.parse(body) } catch { parsed = null }
  const hasToken = Boolean(parsed?.data?.AccessToken)
  console.log(`${label}: HTTP ${status} | retcode ${parsed?.retcode} | token=${hasToken} | msg ${parsed?.message ?? body.slice(0, 120)}`)
}

console.log('--- v2 WITHOUT device headers ---')
await tryLogin('v2/Login  (no headers)', `${BASE}/Passport/v2/Login`, false)
console.log('--- v2 WITH device headers ---')
await tryLogin('v2/Login  (headers)   ', `${BASE}/Passport/v2/Login`, true)
console.log('--- legacy WITHOUT device headers ---')
await tryLogin('Passport/Login (legacy)', `${BASE}/Passport/Login`, false)
