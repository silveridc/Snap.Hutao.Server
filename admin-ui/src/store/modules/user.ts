import { defineStore } from 'pinia';

import { fetchUserInfo, login as apiLogin } from '@/api/passport';
import { usePermissionStore } from '@/store';
import { clearTokens, getAccessToken, setTokens } from '@/utils/token';

interface AdminUserInfo {
  name: string;
  roles: string[];
  isMaintainer: boolean;
  isLicensedDeveloper: boolean;
  gachaLogExpireAt?: string;
  cdnExpireAt?: string;
}

const InitUserInfo: AdminUserInfo = {
  name: '',
  roles: [],
  isMaintainer: false,
  isLicensedDeveloper: false,
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
    async login(userInfo: { email?: string; account?: string; password: string }) {
      const email = (userInfo.email ?? userInfo.account ?? '').trim();
      const token = await apiLogin(email, userInfo.password);
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
        gachaLogExpireAt: info.GachaLogExpireAt,
        cdnExpireAt: info.CdnExpireAt,
      };
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
