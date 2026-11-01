import { RSA_PUBLIC_KEY_DER_BASE64 } from './publicKey';

// Web Crypto 的 RSA-OAEP(SHA-1)，与后端 RSAEncryptionPadding.OaepSHA1 对应.
// 仅在安全上下文可用（https 或 localhost），开发用 http://localhost:5174 满足条件.

function base64ToArrayBuffer(base64: string): ArrayBuffer {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i += 1) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes.buffer;
}

function arrayBufferToBase64(buffer: ArrayBuffer): string {
  let binary = '';
  for (const byte of new Uint8Array(buffer)) {
    binary += String.fromCharCode(byte);
  }
  return btoa(binary);
}

let keyPromise: Promise<CryptoKey> | null = null;

function importPublicKey(): Promise<CryptoKey> {
  if (!keyPromise) {
    keyPromise = crypto.subtle.importKey(
      'spki',
      base64ToArrayBuffer(RSA_PUBLIC_KEY_DER_BASE64),
      { name: 'RSA-OAEP', hash: 'SHA-1' },
      false,
      ['encrypt'],
    );
  }
  return keyPromise;
}

export async function rsaEncrypt(plain: string): Promise<string> {
  if (!globalThis.crypto?.subtle) {
    throw new Error('当前环境不支持 Web Crypto，请通过 https 或 localhost 访问管理后台');
  }
  const key = await importPublicKey();
  const encrypted = await crypto.subtle.encrypt({ name: 'RSA-OAEP' }, key, new TextEncoder().encode(plain));
  return arrayBufferToBase64(encrypted);
}
