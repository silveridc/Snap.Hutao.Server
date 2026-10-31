import { useState, type ReactNode } from 'react';
import { Button, Dropdown, Input, Modal, Select, Tag, Toast } from '@douyinfe/semi-ui';
import {
  IconDesktop,
  IconIndentLeft,
  IconIndentRight,
  IconMoon,
  IconPlus,
  IconRefresh,
  IconSearch,
  IconSun,
  IconGlobe,
} from '@douyinfe/semi-icons';
import type { DocEntry } from '../lib/config';
import type { ThemeMode } from '../lib/theme';

interface Props {
  title: string;
  copyright: string;
  specVersion: string;
  docs: DocEntry[];
  /** 来自 config.json 的条目，界面里不能移除 */
  baseDocs: DocEntry[];
  activeDoc: string;
  search: string;
  collapsed: boolean;
  theme: ThemeMode;
  onSearch: (value: string) => void;
  onSwitch: (name: string) => void;
  onRefresh: () => void;
  onAddDoc: (entry: DocEntry) => void;
  onRemoveDoc: (url: string) => void;
  onToggleSider: () => void;
  onThemeChange: (mode: ThemeMode) => void;
}

const THEME_OPTIONS: { key: ThemeMode; label: string; description: string; icon: ReactNode }[] = [
  { key: 'light', label: '浅色模式', description: '始终使用浅色主题', icon: <IconSun /> },
  { key: 'dark', label: '深色模式', description: '始终使用深色主题', icon: <IconMoon /> },
  { key: 'auto', label: '自动模式', description: '跟随系统主题设置', icon: <IconDesktop /> },
];

export default function TopBar({
  title,
  copyright,
  specVersion,
  docs,
  baseDocs,
  activeDoc,
  search,
  collapsed,
  theme,
  onSearch,
  onSwitch,
  onRefresh,
  onAddDoc,
  onRemoveDoc,
  onToggleSider,
  onThemeChange,
}: Props) {
  const [visible, setVisible] = useState(false);
  const [name, setName] = useState('');
  const [url, setUrl] = useState('');

  function submit() {
    const cleanUrl = url.trim();
    if (cleanUrl === '') {
      Toast.warning('请填写 swagger.json 地址');
      return;
    }
    onAddDoc({ name: name.trim() || cleanUrl.split('/').slice(-2).join('/'), url: cleanUrl });
    setName('');
    setUrl('');
    Toast.success('已添加文档源，会记住在本浏览器');
  }

  const currentIcon = THEME_OPTIONS.find((item) => item.key === theme)?.icon ?? <IconDesktop />;

  return (
    <header className="topbar">
      <div className="brand">
        <Button
          className="icon-button"
          theme="borderless"
          type="tertiary"
          aria-label={collapsed ? '展开目录' : '收起目录'}
          icon={collapsed ? <IconIndentRight /> : <IconIndentLeft />}
          onClick={onToggleSider}
        />
        <span className="brand-logo">
          <IconGlobe />
        </span>
        <span className="brand-text">
          <b>{title}</b>
          <span>
            SEMI SWAGGER DOC{specVersion ? ` · OPENAPI ${specVersion}` : ''}
          </span>
        </span>
      </div>

      <div className="topbar-mid">
        <div className="topbar-search">
          <Input
            prefix={<IconSearch />}
            placeholder="搜索接口名 / 路径 / 说明，Ctrl + K"
            value={search}
            onChange={(value) => onSearch(String(value))}
            showClear
            style={{ width: '100%' }}
          />
        </div>
      </div>

      <div className="topbar-right">
        {copyright && <span>{copyright}</span>}
        <Select
          value={activeDoc}
          onChange={(value) => onSwitch(String(value))}
          style={{ minWidth: 160 }}
          optionList={docs.map((doc) => ({ label: doc.name, value: doc.name }))}
        />
        <Button theme="light" size="small" icon={<IconPlus />} onClick={() => setVisible(true)}>
          文档源
        </Button>
        <Button
          className="icon-button"
          theme="borderless"
          type="tertiary"
          aria-label="重新拉取文档"
          icon={<IconRefresh />}
          onClick={onRefresh}
        />
        <Dropdown
          position="bottomRight"
          render={
            <Dropdown.Menu>
              {THEME_OPTIONS.map((option) => (
                <Dropdown.Item
                  key={option.key}
                  icon={option.icon}
                  onClick={() => onThemeChange(option.key)}
                  style={
                    theme === option.key
                      ? { background: 'var(--semi-color-primary-light-default)', fontWeight: 600 }
                      : undefined
                  }
                >
                  <div>
                    <div>{option.label}</div>
                    <div className="dim" style={{ fontSize: 12 }}>
                      {option.description}
                    </div>
                  </div>
                </Dropdown.Item>
              ))}
            </Dropdown.Menu>
          }
        >
          <span style={{ display: 'inline-flex' }}>
            <Button
              className="icon-button"
              theme="borderless"
              type="tertiary"
              aria-label="切换主题"
              icon={currentIcon}
            />
          </span>
        </Dropdown>
      </div>

      <Modal
        title="文档源"
        visible={visible}
        onCancel={() => setVisible(false)}
        footer={
          <div style={{ display: 'flex', gap: 10, justifyContent: 'flex-end' }}>
            <Button onClick={() => setVisible(false)}>关闭</Button>
            <Button theme="solid" type="primary" onClick={submit}>
              添加
            </Button>
          </div>
        }
      >
        <div className="doc-list">
          {docs.map((doc) => (
            <div className="doc-row" key={doc.url}>
              <div>
                <b>{doc.name}</b>
                <div className="dim" style={{ fontSize: 12 }}>
                  {doc.url}
                </div>
              </div>
              {baseDocs.some((d) => d.url === doc.url) ? (
                <Tag size="small" color="grey" type="light">
                  config.json
                </Tag>
              ) : (
                <Button size="small" theme="borderless" type="tertiary" onClick={() => onRemoveDoc(doc.url)}>
                  移除
                </Button>
              )}
            </div>
          ))}
        </div>

        <div className="section-title" style={{ marginTop: 16, fontSize: 13 }}>
          添加
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          <Input placeholder="显示名称，例如 账户" value={name} onChange={setName} />
          <Input
            placeholder="swagger.json 地址，例如 /swagger/Passport/swagger.json"
            value={url}
            onChange={setUrl}
          />
        </div>
        <p className="dim" style={{ fontSize: 12, marginTop: 10 }}>
          这里添加的只保存在本浏览器；标着 config.json 的条目来自部署目录的配置文件，要改文件才能移除.
        </p>
      </Modal>
    </header>
  );
}
