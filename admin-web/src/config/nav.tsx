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
}

export const navItems: NavItem[] = [
  { path: '/', navKey: 'layout.nav.home', icon: <IconHomeStroked /> },
  { path: '/dashboard', navKey: 'layout.nav.dashboard', icon: <IconHistogram /> },
  { path: '/user', navKey: 'layout.nav.user', icon: <IconUser /> },
  { path: '/ban', navKey: 'layout.nav.ban', icon: <IconShieldStroked /> },
  { path: '/statistics', navKey: 'layout.nav.statistics', icon: <IconBarChartVStroked /> },
  { path: '/rank', navKey: 'layout.nav.rank', icon: <IconPieChartStroked /> },
  { path: '/compensation', navKey: 'layout.nav.compensation', icon: <IconGiftStroked /> },
  { path: '/announcement', navKey: 'layout.nav.announcement', icon: <IconBellStroked /> },
  { path: '/release', navKey: 'layout.nav.release', icon: <IconCloudUploadStroked /> },
  { path: '/wallpaper', navKey: 'layout.nav.wallpaper', icon: <IconImageStroked /> },
  { path: '/redeem', navKey: 'layout.nav.redeem', icon: <IconTicketCodeStroked /> },
  { path: '/datatools', navKey: 'layout.nav.datatools', icon: <IconWrenchStroked /> },
  { path: '/cache', navKey: 'layout.nav.cache', icon: <IconServerStroked /> },
  { path: '/audit', navKey: 'layout.nav.audit', icon: <IconHistory /> },
  { path: '/loginlog', navKey: 'layout.nav.loginlog', icon: <IconLockStroked /> },
  { path: '/analytics', navKey: 'layout.nav.analytics', icon: <IconArticle /> },
  { path: '/mailtemplate', navKey: 'layout.nav.mailtemplate', icon: <IconMailStroked /> },
  { path: '/license', navKey: 'layout.nav.license', icon: <IconCoinMoneyStroked /> },
  { path: '/adminaccount', navKey: 'layout.nav.adminaccount', icon: <IconKeyStroked /> },
  { path: '/clientversion', navKey: 'layout.nav.clientversion', icon: <IconLayers /> },
  { path: '/settings', navKey: 'layout.nav.settings', icon: <IconSettingStroked /> },
];

export function navItemByPath(path: string): NavItem | undefined {
  return navItems.find((item) => item.path === path);
}
