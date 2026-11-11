import { useCallback, useEffect, useMemo, useState } from 'react';
import { Banner, Radio, RadioGroup, Spin, Tabs, Tag } from '@douyinfe/semi-ui';
import { IconIndentLeft, IconIndentRight } from '@douyinfe/semi-icons';
import type { DocEntry } from './lib/config';
import { docUrl, loadExtraDocs, loadRuntimeConfig, saveExtraDocs, type RuntimeConfig } from './lib/config';
import { applyTheme, loadThemeMode, saveThemeMode, type ThemeMode } from './lib/theme';
import { loadSpec } from './api/client';
import type { NormalizedDoc, Operation, RawDocument } from './openapi/types';
import OpTree from './components/OpTree';
import OpDetail from './components/OpDetail';
import SchemasView from './components/SchemasView';
import TopBar from './components/TopBar';
import SetupView from './components/SetupView';

const SIDER_KEY = 'semi-swagger-doc:sider';

interface LoadState {
  status: 'loading' | 'ready' | 'error';
  doc?: NormalizedDoc;
  raw?: RawDocument;
  error?: string;
}

const TAB_SEP = '|';
const tabKey = (docName: string, opId: string) => `${docName}${TAB_SEP}${opId}`;

export default function App() {
  const [config, setConfig] = useState<RuntimeConfig | null>(null);
  const [baseDocs, setBaseDocs] = useState<DocEntry[]>([]);
  const [activeDoc, setActiveDoc] = useState('');
  const [specs, setSpecs] = useState<Record<string, LoadState>>({});
  const [tabs, setTabs] = useState<string[]>([]);
  const [activeTab, setActiveTab] = useState('');
  const [view, setView] = useState<'ops' | 'models'>('ops');
  const [modelFocus, setModelFocus] = useState('');
  const [search, setSearch] = useState('');
  const [theme, setTheme] = useState<ThemeMode>(() => loadThemeMode());
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem(SIDER_KEY) === '1');

  useEffect(() => {
    applyTheme(theme);
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    const sync = () => applyTheme(theme);
    media.addEventListener('change', sync);
    return () => media.removeEventListener('change', sync);
  }, [theme]);

  useEffect(() => {
    document.body.classList.toggle('sidebar-collapsed', collapsed);
    localStorage.setItem(SIDER_KEY, collapsed ? '1' : '0');
  }, [collapsed]);

  function changeTheme(next: ThemeMode) {
    setTheme(next);
    saveThemeMode(next);
  }

  useEffect(() => {
    void loadRuntimeConfig().then(({ config: next, baseDocs: bases }) => {
      setConfig(next);
      setBaseDocs(bases);
      const stored = localStorage.getItem('semi-swagger-doc:last');
      const first =
        next.docs.find((d) => d.name === stored) ??
        next.docs.find((d) => d.name === next.defaultDoc) ??
        next.docs[0];
      if (first) {
        setActiveDoc(first.name);
      }
    });
  }, []);

  const entry = useMemo(() => config?.docs.find((d) => d.name === activeDoc), [config, activeDoc]);

  const fetchDoc = useCallback(async (item: DocEntry, base: string, force = false) => {
    setSpecs((prev) => ({ ...prev, [item.name]: { status: 'loading' } }));
    try {
      const { doc, raw } = await loadSpec(docUrl(item.url, base) + (force ? (item.url.includes('?') ? '&' : '?') + 't=' + Date.now() : ''));
      setSpecs((prev) => ({ ...prev, [item.name]: { status: 'ready', doc, raw } }));
    } catch (e) {
      setSpecs((prev) => ({
        ...prev,
        [item.name]: { status: 'error', error: e instanceof Error ? e.message : '加载失败' },
      }));
    }
  }, []);

  useEffect(() => {
    if (entry && config && !specs[entry.name]) {
      void fetchDoc(entry, config.base);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [entry, config]);

  useEffect(() => {
    if (activeDoc) {
      localStorage.setItem('semi-swagger-doc:last', activeDoc);
    }
  }, [activeDoc]);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        document.querySelector<HTMLInputElement>('.topbar-search input')?.focus();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  const state = specs[activeDoc];
  const doc = state?.doc;
  const raw = state?.raw;

  const openOperation = useCallback((op: Operation) => {
    const key = tabKey(activeDoc, op.id);
    setTabs((prev) => (prev.includes(key) ? prev : [...prev, key]));
    setActiveTab(key);
    setView('ops');
  }, [activeDoc]);

  const openModel = useCallback((name: string) => {
    setModelFocus(name);
    setView('models');
  }, []);

  const activeOp = useMemo(() => {
    if (!doc || activeTab === '' || !activeTab.startsWith(activeDoc + TAB_SEP)) {
      return null;
    }
    const id = activeTab.slice(activeDoc.length + TAB_SEP.length);
    return doc.operations.find((op) => op.id === id) ?? null;
  }, [doc, activeTab, activeDoc]);

  function addDoc(item: DocEntry) {
    if (!config) {
      return;
    }
    if (baseDocs.some((d) => d.url === item.url)) {
      setActiveDoc(baseDocs.find((d) => d.url === item.url)!.name);
      return;
    }
    const extras = [...loadExtraDocs().filter((d) => d.url !== item.url), item];
    saveExtraDocs(extras);
    setConfig({
      ...config,
      docs: [...baseDocs, ...extras.filter((e) => !baseDocs.some((d) => d.url === e.url))],
    });
    setActiveDoc(item.name);
    setSpecs((prev) => {
      const copy = { ...prev };
      delete copy[item.name];
      return copy;
    });
    setTabs([]);
    setActiveTab('');
  }

  function removeDoc(url: string) {
    if (!config) {
      return;
    }
    const target = config.docs.find((d) => d.url === url);
    if (!target) {
      return;
    }
    const extras = loadExtraDocs().filter((d) => d.url !== url);
    saveExtraDocs(extras);
    const next = [...baseDocs, ...extras.filter((e) => !baseDocs.some((d) => d.url === e.url))];
    setConfig({ ...config, docs: next });
    if (activeDoc === target.name) {
      setActiveDoc(next[0]?.name ?? '');
      setTabs([]);
      setActiveTab('');
    }
  }

  if (!config) {
    return (
      <div style={{ display: 'grid', placeItems: 'center', height: '100vh' }}>
        <Spin size="large" />
      </div>
    );
  }

  if (config.docs.length === 0) {
    return <SetupView onAdd={addDoc} />;
  }

  return (
    <>
      <TopBar
        title={config.title}
        copyright={config.copyright}
        specVersion={doc?.meta.specVersion ?? ''}
        docs={config.docs}
        baseDocs={baseDocs}
        activeDoc={activeDoc}
        search={search}
        collapsed={collapsed}
        theme={theme}
        onSearch={setSearch}
        onSwitch={(name) => {
          setActiveDoc(name);
          setTabs([]);
          setActiveTab('');
          setView('ops');
        }}
        onRefresh={() => entry && void fetchDoc(entry, config.base, true)}
        onAddDoc={addDoc}
        onRemoveDoc={removeDoc}
        onToggleSider={() => setCollapsed((prev) => !prev)}
        onThemeChange={changeTheme}
      />

      <div className="layout">
        <aside className="sidebar">
          <div className="sidebar-head">
            <RadioGroup
              type="button"
              buttonSize="small"
              value={view}
              onChange={(e) => setView(String(e.target.value) === 'models' ? 'models' : 'ops')}
            >
              <Radio value="ops">接口 {doc ? doc.operations.length : 0}</Radio>
              <Radio value="models">模型 {doc ? doc.models.length : 0}</Radio>
            </RadioGroup>
          </div>

          <div className="sidebar-body">
            {state?.status === 'loading' && (
              <div style={{ display: 'grid', placeItems: 'center', padding: 40 }}>
                <Spin />
              </div>
            )}

            {state?.status === 'error' && (
              <Banner
                type="danger"
                description={state.error}
                fullMode={false}
                style={{ margin: 8 }}
                bordered
              />
            )}

            {doc && view === 'ops' && (
              <OpTree doc={doc} activeId={activeOp?.id ?? ''} filter={search} onSelect={openOperation} />
            )}

            {doc && view === 'models' && (
              <div>
                <div className="sidebar-group-label">Schema {doc.models.length}</div>
                {doc.models.map((model) => (
                  <div className="search-item" key={model.name} onClick={() => openModel(model.name)}>
                    <span className="field-name">{model.name}</span>
                    <span className="dim" style={{ marginLeft: 6, fontSize: 11 }}>
                      {model.type}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="sidebar-collapse" onClick={() => setCollapsed((prev) => !prev)}>
            <span className="icon-button" style={{ display: 'inline-flex' }}>
              {collapsed ? <IconIndentRight /> : <IconIndentLeft />}
            </span>
          </div>
        </aside>

        <main className="content">
          {tabs.length > 0 && (
            <Tabs
              className="tabbar"
              type="card"
              activeKey={activeTab}
              collapsible={false}
              onTabClick={(key) => {
                setActiveTab(String(key));
                setView('ops');
              }}
              onTabClose={(key) => {
                const next = tabs.filter((t) => t !== key);
                setTabs(next);
                if (activeTab === key) {
                  setActiveTab(next[next.length - 1] ?? '');
                }
              }}
              tabList={tabs.map((key) => {
                const [docName, id] = key.split(TAB_SEP);
                const cached = specs[docName]?.doc;
                const op = cached?.operations.find((item) => item.id === id);
                return {
                  itemKey: key,
                  tab: (
                    <span>
                      <Tag size="small" color={op?.method === 'GET' ? 'blue' : 'green'} type="light">
                        {op?.method ?? '?'}
                      </Tag>{' '}
                      {op?.summary ?? id}
                    </span>
                  ),
                  closable: true,
                };
              })}
            />
          )}

          {state?.status === 'error' && (
            <div className="card">
              <div className="section-title">文档加载失败</div>
              <div className="op-desc">{state.error}</div>
              <p className="dim" style={{ fontSize: 12 }}>
                若这是跨域限制：把本站点部署到与接口同源（例如 ASP.NET 的 wwwroot/docs），或给 swagger.json 所在服务加 CORS 头.
              </p>
            </div>
          )}

          {doc && raw && view === 'models' && <SchemasView models={doc.models} raw={raw} focus={modelFocus} />}

          {doc && raw && view === 'ops' && activeOp && (
            <OpDetail
              key={`${activeDoc}-${activeOp.id}`}
              op={activeOp}
              doc={doc}
              raw={raw}
              docName={activeDoc}
              onOpenModel={openModel}
            />
          )}

          {doc && view === 'ops' && !activeOp && (
            <Overview doc={doc} onOpen={openOperation} />
          )}
        </main>
      </div>
    </>
  );
}

function Overview({
  doc,
  onOpen,
}: {
  doc: NormalizedDoc;
  onOpen: (op: Operation) => void;
}) {
  const tags = new Set(doc.operations.map((op) => op.tag));
  return (
    <>
      <div className="card card-hero">
        <div className="op-title">{doc.meta.title}</div>
        <div className="op-desc">{doc.meta.description}</div>
        <div className="stat-row">
          <div className="stat">
            <b>{tags.size}</b>
            <span>分组</span>
          </div>
          <div className="stat">
            <b>{doc.operations.length}</b>
            <span>接口</span>
          </div>
          <div className="stat">
            <b>{doc.models.length}</b>
            <span>模型</span>
          </div>
          {doc.meta.baseUrl && (
            <div className="stat">
              <b style={{ fontSize: 13, fontFamily: 'Consolas, monospace' }}>{doc.meta.baseUrl}</b>
              <span>server</span>
            </div>
          )}
        </div>
        {doc.warnings.length > 0 && (
          <Banner type="warning" style={{ marginTop: 14 }} description={doc.warnings.join('；')} />
        )}
      </div>

      {doc.groups.map((group) => (
        <div className="card" key={group.tag}>
          <div className="section-title">
            {group.tag}
            <span className="count">{group.operations.length}</span>
            {group.description && <span className="dim" style={{ fontWeight: 400 }}>{group.description}</span>}
          </div>
          <div className="overview-grid">
            {group.operations.map((op) => (
              <div className="overview-card" key={op.id} onClick={() => onOpen(op)}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span className={`mchip mchip-${op.method.toLowerCase()}`}>{op.method}</span>
                  {op.deprecated && <Tag size="small" color="red" type="light">废弃</Tag>}
                </div>
                <h4>{op.summary}</h4>
                <div className="tree-op-path">{op.path}</div>
                {op.params.length > 0 && (
                  <div className="dim" style={{ fontSize: 12, marginTop: 6 }}>
                    {op.params.length} 个参数
                    {op.requestContents.length > 0 ? ' · 有请求体' : ''}
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      ))}
    </>
  );
}
