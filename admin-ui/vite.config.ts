import path from 'node:path';

import { sentryVitePlugin } from '@sentry/vite-plugin';
import vue from '@vitejs/plugin-vue';
import vueJsx from '@vitejs/plugin-vue-jsx';
import type { ConfigEnv, UserConfig } from 'vite';
import svgLoader from 'vite-svg-loader';

const CWD = process.cwd();

// 后端接口前缀，开发期统一代理到线上服务
const API_PREFIXES = ['/Passport', '/Service', '/Admin', '/Redeem', '/Accession', '/patch', '/wallpaper', '/Announcement', '/Enka'];
const PROXY_TARGET = 'https://localhost:7013';

// 自建 Sentry；三个变量齐全才上传 sourcemap，见 AGENTS.md
const SENTRY_URL = 'https://sentry.silveridc.cn';
const sentryAuthToken = process.env.SENTRY_AUTH_TOKEN;
const sentryOrg = process.env.SENTRY_ORG;
const sentryProject = process.env.SENTRY_PROJECT;

// https://vitejs.dev/config/
export default (_: ConfigEnv): UserConfig => {
  return {
    base: '/admin/',
    resolve: {
      alias: {
        '@': path.resolve(CWD, './src'),
      },
    },

    css: {
      preprocessorOptions: {
        less: {
          modifyVars: {
            hack: `true; @import (reference) "${path.resolve('src/style/variables.less')}";`,
          },
          math: 'strict',
          javascriptEnabled: true,
        },
      },
    },

    plugins: [
      vue(),
      vueJsx(),
      svgLoader(),
      ...(sentryAuthToken && sentryOrg && sentryProject
        ? [
            sentryVitePlugin({
              url: SENTRY_URL,
              authToken: sentryAuthToken,
              org: sentryOrg,
              project: sentryProject,
              telemetry: false,
              // 上传后删除，避免 /admin/assets/*.map 被公开下载
              sourcemaps: { filesToDeleteAfterUpload: ['./**/*.map'] },
            }),
          ]
        : []),
    ],

    server: {
      port: 5173,
      host: '0.0.0.0',
      allowedHosts: true,
      proxy: Object.fromEntries(
        API_PREFIXES.map((p) => [p, { target: PROXY_TARGET, changeOrigin: true, secure: false }]),
      ),
    },

    build: {
      // hidden：产物带 .map 但不在 js 末尾引用（配合 Sentry 上传用）
      sourcemap: 'hidden',
      outDir: path.resolve(CWD, '../server/wwwroot/admin'),
      emptyOutDir: true,
      chunkSizeWarningLimit: 2000,
      rolldownOptions: {
        onLog(level, log, defaultHandler) {
          if (log.code === 'INVALID_ANNOTATION') return null;
          return defaultHandler(level, log);
        },
      },
    },
  };
};
