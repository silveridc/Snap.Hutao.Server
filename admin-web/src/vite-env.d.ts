/// <reference types="vite/client" />

// vite.config.ts 的 define 注入，与服务端 server@vX.Y.Z 同格式的 Sentry release 标识
declare const __SENTRY_RELEASE__: string;
