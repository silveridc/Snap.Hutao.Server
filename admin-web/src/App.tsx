import type { ReactElement } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router';

import { Spin } from '@douyinfe/semi-ui';

import { AuthProvider, useAuth } from '@/auth/AuthProvider';
import { navItems } from '@/config/nav';
import AdminLayout from '@/layouts/AdminLayout';
import AdminAccountPage from '@/pages/AdminAccountPage';
import AnalyticsPage from '@/pages/AnalyticsPage';
import AnnouncementPage from '@/pages/AnnouncementPage';
import AuditPage from '@/pages/AuditPage';
import BanPage from '@/pages/BanPage';
import CachePage from '@/pages/CachePage';
import ClientVersionPage from '@/pages/ClientVersionPage';
import CompensationPage from '@/pages/CompensationPage';
import DashboardPage from '@/pages/DashboardPage';
import DataToolsPage from '@/pages/DataToolsPage';
import HomePage from '@/pages/HomePage';
import LicensePage from '@/pages/LicensePage';
import LoginPage from '@/pages/LoginPage';
import LoginLogPage from '@/pages/LoginLogPage';
import MailTemplatePage from '@/pages/MailTemplatePage';
import MigratingPage from '@/pages/MigratingPage';
import NotFoundPage from '@/pages/NotFoundPage';
import RankPage from '@/pages/RankPage';
import RedeemPage from '@/pages/RedeemPage';
import ReleasePage from '@/pages/ReleasePage';
import SettingsPage from '@/pages/SettingsPage';
import StatisticsPage from '@/pages/StatisticsPage';
import SsoCallbackPage from '@/pages/SsoCallbackPage';
import UserPage from '@/pages/UserPage';
import WallpaperPage from '@/pages/WallpaperPage';

function Splash() {
  return (
    <div className="flex min-h-screen items-center justify-center">
      <Spin size="large" />
    </div>
  );
}

function RequireAuth({ children }: { children: ReactElement }) {
  const { ready, user } = useAuth();
  const location = useLocation();
  if (!ready) {
    return <Splash />;
  }
  if (!user) {
    // 登录页与身份源回跳都按 ?redirect= 取原地址
    return <Navigate to={`/login?redirect=${encodeURIComponent(location.pathname)}`} replace />;
  }
  return children;
}

// 只读身份也允许浏览，写操作由后端拒绝
function Shell({ children }: { children: ReactElement }) {
  return <RequireAuth>{children}</RequireAuth>;
}

// 页面按批次迁移，这里登记已迁好的页面，未登记的导航项统一落到占位页
const pages: Record<string, ReactElement> = {
  '/': <HomePage />,
  '/adminaccount': <AdminAccountPage />,
  '/analytics': <AnalyticsPage />,
  '/announcement': <AnnouncementPage />,
  '/audit': <AuditPage />,
  '/ban': <BanPage />,
  '/cache': <CachePage />,
  '/clientversion': <ClientVersionPage />,
  '/compensation': <CompensationPage />,
  '/dashboard': <DashboardPage />,
  '/datatools': <DataToolsPage />,
  '/license': <LicensePage />,
  '/loginlog': <LoginLogPage />,
  '/mailtemplate': <MailTemplatePage />,
  '/rank': <RankPage />,
  '/redeem': <RedeemPage />,
  '/release': <ReleasePage />,
  '/settings': <SettingsPage />,
  '/statistics': <StatisticsPage />,
  '/user': <UserPage />,
  '/wallpaper': <WallpaperPage />,
};

export default function App() {
  return (
    <AuthProvider>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/sso" element={<SsoCallbackPage />} />
        <Route
          element={
            <Shell>
              <AdminLayout />
            </Shell>
          }
        >
          {navItems.map((item) => (
            <Route key={item.path} path={item.path} element={pages[item.path] ?? <MigratingPage />} />
          ))}
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </AuthProvider>
  );
}
