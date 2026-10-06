import path from 'node:path';

import vue from '@vitejs/plugin-vue';
import vueJsx from '@vitejs/plugin-vue-jsx';
import type { ConfigEnv, UserConfig } from 'vite';
import svgLoader from 'vite-svg-loader';

const CWD = process.cwd();

// 后端接口前缀，开发期统一代理到线上服务
const API_PREFIXES = ['/Passport', '/Service', '/Redeem', '/Accession', '/patch', '/wallpaper', '/Announcement', '/Enka'];
const PROXY_TARGET = 'https://localhost:7013';

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

    plugins: [vue(), vueJsx(), svgLoader()],

    server: {
      port: 5173,
      host: '0.0.0.0',
      allowedHosts: true,
      proxy: Object.fromEntries(
        API_PREFIXES.map((p) => [p, { target: PROXY_TARGET, changeOrigin: true, secure: false }]),
      ),
    },

    build: {
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
