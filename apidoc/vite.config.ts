import fs from 'node:fs';
import path from 'node:path';
import { defineConfig, loadEnv, type ProxyOptions } from 'vite';
import react from '@vitejs/plugin-react';

const CWD = process.cwd();

interface RuntimeConfig {
  docs?: { url?: string }[];
}

/**
 * 开发代理的目标站点。swagger.json 通常不带 CORS 头，本地直接跨域拉取会被浏览器拦截，
 * 所以按 config.json 里的相对路径前缀自动建代理规则，生产环境由 ASP.NET 同源托管。
 */
function readDocsPrefixes(root: string): string[] {
  const file = path.join(root, 'public', 'config.json');
  if (!fs.existsSync(file)) {
    return [];
  }
  const config = JSON.parse(fs.readFileSync(file, 'utf8')) as RuntimeConfig;
  const prefixes = new Set<string>();
  for (const doc of config.docs ?? []) {
    const url = doc.url ?? '';
    if (/^https?:\/\//i.test(url)) {
      continue;
    }
    const first = url.replace(/^\/+/, '').split('/')[0];
    if (first) {
      prefixes.add('/' + first);
    }
  }
  return [...prefixes];
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, CWD, '');
  const target = env.VITE_PROXY_TARGET || 'https://localhost:7013';

  const extra = (env.VITE_PROXY_PATHS ?? '/swagger,/api')
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
    .map((item) => (item.startsWith('/') ? item : `/${item}`));

  const proxy: Record<string, ProxyOptions> = {};
  for (const prefix of new Set([...readDocsPrefixes(CWD), ...extra])) {
    proxy[prefix] = { target, changeOrigin: true, secure: false };
  }

  return {
    base: env.VITE_BASE || '/doc/',
    plugins: [react()],
    server: {
      port: Number(env.VITE_PORT) || 5180,
      host: '0.0.0.0',
      allowedHosts: true,
      proxy,
    },
    build: {
      outDir: path.resolve(CWD, '../server/wwwroot/doc'),
      emptyOutDir: true,
      sourcemap: false,
      chunkSizeWarningLimit: 1200,
    },
  };
});
