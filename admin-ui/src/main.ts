/* eslint-disable simple-import-sort/imports */
import * as Sentry from '@sentry/vue';
import { createApp } from 'vue';
import TDesign from 'tdesign-vue-next';

import App from './App.vue';
import router from './router';
import { store } from './store';
import i18n from './locales';

import 'tdesign-vue-next/es/style/index.css';
import '@/style/index.less';
import './permission';

const app = createApp(App);

// 后台前端与 Snap.Hutao.Server 共用同一个 Sentry 项目，用 service 标签区分
if (import.meta.env.VITE_SENTRY_DSN) {
  Sentry.init({
    app,
    dsn: import.meta.env.VITE_SENTRY_DSN,
    environment: import.meta.env.MODE,
    integrations: [Sentry.browserTracingIntegration({ router })],
    tracesSampleRate: 0.2,
    initialScope: { tags: { service: 'admin-ui' } },
  });
}

app.use(TDesign);
app.use(store);
app.use(router);
app.use(i18n);

app.mount('#app');
