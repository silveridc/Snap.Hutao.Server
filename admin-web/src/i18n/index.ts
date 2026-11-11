import { useSyncExternalStore } from 'react';

export const supportedLocales = ['zh_CN', 'en_US'] as const;
export type SupportedLocale = (typeof supportedLocales)[number];

export const LOCALE_STORAGE_KEY = 'hutao-admin-locale';
export const LOCALE_CHANGED_EVENT = 'hutao-admin:locale-changed';

// 每个文件顶层自带命名空间（如 { "home": { ... } }），合并后词条路径就是 home.xxx
type Dict = Record<string, unknown>;

const modules = import.meta.glob<{ default: Dict }>('./*/*.json', { eager: true, import: 'default' });

const dictionaries: Record<string, Dict> = {};
for (const [path, content] of Object.entries(modules)) {
  const matched = /^\.\/([^/]+)\/[^/]+\.json$/.exec(path);
  const locale = matched?.[1];
  if (!locale || !supportedLocales.includes(locale as SupportedLocale)) {
    continue;
  }
  dictionaries[locale] = { ...dictionaries[locale], ...content };
}

const FALLBACK: SupportedLocale = 'zh_CN';

function initialLocale(): SupportedLocale {
  try {
    const stored = localStorage.getItem(LOCALE_STORAGE_KEY)?.replace(/^"|"$/g, '');
    if (stored && supportedLocales.includes(stored as SupportedLocale)) {
      return stored as SupportedLocale;
    }
  } catch {
    /* ignore */
  }
  const preferred = navigator.languages?.[0]?.replace(/-/g, '_');
  if (preferred && supportedLocales.includes(preferred as SupportedLocale)) {
    return preferred as SupportedLocale;
  }
  return FALLBACK;
}

let current: SupportedLocale = initialLocale();

export function getLocale(): SupportedLocale {
  return current;
}

export function setLocale(locale: SupportedLocale): void {
  current = locale;
  try {
    localStorage.setItem(LOCALE_STORAGE_KEY, locale);
  } catch {
    /* ignore */
  }
  document.documentElement.setAttribute('lang', locale === 'zh_CN' ? 'zh-CN' : 'en');
  window.dispatchEvent(new CustomEvent(LOCALE_CHANGED_EVENT));
}

function subscribe(callback: () => void): () => void {
  window.addEventListener(LOCALE_CHANGED_EVENT, callback);
  return () => window.removeEventListener(LOCALE_CHANGED_EVENT, callback);
}

export function useLocale(): SupportedLocale {
  return useSyncExternalStore(subscribe, getLocale, getLocale);
}

export function useText(): (key: string, vars?: Record<string, string | number>) => string {
  useLocale();
  return t;
}

function lookup(locale: SupportedLocale, path: string): string | undefined {
  let node: unknown = dictionaries[locale];
  for (const segment of path.split('.')) {
    if (typeof node !== 'object' || node === null) {
      return undefined;
    }
    node = (node as Dict)[segment];
  }
  return typeof node === 'string' ? node : undefined;
}

export function te(key: string): boolean {
  return lookup(current, key) !== undefined || lookup(FALLBACK, key) !== undefined;
}

// 变量写法与 vue-i18n 一致：{name}
function interpolate(template: string, vars?: Record<string, string | number>): string {
  if (!vars) {
    return template;
  }
  return template.replace(/\{(\w+)\}/g, (raw, name: string) => {
    const value = vars[name];
    return value === undefined || value === null ? raw : String(value);
  });
}

export function t(key: string, vars?: Record<string, string | number>): string {
  const template = lookup(current, key) ?? lookup(FALLBACK, key);
  if (template === undefined) {
    return key;
  }
  return interpolate(template, vars);
}

export const languageNames: Record<SupportedLocale, string> = {
  zh_CN: '简体中文',
  en_US: 'English',
};
