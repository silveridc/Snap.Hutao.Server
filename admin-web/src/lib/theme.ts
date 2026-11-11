import { useSyncExternalStore } from 'react';

export type ThemeMode = 'light' | 'dark' | 'auto';

const THEME_KEY = 'hutao-admin-theme';
const THEME_EVENT = 'hutao-admin:theme-changed';

function initialMode(): ThemeMode {
  try {
    const stored = localStorage.getItem(THEME_KEY);
    if (stored === 'light' || stored === 'dark' || stored === 'auto') {
      return stored;
    }
  } catch {
    /* ignore */
  }
  return 'auto';
}

let mode = initialMode();

function effectiveTheme(current: ThemeMode): 'light' | 'dark' {
  if (current !== 'auto') {
    return current;
  }
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

export function applyTheme(): void {
  const theme = effectiveTheme(mode);
  document.documentElement.classList.toggle('dark', theme === 'dark');
  document.body.setAttribute('theme-mode', theme);
}

export function getThemeMode(): ThemeMode {
  return mode;
}

export function setThemeMode(next: ThemeMode): void {
  mode = next;
  try {
    localStorage.setItem(THEME_KEY, next);
  } catch {
    /* ignore */
  }
  applyTheme();
  window.dispatchEvent(new CustomEvent(THEME_EVENT));
}

function subscribe(callback: () => void): () => void {
  window.addEventListener(THEME_EVENT, callback);
  const media = window.matchMedia('(prefers-color-scheme: dark)');
  media.addEventListener('change', callback);
  return () => {
    window.removeEventListener(THEME_EVENT, callback);
    media.removeEventListener('change', callback);
  };
}

// 快照值必须是稳定的布尔，auto 模式下跟随系统变化
function useResolvedTheme(): boolean {
  return useSyncExternalStore(
    subscribe,
    () => effectiveTheme(getThemeMode()) === 'dark',
    () => false,
  );
}

export function useTheme(): { dark: boolean; mode: ThemeMode; setMode(next: ThemeMode): void } {
  const dark = useResolvedTheme();
  return { dark, mode, setMode: setThemeMode };
}
