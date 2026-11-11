import { useMemo, useState } from 'react';
import { Banner, Button, Table, Tabs, Tag } from '@douyinfe/semi-ui';
import { IconCopy } from '@douyinfe/semi-icons';
import type { ContentBlock, NormalizedDoc, Operation, RawDocument } from '../openapi/types';
import { buildExample, stringifyExample } from '../openapi/example';
import { copyText, MethodChip } from '../ui';
import FieldTable from './FieldTable';
import CodeBlock from './CodeBlock';
import TryPanel from './TryPanel';

interface Props {
  op: Operation;
  doc: NormalizedDoc;
  raw: RawDocument;
  docName: string;
  onOpenModel: (name: string) => void;
}

function Section({ title, count, children }: { title: string; count?: number; children: React.ReactNode }) {
  return (
    <div className="card">
      <div className="section-title">
        {title}
        {count !== undefined && count > 0 && <span className="count">{count}</span>}
      </div>
      {children}
    </div>
  );
}

function Contents({ blocks, raw, onOpenModel }: { blocks: ContentBlock[]; raw: RawDocument; onOpenModel: (name: string) => void }) {
  const [active, setActive] = useState(blocks[0]?.contentType ?? '');
  const current = blocks.find((b) => b.contentType === active) ?? blocks[0];
  if (!current) {
    return <div className="empty">无内容</div>;
  }

  return (
    <>
      {blocks.length > 1 && (
        <Tabs
          type="slash"
          activeKey={active || current.contentType}
          onChange={setActive}
          style={{ marginBottom: 12 }}
          tabList={blocks.map((block) => ({ itemKey: block.contentType, tab: block.contentType }))}
        />
      )}
      {current.fields.length > 0 ? (
        <FieldTable fields={current.fields} onOpenModel={onOpenModel} />
      ) : (
        <div className="empty">
          返回 <span className="type-tag">{current.schema?.type ?? '无 schema'}</span>，没有可展开的字段
        </div>
      )}
      <div style={{ marginTop: 12 }}>
        <div className="section-title" style={{ fontSize: 13 }}>
          {current.contentType} 示例
        </div>
        <CodeBlock text={stringifyExample(buildExample(current.schema ?? {}, raw))} />
      </div>
    </>
  );
}

export default function OpDetail({ op, doc, raw, docName, onOpenModel }: Props) {
  const scheme = doc.securitySchemes;
  const usedSecurity = op.securityNames
    .map((name) => scheme.find((s) => s.name === name))
    .filter((s): s is NonNullable<typeof s> => Boolean(s));

  const paramColumns = useMemo(
    () => [
      {
        title: '参数名',
        dataIndex: 'name',
        width: 200,
        render: (value: string, record: { required: boolean }) => (
          <span className="field-name">
            {value}
            {record.required && <span className="req-star">*</span>}
          </span>
        ),
      },
      { title: '位置', dataIndex: 'location', width: 90, render: (value: string) => <span className="dim">{value}</span> },
      {
        title: '类型',
        dataIndex: 'type',
        width: 200,
        render: (_: unknown, record: { type: string; enumValues: string[] }) => (
          <>
            <span className="type-tag">{record.type}</span>
            {record.enumValues.map((v) => (
              <span className="enum-tag" key={v} style={{ marginLeft: 4 }}>
                {v}
              </span>
            ))}
          </>
        ),
      },
      {
        title: '默认值',
        dataIndex: 'defaultValue',
        width: 110,
        render: (value: string) => <span className="dim">{value === '' ? '—' : value}</span>,
      },
      {
        title: '说明',
        dataIndex: 'description',
        render: (value: string) => (value === '' ? <span className="dim">—</span> : value),
      },
    ],
    [],
  );

  return (
    <>
      <div className="card card-hero">
        <div className="op-title">
          <MethodChip method={op.method} />
          <span className={op.deprecated ? 'deprecated' : undefined}>{op.summary}</span>
          {op.deprecated && (
            <Tag color="red" size="small" type="light">
              已废弃
            </Tag>
          )}
          <Tag color="blue" size="small" type="light">
            {op.tag}
          </Tag>
        </div>
        <div className="op-path">
          <code>{op.path}</code>
          <span className="dim">{doc.meta.baseUrl || window.location.origin}</span>
          <Button size="small" theme="borderless" type="tertiary" icon={<IconCopy size="small" />} onClick={() => void copyText(op.path)}>
            复制路径
          </Button>
        </div>
        <div className="op-meta">
          <span>文档源：{docName}</span>
          {op.operationId && <span>operationId：{op.operationId}</span>}
          {usedSecurity.length > 0 && <span>需要鉴权：{usedSecurity.map((s) => s.name).join('、')}</span>}
        </div>
        {op.description && <div className="op-desc">{op.description}</div>}
      </div>

      {usedSecurity.length > 0 && (
        <Banner
          type="warning"
          fullMode={false}
          bordered
          style={{ marginBottom: 16 }}
          description={usedSecurity
            .map((s) =>
              s.type === 'http'
                ? `${s.name}：HTTP ${s.scheme}，请求头 ${s.headerName || 'Authorization'}`
                : `${s.name}：${s.in} 请求头 ${s.headerName}${s.bearerHint ? '，值需带 Bearer 前缀' : ''}`,
            )
            .join('；')}
        />
      )}

      <Section title="请求参数" count={op.params.length}>
        {op.params.length === 0 ? (
          <div className="empty">无参数</div>
        ) : (
          <Table columns={paramColumns} dataSource={op.params} rowKey={(r) => `${r?.location}:${r?.name}`} pagination={false} size="small" bordered />
        )}
      </Section>

      {op.requestContents.length > 0 && (
        <Section title="请求体" count={op.requestContents.length}>
          <Contents blocks={op.requestContents} raw={raw} onOpenModel={onOpenModel} />
        </Section>
      )}

      <Section title="响应" count={op.responses.length}>
        {op.responses.length === 0 ? (
          <div className="empty">文档未声明响应</div>
        ) : (
          op.responses.map((resp) => (
            <div key={resp.status} style={{ marginBottom: 16 }}>
              <div className="section-title" style={{ fontSize: 13 }}>
                <Tag color={resp.status.startsWith('2') ? 'green' : 'orange'} size="small" type="light">
                  {resp.status}
                </Tag>
                <span className="dim">{resp.description}</span>
              </div>
              {resp.contents.length === 0 ? (
                <div className="empty">无响应体</div>
              ) : (
                <Contents blocks={resp.contents} raw={raw} onOpenModel={onOpenModel} />
              )}
            </div>
          ))
        )}
      </Section>

      <TryPanel op={op} doc={doc} raw={raw} />
    </>
  );
}
