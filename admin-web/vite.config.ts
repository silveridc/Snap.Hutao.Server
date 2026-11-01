import path from 'node:path';

import { sentryVitePlugin } from '@sentry/vite-plugin';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

const API_TARGET = process.env.VITE_DEV_API_TARGET ?? 'https://localhost:7013';
const SENTRY_URL = 'https://sentry.silveridc.cn';
const sentryAuthToken = process.env.SENTRY_AUTH_TOKEN;
const sentryOrg = process.env.SENTRY_ORG;
const sentryProject = process.env.SENTRY_PROJECT;

export default defineConfig({
  base: '/admin/',
  resolve: {
    alias: {
      '@': path.resolve(process.cwd(), './src'),
    },
  },
  plugins: [
    react(),
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
    host: '0.0.0.0',
    port: 5174,
    proxy: {
      '/api': {
        target: API_TARGET,
        changeOrigin: true,
        secure: false,
      },
    },
  },
  build: {
    outDir: path.resolve(process.cwd(), '../server/wwwroot/admin'),
    emptyOutDir: true,
    sourcemap: 'hidden',
  },
});
