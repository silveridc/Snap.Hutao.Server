import { useState } from 'react';
import { Button, Input, Modal, Select, Tag, Toast } from '@douyinfe/semi-ui';
import { IconSearch, IconPlus, IconRefresh, IconGlobe } from '@douyinfe/semi-icons';
import type { DocEntry } from '../lib/config';

interface Props {
  title: string;
  copyright: string;
  specVersion: string;
  docs: DocEntry[];
  /** 来自 config.json 的条目，界面里不能移除 */
  baseDocs: DocEntry[];
  activeDoc: string;
  search: string;
  onSearch: (value: string) => void;
  onSwitch: (name: string) => void;
  onRefresh: () => void;
  onAddDoc: (entry: DocEntry) => void;
  onRemoveDoc: (url: string) => void;
}

export default function TopBar({
  title,
  copyright,
  specVersion,
  docs,
  baseDocs,
  activeDoc,
  search,
  onSearch,
  onSwitch,
  onRefresh,
  onAddDoc,
  onRemoveDoc,
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

  return (
    <header className="topbar">
      <div className="brand">
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
        <Select
          value={activeDoc}
          onChange={(value) => onSwitch(String(value))}
          style={{ minWidth: 170 }}
          optionList={docs.map((doc) => ({ label: doc.name, value: doc.name }))}
        />
        <Button theme="light" size="small" icon={<IconPlus />} onClick={() => setVisible(true)}>
          文档源
        </Button>
        <Button theme="light" size="small" icon={<IconRefresh />} onClick={onRefresh}>
          刷新
        </Button>
        {copyright && <span>{copyright}</span>}
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
          这里添加的只保存在本浏览器；标着 config.json 的条目来自部署目录的配置文件，要改文件才能移除。
        </p>
      </Modal>
    </header>
  );
}
