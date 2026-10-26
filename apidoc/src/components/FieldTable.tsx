import { Table } from '@douyinfe/semi-ui';
import type { Field } from '../openapi/types';

interface Props {
  fields: Field[];
  onOpenModel?: (name: string) => void;
  empty?: string;
}

function TypeCell({ field, onOpenModel }: { field: Field; onOpenModel?: (name: string) => void }) {
  return (
    <>
      <span
        className={`type-tag${field.ref && onOpenModel ? ' type-ref' : ''}`}
        onClick={() => field.ref && onOpenModel?.(field.ref)}
      >
        {field.type}
      </span>
      {field.enumValues.length > 0 && (
        <span style={{ marginLeft: 6 }}>
          {field.enumValues.map((v) => (
            <span className="enum-tag" key={v}>
              {v}
            </span>
          ))}
        </span>
      )}
    </>
  );
}

export default function FieldTable({ fields, onOpenModel, empty = '该模型没有字段' }: Props) {
  const columns = [
    {
      title: '字段',
      dataIndex: 'label',
      width: 260,
      render: (_: unknown, record: Field) => (
        <span className="field-name">
          {record.label}
          {record.required && <span className="req-star">*</span>}
          {record.recursive && <span className="dim"> ↻</span>}
        </span>
      ),
    },
    {
      title: '类型',
      dataIndex: 'type',
      width: 240,
      render: (_: unknown, record: Field) => <TypeCell field={record} onOpenModel={onOpenModel} />,
    },
    {
      title: '必填',
      dataIndex: 'required',
      width: 68,
      render: (value: boolean) => <span className="dim">{value ? '是' : '否'}</span>,
    },
    {
      title: '可为空',
      dataIndex: 'nullable',
      width: 78,
      render: (value: boolean, record: Field) =>
        record.ref || record.children.length ? <span className="dim">—</span> : <span className="dim">{value ? '是' : '否'}</span>,
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
  ];

  return (
    <Table
      className="fields"
      columns={columns}
      dataSource={fields}
      rowKey="name"
      pagination={false}
      size="small"
      bordered
      empty={empty}
    />
  );
}
