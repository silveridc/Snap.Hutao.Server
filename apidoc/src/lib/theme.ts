export type ThemeMode = 'light' | 'dark' | 'auto';

const KEY = 'semi-swagger-doc:theme';

export function systemIsDark(): boolean {
  return window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
}

export function loadThemeMode(): ThemeMode {
  const stored = localStorage.getItem(KEY);
  return stored === 'light' || stored === 'dark' || stored === 'auto' ? stored : 'auto';
}

export function saveThemeMode(mode: ThemeMode): void {
  try {
    localStorage.setItem(KEY, mode);
  } catch {
    /* 隐私模式下写不进 localStorage，保持本次会话生效即可 */
  }
}

/** Semi 的深色变量挂在 body[theme-mode=dark]，html.dark 供本站自定义样式跟随 */
export function applyTheme(mode: ThemeMode): void {
  const dark = mode === 'dark' || (mode === 'auto' && systemIsDark());
  document.documentElement.classList.toggle('dark', dark);
  if (dark) {
    document.body.setAttribute('theme-mode', 'dark');
  } else {
    document.body.removeAttribute('theme-mode');
  }
}
