import { execSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import path from 'node:path';

import { sentryVitePlugin } from '@sentry/vite-plugin';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

const API_TARGET = process.env.VITE_DEV_API_TARGET ?? 'https://localhost:7013';
const SENTRY_URL = 'https://sentry.silveridc.cn';
const sentryAuthToken = process.env.SENTRY_AUTH_TOKEN;
const sentryOrg = process.env.SENTRY_ORG;
const sentryProject = process.env.SENTRY_PROJECT;

function gitCommit() {
  try {
    return execSync('git rev-parse HEAD', { encoding: 'utf8' }).trim();
  } catch {
    return 'local';
  }
}

// 服务端 release 是 Program.cs 里的 server@vX.Y.Z，前端同写法。带上 commit 是因为
// 同版本号会被反复构建，不带的话后一次的 sourcemap 会覆盖前一次，线上事件就配错符号
const packageVersion = (JSON.parse(readFileSync(path.resolve(process.cwd(), 'package.json'), 'utf8')) as { version: string }).version;
const sentryRelease = `admin-web@${packageVersion}+${(process.env.GITHUB_SHA ?? gitCommit()).slice(0, 10)}`;

export default defineConfig({
  base: '/admin/',
  define: {
    __SENTRY_RELEASE__: JSON.stringify(sentryRelease),
  },
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
            release: { name: sentryRelease },
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
