import {
  IconArticle,
  IconBarChartVStroked,
  IconBellStroked,
  IconCloudUploadStroked,
  IconCoinMoneyStroked,
  IconGiftStroked,
  IconHistory,
  IconHomeStroked,
  IconImageStroked,
  IconKeyStroked,
  IconLayers,
  IconLockStroked,
  IconMailStroked,
  IconPieChartStroked,
  IconServerStroked,
  IconSettingStroked,
  IconShieldStroked,
  IconTicketCodeStroked,
  IconUser,
  IconWrenchStroked,
  IconHistogram,
} from '@douyinfe/semi-icons';
import type { ReactNode } from 'react';

export interface NavItem {
  path: string;
  /** layout.nav.* 词条键 */
  navKey: string;
  icon: ReactNode;
  /** layout.nav.group.* 词条键，侧栏在每组第一项上方渲染分组标题（同 newapi classic） */
  group?: string;
}

export const navItems: NavItem[] = [
  { path: '/', navKey: 'layout.nav.home', icon: <IconHomeStroked />, group: 'layout.nav.group.overview' },
  { path: '/dashboard', navKey: 'layout.nav.dashboard', icon: <IconHistogram /> },
  { path: '/user', navKey: 'layout.nav.user', icon: <IconUser />, group: 'layout.nav.group.users' },
  { path: '/ban', navKey: 'layout.nav.ban', icon: <IconShieldStroked /> },
  { path: '/adminaccount', navKey: 'layout.nav.adminaccount', icon: <IconKeyStroked /> },
  { path: '/compensation', navKey: 'layout.nav.compensation', icon: <IconGiftStroked />, group: 'layout.nav.group.content' },
  { path: '/announcement', navKey: 'layout.nav.announcement', icon: <IconBellStroked /> },
  { path: '/redeem', navKey: 'layout.nav.redeem', icon: <IconTicketCodeStroked /> },
  { path: '/wallpaper', navKey: 'layout.nav.wallpaper', icon: <IconImageStroked /> },
  { path: '/mailtemplate', navKey: 'layout.nav.mailtemplate', icon: <IconMailStroked /> },
  { path: '/statistics', navKey: 'layout.nav.statistics', icon: <IconBarChartVStroked />, group: 'layout.nav.group.data' },
  { path: '/rank', navKey: 'layout.nav.rank', icon: <IconPieChartStroked /> },
  { path: '/analytics', navKey: 'layout.nav.analytics', icon: <IconArticle /> },
  { path: '/clientversion', navKey: 'layout.nav.clientversion', icon: <IconLayers /> },
  { path: '/release', navKey: 'layout.nav.release', icon: <IconCloudUploadStroked />, group: 'layout.nav.group.system' },
  { path: '/datatools', navKey: 'layout.nav.datatools', icon: <IconWrenchStroked /> },
  { path: '/cache', navKey: 'layout.nav.cache', icon: <IconServerStroked /> },
  { path: '/audit', navKey: 'layout.nav.audit', icon: <IconHistory /> },
  { path: '/loginlog', navKey: 'layout.nav.loginlog', icon: <IconLockStroked /> },
  { path: '/license', navKey: 'layout.nav.license', icon: <IconCoinMoneyStroked /> },
  { path: '/settings', navKey: 'layout.nav.settings', icon: <IconSettingStroked /> },
];

export function navItemByPath(path: string): NavItem | undefined {
  return navItems.find((item) => item.path === path);
}
