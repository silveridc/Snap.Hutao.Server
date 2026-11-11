import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';

import { fetchUserInfo, login as apiLogin } from '@/api/passport';
import { exchangeSsoTicket } from '@/api/sso';
import { clearTokens, getAccessToken, hasToken, setTokens, AUTH_EXPIRED_EVENT } from '@/lib/token';
import type { UserInfo } from '@/types/api';

interface AuthValue {
  /** 首次校验本地令牌是否还有效的过程 */
  ready: boolean;
  user: UserInfo | null;
  login(email: string, password: string, captchaToken?: string): Promise<void>;
  loginWithTicket(ticket: string): Promise<void>;
  /** 重新拉取当前身份，用于改权限/续期后刷新界面 */
  refresh(): Promise<void>;
  logout(): void;
}

const AuthContext = createContext<AuthValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(false);
  const [user, setUser] = useState<UserInfo | null>(null);

  useEffect(() => {
    if (!hasToken()) {
      setReady(true);
      return;
    }
    let cancelled = false;
    fetchUserInfo()
      .then((info) => {
        if (!cancelled) {
          setUser(info);
        }
      })
      .catch(() => {
        clearTokens();
      })
      .finally(() => {
        if (!cancelled) {
          setReady(true);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  // 请求层刷新失败后广播，这里只负责把界面切回未登录态，跳转交给路由守卫
  useEffect(() => {
    const onExpired = () => setUser(null);
    window.addEventListener(AUTH_EXPIRED_EVENT, onExpired);
    return () => window.removeEventListener(AUTH_EXPIRED_EVENT, onExpired);
  }, []);

  const login = useCallback(async (email: string, password: string, captchaToken?: string) => {
    const token = await apiLogin(email, password, captchaToken);
    setTokens({
      accessToken: token.AccessToken,
      refreshToken: token.RefreshToken,
      expiresIn: token.ExpiresIn,
    });
    setUser(await fetchUserInfo());
  }, []);

  // 后台令牌不带刷新令牌，先清空旧令牌，否则过期后会静默刷新回主站用户身份
  const loginWithTicket = useCallback(async (ticket: string) => {
    const token = await exchangeSsoTicket(ticket);
    clearTokens();
    setTokens({ accessToken: token.AccessToken, expiresIn: token.ExpiresIn });
    setUser(await fetchUserInfo());
  }, []);

  const refresh = useCallback(async () => {
    setUser(await fetchUserInfo());
  }, []);

  const logout = useCallback(() => {
    clearTokens();
    setUser(null);
  }, []);

  const value = useMemo<AuthValue>(
    () => ({ ready, user, login, loginWithTicket, refresh, logout }),
    [ready, user, login, loginWithTicket, refresh, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthValue {
  const value = useContext(AuthContext);
  if (!value) {
    throw new Error('useAuth must be used inside AuthProvider');
  }
  return value;
}

export function isSignedIn(): boolean {
  return Boolean(getAccessToken());
}
