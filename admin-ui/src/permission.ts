import 'nprogress/nprogress.css'; // progress bar style

import NProgress from 'nprogress'; // progress bar
import { MessagePlugin } from 'tdesign-vue-next';

import router from '@/router';
import { getPermissionStore, useUserStore } from '@/store';
import { normalizePath } from '@/utils/route';

NProgress.configure({ showSpinner: false });

router.beforeEach(async (to, from, next) => {
  NProgress.start();

  const permissionStore = getPermissionStore();
  const { whiteListRouters } = permissionStore;
  const userStore = useUserStore();

  if (userStore.token) {
    if (normalizePath(to.path) === '/login') {
      next();
      return;
    }
    try {
      // 所有页面均为固定路由，无需动态构建；仅确保菜单与用户信息就绪
      if (permissionStore.routers.length === 0) {
        await permissionStore.initRoutes();
      }
      await userStore.getUserInfo();
      next();
    } catch (error) {
      MessagePlugin.error((error as Error).message);
      next({ path: '/login', query: { redirect: to.fullPath } });
      NProgress.done();
    }
  } else {
    if (whiteListRouters.includes(normalizePath(to.path))) {
      next();
    } else {
      next({ path: '/login', query: { redirect: to.fullPath } });
    }
    NProgress.done();
  }
});

router.afterEach((to) => {
  if (normalizePath(to.path) === '/login') {
    const userStore = useUserStore();
    const permissionStore = getPermissionStore();

    userStore.logout();
    permissionStore.restoreRoutes();
  }
  NProgress.done();
});
