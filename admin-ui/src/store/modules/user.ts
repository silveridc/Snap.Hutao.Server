import { defineStore } from 'pinia';

import { fetchUserInfo, login as apiLogin } from '@/api/passport';
import { exchangeSsoTicket } from '@/api/sso';
import { usePermissionStore } from '@/store';
import { clearTokens, getAccessToken, setTokens } from '@/utils/token';

interface AdminUserInfo {
  name: string;
  roles: string[];
  isMaintainer: boolean;
  isLicensedDeveloper: boolean;
  /** 身份类型：主站用户 user，后台专属账号 admin */
  authKind: string;
  /** 当前身份能否执行写操作 (启用单点登录后主站维护者会被降级为只读) */
  canWrite: boolean;
  gachaLogExpireAt?: string;
  cdnExpireAt?: string;
}

const InitUserInfo: AdminUserInfo = {
  name: '',
  roles: [],
  isMaintainer: false,
  isLicensedDeveloper: false,
  authKind: 'user',
  canWrite: false,
};

export const useUserStore = defineStore('user', {
  state: () => ({
    token: getAccessToken() || '',
    userInfo: { ...InitUserInfo },
  }),
  getters: {
    roles: (state) => state.userInfo?.roles,
  },
  actions: {
    async login(userInfo: { email?: string; account?: string; password: string; captchaToken?: string }) {
      const email = (userInfo.email ?? userInfo.account ?? '').trim();
      const token = await apiLogin(email, userInfo.password, userInfo.captchaToken);
      setTokens({
        accessToken: token.AccessToken,
        refreshToken: token.RefreshToken,
        expiresIn: token.ExpiresIn,
      });
      this.token = getAccessToken() || '';
    },
    async getUserInfo() {
      const info = await fetchUserInfo();
      this.userInfo = {
        name: info.UserName,
        roles: ['all'],
        isMaintainer: Boolean(info.IsMaintainer),
        isLicensedDeveloper: Boolean(info.IsLicensedDeveloper),
        authKind: info.AuthKind || 'user',
        canWrite: Boolean(info.CanWrite),
        gachaLogExpireAt: info.GachaLogExpireAt,
        cdnExpireAt: info.CdnExpireAt,
      };
    },
    // 单点登录回跳页用一次性票据换后台令牌。后台令牌不带刷新令牌，先清空旧令牌，
    // 否则过期后会静默刷新回主站用户身份。
    async loginWithTicket(ticket: string) {
      const res: any = await exchangeSsoTicket(ticket);
      clearTokens();
      setTokens({
        accessToken: res.data.AccessToken,
        expiresIn: res.data.ExpiresIn,
      });
      this.token = getAccessToken() || '';
      await this.getUserInfo();
      return res.data;
    },
    async logout() {
      clearTokens();
      this.token = '';
      this.userInfo = { ...InitUserInfo };
    },
  },
  persist: {
    afterHydrate: () => {
      const permissionStore = usePermissionStore();
      permissionStore.initRoutes();
    },
    key: 'user',
    pick: [],
  },
});
