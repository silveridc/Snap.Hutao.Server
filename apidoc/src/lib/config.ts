export interface DocEntry {
  /** 目录里显示的名字 */
  name: string;
  /** swagger.json 地址，相对路径按当前站点解析 */
  url: string;
}

export interface RuntimeConfig {
  title: string;
  copyright: string;
  docs: DocEntry[];
  /** 默认选中的文档名，找不到时用第一个 */
  defaultDoc: string;
  /** 给所有相对地址统一加前缀，部署到子目录时用 */
  base: string;
}

const EXTRA_KEY = 'semi-swagger-doc:docs';

const EMPTY: RuntimeConfig = { title: 'API接口文档', copyright: '', docs: [], defaultDoc: '', base: '' };

function text(value: unknown, fallback = ''): string {
  return typeof value === 'string' && value.trim() !== '' ? value.trim() : fallback;
}

function normalizeDocs(input: unknown): DocEntry[] {
  if (!Array.isArray(input)) {
    return [];
  }
  const out: DocEntry[] = [];
  for (const item of input) {
    if (typeof item === 'string') {
      out.push({ name: text(item, '文档'), url: item });
      continue;
    }
    if (!item || typeof item !== 'object') {
      continue;
    }
    const record = item as Record<string, unknown>;
    const url = text(record.url ?? record.url30 ?? record.swaggerUrl);
    if (!url) {
      continue;
    }
    out.push({ name: text(record.name ?? record.title, url.split('/').slice(-2).join('/')), url });
  }
  return out;
}

/** 浏览器地址栏参数优先于配置文件，方便临时看别人的 swagger 而不改部署 */
function applyQuery(config: RuntimeConfig, params: URLSearchParams): RuntimeConfig {
  const next = { ...config, docs: [...config.docs] };
  const single = text(params.get('url'));
  if (single) {
    next.docs = [{ name: text(params.get('name'), '临时文档'), url: single }];
    next.defaultDoc = next.docs[0].name;
  }
  const base = text(params.get('base'));
  if (base) {
    next.base = base;
  }
  const title = text(params.get('title'));
  if (title) {
    next.title = title;
  }
  return next;
}

export function loadExtraDocs(): DocEntry[] {
  try {
    const raw = localStorage.getItem(EXTRA_KEY);
    return raw ? normalizeDocs(JSON.parse(raw)) : [];
  } catch {
    return [];
  }
}

export function saveExtraDocs(docs: DocEntry[]): void {
  try {
    localStorage.setItem(EXTRA_KEY, JSON.stringify(docs));
  } catch {
    /* 隐私模式下写不进 localStorage，忽略即可 */
  }
}

export function docUrl(url: string, base: string): string {
  if (/^https?:\/\//i.test(url)) {
    return url;
  }
  const clean = url.replace(/^\/+/, '');
  if (!base) {
    return '/' + clean;
  }
  return base.replace(/\/+$/, '') + '/' + clean;
}

export interface LoadedConfig {
  config: RuntimeConfig;
  /** config.json / query 带来的文档源，不含浏览器本地添加的 */
  baseDocs: DocEntry[];
}

export async function loadRuntimeConfig(): Promise<LoadedConfig> {
  const params = new URLSearchParams(window.location.search);
  const configUrl = text(params.get('config'), 'config.json');

  let config: RuntimeConfig = { ...EMPTY };
  try {
    const res = await fetch(configUrl, { cache: 'no-store' });
    if (res.ok) {
      const body = (await res.json()) as Record<string, unknown>;
      config = {
        title: text(body.title, EMPTY.title),
        copyright: text(body.copyright, ''),
        docs: normalizeDocs(body.docs),
        defaultDoc: text(body.defaultDoc),
        base: text(body.base),
      };
    }
  } catch {
    /* 没有 config.json 时走「添加文档源」流程 */
  }

  const overridden = applyQuery({ ...config, docs: [...config.docs] }, params);
  const extra = text(params.get('url')) ? [] : loadExtraDocs();
  const merged = [...overridden.docs, ...extra.filter((e) => !overridden.docs.some((d) => d.url === e.url))];

  return { config: { ...overridden, docs: merged }, baseDocs: overridden.docs };
}
