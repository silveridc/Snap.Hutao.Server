import { Fragment, useEffect, useMemo, useState } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router';

import {
  IconChevronLeft,
  IconExit,
  IconLanguage,
  IconMoonStroked,
  IconSunStroked,
} from '@douyinfe/semi-icons';
import { Avatar, Button, Dropdown, IconButton, Layout, Nav, Select, Space, Tag, Tooltip, Typography } from '@douyinfe/semi-ui';

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

  const collapseIcon = (
    <IconChevronLeft
      style={{
        transform: collapsed ? 'rotate(180deg)' : 'none',
        transition: 'transform 0.2s ease',
      }}
    />
  );

  return (
    // 整体锁在一屏内，让 Content 成为唯一滚动容器：
    // sticky 悬浮底栏（设置页）和侧栏固定都依赖这个滚动参照
    <Layout className="!h-screen overflow-hidden">
      {/* Semi 的 Nav 固定 240（收起时 60），侧栏必须跟它一致；shrink-0 不能省，
          宽表格的 min-content 会顺着 flex 链把侧栏压到 203，侧栏宽度会随页面跳动 */}
      <Sider
        className={`!bg-semi-color-bg-1 shrink-0 transition-[width] duration-200 ease-in-out ${collapsed ? 'w-[60px]' : 'w-[240px]'}`}
      >
        <div className="flex h-full min-h-0 flex-col">
          <div className="flex h-14 shrink-0 items-center gap-2 px-4">
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
          {/* 导航项多于视口高度时必须能滚：Semi 的 .semi-navigation 自带 overflow:hidden，
              交给外层容器滚动，logo 行保持固定；滚动条按 newapi classic 隐藏（.sider-scroll） */}
          <div className="sider-scroll min-h-0 flex-1 overflow-y-auto">
            <Nav
              selectedKeys={[selectedKey]}
              isCollapsed={collapsed}
              footer={null}
              className="!w-full !bg-transparent"
              onSelect={(data) => {
                const key = String(data.itemKey ?? '');
                if (key.startsWith('/')) {
                  navigate(key);
                }
              }}
            >
              {navItems.map((item) => (
                <Fragment key={item.path}>
                  {/* newapi classic 的分组标题：Semi Nav 会原样渲染非 Nav.Item 的子元素，
                      收起时整条隐藏（.nav-group-label 的规则在 styles.css） */}
                  {item.group && (
                    <li className="nav-group-label" aria-hidden="true">
                      {t(item.group)}
                    </li>
                  )}
                  <Nav.Item itemKey={item.path} text={t(item.navKey)} icon={item.icon} />
                </Fragment>
              ))}
            </Nav>
          </div>
          {/* newapi classic 的底部折叠条：展开时整宽按钮带文字，收起时只剩旋转后的箭头。
              收起态必须换 IconButton —— Button 没有 iconOnly 这个 prop，传进去会被
              Semi 原样 spread 到 DOM 上，React 每个页面报一次未知属性告警 */}
          <div className="flex shrink-0 justify-center px-3 py-3">
            {collapsed ? (
              <IconButton
                theme="outline"
                type="tertiary"
                size="small"
                aria-label={t('layout.expand')}
                onClick={() => setCollapsed((value) => !value)}
                icon={collapseIcon}
              />
            ) : (
              <Button
                theme="outline"
                type="tertiary"
                size="small"
                block
                aria-label={t('layout.collapse')}
                onClick={() => setCollapsed((value) => !value)}
                icon={collapseIcon}
              >
                {t('layout.collapse')}
              </Button>
            )}
          </div>
        </div>
      </Sider>

      <Layout className="h-full min-w-0 overflow-hidden">
        {/* 右列自身是唯一滚动容器，顶栏 sticky 悬在滚动口顶部，
            内容才会从玻璃头部底下滚过去（newapi classic 的毛玻璃顶栏效果）；
            设置页的 sticky 底栏也是挂在同一个滚动口上 */}
        <Layout className="h-full min-w-0 overflow-y-auto">
          {/* 玻璃顶栏的底色写在 styles.css 里：Tailwind 的 !important 在 @layer 里反而
              输给层外规则（层序对 important 是反的），暗色变体在这里不可靠 */}
          <Header className="sticky top-0 z-50 flex h-14 shrink-0 items-center gap-3 px-4 backdrop-blur-[20px]">
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

        {/* 窄屏下固定列（操作列宽 300）比整个可视区还宽，会把其它列全盖住；
            这里取消 Semi 固定列的 sticky，让它跟着 .semi-table-body 一起横向滚。
            semi.min.css 是未分层引入的，优先级高于 @layer 里的工具类，必须用 ! 才能盖住 position:sticky */}
          <Content className="min-h-0 bg-semi-color-bg-0 p-4 max-md:[&_[class*=cell-fixed]]:!static max-md:[&_[class*=cell-fixed]]:!shadow-none">
            <Outlet />
          </Content>
        </Layout>
      </Layout>
    </Layout>
  );
}
