import { bare, get, RET_SUCCESS } from '@/lib/http';
import { getDeviceHeaders } from '@/lib/device';
import { rsaEncrypt } from '@/lib/crypto';
import type { CaptchaSettings, Envelope, TokenResponse, UserInfo } from '@/types/api';

// 登录：邮箱与密码都要 RSA 加密后提交。接口不带令牌，走裸实例，
// 免得拦截器给未登录请求加上过期的 Authorization。
export async function login(email: string, password: string, captchaToken?: string): Promise<TokenResponse> {
  const [UserName, Password] = await Promise.all([rsaEncrypt(email), rsaEncrypt(password)]);
  const { data: envelope } = await bare.post<Envelope<TokenResponse>>('/api/passport/v2/sessions',
    { UserName, Password, CaptchaToken: captchaToken },
    { headers: getDeviceHeaders() },
  );
  if (envelope.retcode !== RET_SUCCESS) {
    throw new Error(envelope.message || '登录失败');
  }
  return envelope.data;
}

// 未启用人机验证时 Enabled=false，登录页据此决定是否请求 token
export async function getCaptchaSettings(): Promise<CaptchaSettings | null> {
  const envelope = await get<CaptchaSettings>('/api/passport/v2/captcha');
  return envelope.data ?? null;
}

export async function fetchUserInfo(): Promise<UserInfo> {
  const envelope = await get<UserInfo>('/api/passport/v2/me');
  return envelope.data;
}
