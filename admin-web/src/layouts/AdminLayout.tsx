import { useEffect, useMemo, useState } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router';

import {
  IconChevronLeft,
  IconChevronRight,
  IconExit,
  IconLanguage,
  IconMoonStroked,
  IconSunStroked,
} from '@douyinfe/semi-icons';
import { Avatar, Dropdown, Layout, Nav, Select, Space, Tag, Tooltip, Typography } from '@douyinfe/semi-ui';

import { useAuth } from '@/auth/AuthProvider';
import { navItems } from '@/config/nav';
import { t, useLocale, setLocale, supportedLocales, languageNames, type SupportedLocale } from '@/i18n';
import { useTheme } from '@/lib/theme';

const { Header, Sider, Content } = Layout;

export default function AdminLayout() {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuth();
  const locale = useLocale();
  const { dark, setMode } = useTheme();
  const [collapsed, setCollapsed] = useState(false);

  // 小屏默认收起侧栏，避免导航挤占表格宽度
  useEffect(() => {
    const sync = () => setCollapsed(window.innerWidth < 900);
    sync();
    window.addEventListener('resize', sync);
    return () => window.removeEventListener('resize', sync);
  }, []);

  const selectedKey = useMemo(() => {
    const exact = navItems.find((item) => item.path === location.pathname);
    return exact ? exact.path : '/';
  }, [location.pathname]);

  const current = navItems.find((item) => item.path === selectedKey);

  return (
    <Layout className="min-h-screen">
      <Sider
        className="!bg-semi-color-bg-1"
        style={{ width: collapsed ? 64 : 220, transition: 'width 0.2s ease' }}
      >
        <div className="flex h-14 items-center gap-2 px-4">
          <img src={`${import.meta.env.BASE_URL}favicon.ico`} alt="" className="h-6 w-6" />
          {!collapsed && (
            <div className="min-w-0">
              <Typography.Text className="!text-[15px] font-semibold !text-semi-color-text-0" ellipsis>
                {t('common.brand')}
              </Typography.Text>
              <div className="text-xs text-semi-color-text-2">{t('common.subtitle')}</div>
            </div>
          )}
        </div>
        <Nav
          selectedKeys={[selectedKey]}
          isCollapsed={collapsed}
          footer={null}
          className="!bg-transparent"
          onSelect={(data) => {
            const key = String(data.itemKey ?? '');
            if (key) {
              navigate(key);
            }
          }}
        >
          {navItems.map((item) => (
            <Nav.Item key={item.path} itemKey={item.path} text={t(item.navKey)} icon={item.icon} />
          ))}
        </Nav>
      </Sider>

      <Layout>
        <Header className="flex h-14 items-center gap-3 border-b border-semi-color-border bg-semi-color-bg-1 px-4">
          <button
            type="button"
            aria-label={collapsed ? t('layout.expand') : t('layout.collapse')}
            onClick={() => setCollapsed((value) => !value)}
            className="flex h-8 w-8 items-center justify-center rounded-semi-radius-small text-semi-color-text-1 hover:bg-semi-color-fill-0"
          >
            {collapsed ? <IconChevronRight /> : <IconChevronLeft />}
          </button>

          <Typography.Text className="!whitespace-nowrap !text-[15px] font-semibold">
            {current ? t(current.navKey) : ''}
          </Typography.Text>

          {!user?.CanWrite && (
            <Tooltip content={t('common.readOnlyTip')}>
              <Tag color="orange" size="small">
                {t('common.readOnly')}
              </Tag>
            </Tooltip>
          )}

          <Space className="ml-auto" spacing="tight">
            <Tooltip content={dark ? t('common.themeLight') : t('common.themeDark')}>
              <button
                type="button"
                onClick={() => setMode(dark ? 'light' : 'dark')}
                aria-label={t('common.themeLight')}
                className="flex h-8 w-8 items-center justify-center rounded-semi-radius-small text-semi-color-text-1 hover:bg-semi-color-fill-0"
              >
                {dark ? <IconSunStroked /> : <IconMoonStroked />}
              </button>
            </Tooltip>

            <span className="hidden sm:inline-flex">
              <Select
                size="small"
                value={locale}
                onChange={(value) => setLocale(value as SupportedLocale)}
                prefix={<IconLanguage />}
                optionList={supportedLocales.map((code) => ({ value: code, label: languageNames[code] }))}
                className="!w-[132px]"
              />
            </span>

            <Dropdown
              trigger="click"
              clickOnMenu
              render={
                <Dropdown.Menu>
                  <Dropdown.Item icon={<IconExit />} onClick={logout}>
                    {t('common.logout')}
                  </Dropdown.Item>
                </Dropdown.Menu>
              }
            >
              <div className="flex cursor-pointer items-center gap-2 rounded-semi-radius-small px-2 py-1 hover:bg-semi-color-fill-0">
                <Avatar size="small" color="light-blue">
                  {(user?.UserName ?? '?').slice(0, 1).toUpperCase()}
                </Avatar>
                <div className="hidden leading-tight sm:block">
                  <div className="max-w-[160px] truncate text-[13px] text-semi-color-text-0">{user?.UserName}</div>
                  <div className="text-[11px] text-semi-color-text-2">
                    {user?.AuthKind === 'admin' ? t('layout.identity.kindAdmin') : t('layout.identity.kindUser')}
                  </div>
                </div>
              </div>
            </Dropdown>
          </Space>
        </Header>

        <Content className="bg-semi-color-bg-0 p-4">
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  );
}
