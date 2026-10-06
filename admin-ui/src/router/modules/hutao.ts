import {
  BarcodeIcon,
  CertificateIcon,
  ChartBarIcon,
  ChartBubbleIcon,
  CloudUploadIcon,
  DashboardIcon,
  GiftIcon,
  HistoryIcon,
  NotificationIcon,
  ServerIcon,
  SettingIcon,
  ToolsIcon,
  UserBlockedIcon,
  UsergroupIcon,
} from 'tdesign-icons-vue-next';
import { shallowRef } from 'vue';
import type { RouteRecordRaw } from 'vue-router';

import { LAYOUT } from '@/utils/route/constant';

// 单个顶级菜单项：父路由挂 LAYOUT，meta.single 让侧边栏渲染成一个可点击项
function single(path: string, name: string, zh: string, en: string, icon: any, orderNo: number): RouteRecordRaw {
  return {
    path: `/${path}`,
    component: LAYOUT,
    redirect: `/${path}/index`,
    name,
    meta: {
      title: { zh_CN: zh, en_US: en },
      icon: shallowRef(icon),
      orderNo,
      single: true,
    },
    children: [
      {
        path: 'index',
        name: `${name}Index`,
        component: () => import(`@/pages/${path}/index.vue`),
        meta: { title: { zh_CN: zh, en_US: en } },
      },
    ],
  };
}

export default [
  single('dashboard', 'Dashboard', '概览', 'Overview', DashboardIcon, 0),
  single('user', 'User', '用户管理', 'Users', UsergroupIcon, 1),
  single('ban', 'Ban', '封禁管理', 'Bans', UserBlockedIcon, 2),
  single('statistics', 'Statistics', '统计刷新', 'Statistics', ChartBubbleIcon, 3),
  single('rank', 'Rank', '排行榜', 'Leaderboard', ChartBarIcon, 4),
  single('compensation', 'Compensation', '时长补偿', 'Compensation', GiftIcon, 5),
  single('announcement', 'Announcement', '公告管理', 'Announcement', NotificationIcon, 6),
  single('release', 'Release', '版本发布', 'Release', CloudUploadIcon, 7),
  single('redeem', 'Redeem', '兑换码', 'Redeem Code', BarcodeIcon, 8),
  single('datatools', 'DataTools', '数据维护', 'Data Tools', ToolsIcon, 9),
  single('cache', 'Cache', '缓存管理', 'Cache', ServerIcon, 10),
  single('audit', 'Audit', '审计日志', 'Audit Log', HistoryIcon, 11),
  single('license', 'License', '开源许可证', 'License', CertificateIcon, 12),
  single('settings', 'Settings', '系统设置', 'Settings', SettingIcon, 99),
] satisfies RouteRecordRaw[];
