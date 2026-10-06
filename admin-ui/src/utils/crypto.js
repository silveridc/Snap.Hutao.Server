import { RSA_PUBLIC_KEY_DER_BASE64 } from '@/config/publicKey'

// 使用浏览器原生 Web Crypto 做 RSA-OAEP(SHA-1) 加密。
// 注意：Web Crypto 仅在安全上下文可用 (https 或 http://localhost)。
// 生产环境后端启用了 HttpsRedirection，通过 https 访问即可。

function base64ToArrayBuffer(base64) {
  const binary = atob(base64)
  const bytes = new Uint8Array(binary.length)
  for (let i = 0; i < binary.length; i += 1) {
    bytes[i] = binary.charCodeAt(i)
  }
  return bytes.buffer
}

function arrayBufferToBase64(buffer) {
  const bytes = new Uint8Array(buffer)
  let binary = ''
  for (let i = 0; i < bytes.length; i += 1) {
    binary += String.fromCharCode(bytes[i])
  }
  return btoa(binary)
}

let keyPromise = null

function importPublicKey() {
  if (!keyPromise) {
    keyPromise = crypto.subtle.importKey(
      'spki',
      base64ToArrayBuffer(RSA_PUBLIC_KEY_DER_BASE64),
      { name: 'RSA-OAEP', hash: 'SHA-1' },
      false,
      ['encrypt'],
    )
  }
  return keyPromise
}

// 对明文做 RSA 加密并返回 base64 字符串，与后端 Convert.FromBase64String + OaepSHA1 对应。
export async function rsaEncrypt(plain) {
  if (!globalThis.crypto?.subtle) {
    throw new Error('当前环境不支持 Web Crypto，请通过 https 或 localhost 访问管理后台')
  }
  const key = await importPublicKey()
  const data = new TextEncoder().encode(plain)
  const encrypted = await crypto.subtle.encrypt({ name: 'RSA-OAEP' }, key, data)
  return arrayBufferToBase64(encrypted)
}
