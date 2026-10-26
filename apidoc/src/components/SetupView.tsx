import { useState } from 'react';
import { Button, Input, Toast } from '@douyinfe/semi-ui';
import { IconGlobe, IconPlus } from '@douyinfe/semi-icons';
import type { DocEntry } from '../lib/config';

const EXAMPLE = `{
  "title": "接口文档",
  "docs": [
    { "name": "PublicApi", "url": "/swagger/Public/swagger.json" },
    { "name": "账户", "url": "/swagger/Passport/swagger.json" }
  ]
}`;

export default function SetupView({ onAdd }: { onAdd: (entry: DocEntry) => void }) {
  const [name, setName] = useState('');
  const [url, setUrl] = useState('');

  function submit() {
    const cleanUrl = url.trim();
    if (cleanUrl === '') {
      Toast.warning('请填写 swagger.json 地址');
      return;
    }
    onAdd({ name: name.trim() || cleanUrl.split('/').slice(-2).join('/'), url: cleanUrl });
  }

  return (
    <div className="setup">
      <div className="card card-hero">
        <div className="op-title">
          <IconGlobe /> 还没有文档源
        </div>
        <div className="op-desc">
          添加一个 <code>swagger.json</code> / <code>openapi.json</code> 地址即可开始浏览。
          这里填写的地址只保存在当前浏览器，刷新后仍然可用。
        </div>
      </div>

      <div className="card">
        <div className="section-title">添加文档源</div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
          <Input placeholder="显示名称，例如 账户（留空则自动取路径）" value={name} onChange={setName} />
          <Input
            placeholder="地址，例如 /swagger/Passport/swagger.json 或 https://example.com/swagger/Public/swagger.json"
            value={url}
            onChange={setUrl}
          />
          <div>
            <Button theme="solid" type="primary" icon={<IconPlus />} onClick={submit}>
              添加
            </Button>
          </div>
        </div>
      </div>

      <div className="card">
        <div className="section-title">团队共享</div>
        <div className="op-desc">
          把 <code>config.json</code> 放在站点根目录（构建产物目录），所有访问者都会看到同一批文档源；
          也支持 <code>?url=</code> 单链接直接进入，或 <code>?config=</code> 指定另一份配置。
        </div>
        <pre className="code">{EXAMPLE}</pre>
      </div>
    </div>
  );
}
