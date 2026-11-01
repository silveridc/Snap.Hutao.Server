import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router';
import type { ReactNode } from 'react';

import * as Sentry from '@sentry/react';
import { LocaleProvider } from '@douyinfe/semi-ui';
import enUS from '@douyinfe/semi-ui/lib/es/locale/source/en_US';
import zhCN from '@douyinfe/semi-ui/lib/es/locale/source/zh_CN';

import App from '@/App';
import { getLocale, useLocale } from '@/i18n';
import { applyTheme } from '@/lib/theme';

import '@douyinfe/semi-icons/dist/css/semi-icons.min.css';
// semi-ui 的 exports 字段只放开 lib/es/**，dist/css 只能按文件路径引用
import '../node_modules/@douyinfe/semi-ui/dist/css/semi.min.css';
import '@/styles.css';

applyTheme();

// 后台前端与 Snap.Hutao.Server 共用同一个 Sentry 项目，用 service 标签区分
const sentryDsn = import.meta.env.VITE_SENTRY_DSN;
if (sentryDsn) {
  Sentry.init({
    dsn: sentryDsn,
    environment: import.meta.env.MODE,
    integrations: [Sentry.browserTracingIntegration()],
    tracesSampleRate: 0.2,
    initialScope: { tags: { service: 'admin-web' } },
  });
}

function SemiLocale({ children }: { children: ReactNode }) {
  const locale = useLocale();
  return <LocaleProvider locale={locale === 'zh_CN' ? zhCN : enUS}>{children}</LocaleProvider>;
}

const container = document.getElementById('root');
if (!container) {
  throw new Error('Missing #root element');
}

createRoot(container).render(
  <SemiLocale>
    <BrowserRouter basename="/admin">
      <App />
    </BrowserRouter>
  </SemiLocale>,
);

document.documentElement.setAttribute('lang', getLocale() === 'zh_CN' ? 'zh-CN' : 'en');
