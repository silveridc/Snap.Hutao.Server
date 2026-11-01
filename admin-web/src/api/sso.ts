import { get, post } from '@/lib/http';
import type { AdminSsoStatus, AdminSsoTokenResponse } from '@/types/api';

// 后台是否启用单点登录，登录页据此决定是否显示 SSO 入口
export async function getSsoStatus(): Promise<AdminSsoStatus> {
  const envelope = await get<AdminSsoStatus>('/api/admin/sso/status');
  return envelope.data;
}

// 取跳转到身份源的授权地址，拿到后整页跳转（不要在 XHR 里跟随）
export async function getSsoChallenge(returnPath: string): Promise<string> {
  const envelope = await get<string>('/api/admin/sso/challenge', { returnPath });
  return envelope.data;
}

// 用一次性票据换取后台令牌，票据只能用一次
export async function exchangeSsoTicket(ticket: string): Promise<AdminSsoTokenResponse> {
  const envelope = await post<AdminSsoTokenResponse>('/api/admin/sso/sessions', { Ticket: ticket });
  return envelope.data;
}
