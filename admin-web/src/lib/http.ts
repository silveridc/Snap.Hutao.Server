import axios, { type AxiosError, type AxiosRequestConfig } from 'axios';

import { t, te } from '@/i18n';
import type { Envelope } from '@/types/api';

import { rsaEncrypt } from './crypto';
import { getDeviceHeaders } from './device';
import { clearTokens, getAccessToken, getRefreshToken, setTokens } from './token';

declare module 'axios' {
  export interface AxiosRequestConfig {
    // 标记已经用刷新后的令牌重试过一次，避免 514002 反复触发刷新
    _retried?: boolean;
  }
}

// 后端返回码，对应 server/Models/Response/ReturnCode.cs
export const RET_SUCCESS = 0;
export const RET_LOGIN_FAIL = 514002;
export const RET_NOT_MAINTAINER = 516001;

export class ApiError extends Error {
  retcode?: number;
  l10nKey?: string;

  constructor(message: string, retcode?: number, l10nKey?: string) {
    super(message);
    this.name = 'ApiError';
    this.retcode = retcode;
    this.l10nKey = l10nKey;
  }
}

// 服务端 l10nKey 与启动器 SH.resx 同源，词条在 i18n/<locale>/server.json
function localizedMessage(envelope: Envelope | undefined): string | undefined {
  const key = envelope?.l10nKey;
  if (key && te(`server.${key}`)) {
    return t(`server.${key}`);
  }
  return envelope?.message;
}

const http = axios.create({ timeout: 20000 });

// 独立实例走刷新，不挂拦截器，避免递归
const bare = axios.create({ timeout: 20000 });

http.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

let refreshPromise: Promise<boolean> | null = null;

async function refreshTokens(): Promise<boolean> {
  const refreshToken = getRefreshToken();
  if (!refreshToken) {
    return false;
  }
  if (!refreshPromise) {
    refreshPromise = (async () => {
      try {
        const { data: envelope } = await bare.post<Envelope<{ AccessToken: string; RefreshToken: string; ExpiresIn: number }>>(
          '/api/passport/v2/sessions/refresh',
          { RefreshToken: await rsaEncrypt(refreshToken) },
          { headers: getDeviceHeaders() },
        );
        if (envelope?.retcode === RET_SUCCESS && envelope.data?.AccessToken) {
          setTokens({
            accessToken: envelope.data.AccessToken,
            refreshToken: envelope.data.RefreshToken,
            expiresIn: envelope.data.ExpiresIn,
          });
          return true;
        }
        return false;
      } catch {
        return false;
      } finally {
        refreshPromise = null;
      }
    })();
  }
  return refreshPromise;
}

function dropSession(): void {
  clearTokens();
  window.dispatchEvent(new CustomEvent('hutao-admin:auth-expired'));
}

http.interceptors.response.use(
  (response) => {
    const envelope = response.data as Envelope | undefined;
    // 非标准信封（例如透传的第三方数据）原样返回
    if (!envelope || typeof envelope.retcode === 'undefined') {
      return envelope as never;
    }
    if (envelope.retcode === RET_SUCCESS) {
      envelope.message = localizedMessage(envelope) ?? envelope.message;
      return envelope as never;
    }
    if (envelope.retcode === RET_LOGIN_FAIL) {
      if (!response.config._retried) {
        return refreshTokens().then((ok) => {
          if (ok) {
            response.config._retried = true;
            return http.request(response.config);
          }
          dropSession();
          throw new ApiError(localizedMessage(envelope) ?? t('common.sessionExpired'), envelope.retcode, envelope.l10nKey);
        });
      }
      dropSession();
    }
    throw new ApiError(
      localizedMessage(envelope) ?? t('server.requestFailed'),
      envelope.retcode,
      envelope.l10nKey,
    );
  },
  (error: AxiosError) => {
    const message = error.response
      ? t('server.httpError', { code: error.response.status })
      : error.message || t('server.networkError');
    return Promise.reject(new Error(message));
  },
);

type Params = Record<string, string | number | boolean | null | undefined>;

export function get<T>(url: string, params?: Params, config?: AxiosRequestConfig): Promise<Envelope<T>> {
  return http.get(url, { params, ...config }) as unknown as Promise<Envelope<T>>;
}

export function post<T>(url: string, data?: unknown, params?: Params, config?: AxiosRequestConfig): Promise<Envelope<T>> {
  return http.post(url, data, { params, ...config }) as unknown as Promise<Envelope<T>>;
}

export function put<T>(url: string, data?: unknown, params?: Params): Promise<Envelope<T>> {
  return http.put(url, data, { params }) as unknown as Promise<Envelope<T>>;
}

export function patch<T>(url: string, data?: unknown, params?: Params): Promise<Envelope<T>> {
  return http.patch(url, data, { params }) as unknown as Promise<Envelope<T>>;
}

export function del<T>(url: string, params?: Params, data?: unknown, config?: AxiosRequestConfig): Promise<Envelope<T>> {
  return http.delete(url, { params, data, ...config }) as unknown as Promise<Envelope<T>>;
}

export { bare, http };
