import { useEffect, useMemo, useState } from 'react';
import { Collapse, Input } from '@douyinfe/semi-ui';
import { IconSearch } from '@douyinfe/semi-icons';
import type { ModelDef, RawDocument } from '../openapi/types';
import { buildExample, stringifyExample } from '../openapi/example';
import FieldTable from './FieldTable';
import CodeBlock from './CodeBlock';

interface Props {
  models: ModelDef[];
  raw: RawDocument;
  focus: string;
}

export default function SchemasView({ models, raw, focus }: Props) {
  const [keyword, setKeyword] = useState('');
  const [open, setOpen] = useState<string[]>(focus ? [focus] : []);

  useEffect(() => {
    if (focus) {
      setOpen((prev) => (prev.includes(focus) ? prev : [...prev, focus]));
    }
  }, [focus]);

  const list = useMemo(() => {
    const key = keyword.trim().toLowerCase();
    if (key === '') {
      return models;
    }
    return models.filter(
      (m) => m.name.toLowerCase().includes(key) || m.fields.some((f) => f.label.toLowerCase().includes(key)),
    );
  }, [models, keyword]);

  return (
    <>
      <div className="card">
        <div className="section-title">
          数据模型
          <span className="count">{models.length}</span>
        </div>
        <Input prefix={<IconSearch />} placeholder="按模型名或字段名过滤" showClear style={{ width: 320 }} onChange={setKeyword} />
        {list.length === 0 && <div className="empty">没有匹配的模型</div>}
      </div>

      {list.length > 0 && (
        <div className="card">
          <Collapse activeKey={open} onChange={(keys) => setOpen((Array.isArray(keys) ? keys : [keys]).map(String))}>
            {list.map((model) => (
              <Collapse.Panel
                key={model.name}
                itemKey={model.name}
                header={
                  <span className="field-name">
                    {model.name} <span className="type-tag">{model.type}</span>
                    {model.usedBy.length > 0 && <span className="dim"> · 被 {model.usedBy.length} 个接口使用</span>}
                  </span>
                }
              >
                {model.description && <div className="op-desc">{model.description}</div>}
                {model.enumValues.length > 0 && (
                  <div style={{ marginBottom: 10 }}>
                    {model.enumValues.map((v) => (
                      <span className="enum-tag" key={v}>
                        {v}
                      </span>
                    ))}
                  </div>
                )}
                {model.fields.length > 0 ? (
                  <FieldTable fields={model.fields} />
                ) : (
                  <div className="empty">没有字段</div>
                )}
                <div style={{ marginTop: 12 }}>
                  <div className="section-title" style={{ fontSize: 13 }}>
                    JSON 示例
                  </div>
                  <CodeBlock text={stringifyExample(buildExample({ type: 'object', ...raw.components?.schemas?.[model.name] }, raw))} maxHeight={300} />
                </div>
              </Collapse.Panel>
            ))}
          </Collapse>
        </div>
      )}
    </>
  );
}
