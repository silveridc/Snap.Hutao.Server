import { useState } from 'react';

import { Banner, Button, Card, Pagination, Table, Tag, Tooltip } from '@douyinfe/semi-ui';
import type { ColumnProps } from '@douyinfe/semi-ui/lib/es/table';

import { getAuditLogs } from '@/api/service';
import { t, useLocale } from '@/i18n';
import { useQuery } from '@/hooks/useQuery';
import type { AuditLogItem } from '@/types/api';

// 属地是 IP2Region 的四段数组（国家/省份/城市/运营商），查不到的段是 "0"，去重后拼成可读文本
function fmtRegion(region?: (string | null)[] | null): string {
  if (!region?.length) {
    return '—';
  }
  const parts = region.map((part) => (part ?? '').trim()).filter((part) => part && part !== '0');
  return parts.length ? [...new Set(parts)].join(' ') : '—';
}

export default function AuditPage() {
  useLocale();

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const list = useQuery(() => getAuditLogs(page, pageSize), [page, pageSize]);

  const columns: ColumnProps<AuditLogItem>[] = [
    { title: t('audit.list.colTime'), dataIndex: 'Timestamp', width: 170, render: (value: number) => (value > 0 ? new Date(value * 1000).toLocaleString() : '—') },
    {
      title: t('audit.list.colOperator'),
      dataIndex: 'AdminUserName',
      width: 190,
      ellipsis: true,
      render: (value?: string | null, row?: AuditLogItem) => (value ? `${value} (#${row?.AdminUserId})` : `— (#${row?.AdminUserId})`),
    },
    { title: t('audit.list.colIp'), dataIndex: 'Ip', width: 140, render: (value?: string | null) => <span className="font-mono text-xs">{value || '—'}</span> },
    { title: t('audit.list.colIpRegion'), dataIndex: 'IpRegion', width: 170, ellipsis: true, render: (_v, row) => fmtRegion(row.IpRegion) },
    { title: t('audit.list.colAction'), dataIndex: 'Action', width: 230, ellipsis: true },
    {
      title: t('audit.list.colResult'),
      dataIndex: 'Success',
      width: 80,
      render: (value: boolean) => (
        <Tag size="small" type="light" color={value ? 'green' : 'red'}>
          {value ? t('audit.list.success') : t('audit.list.failure')}
        </Tag>
      ),
    },
    {
      title: t('audit.list.colDetail'),
      dataIndex: 'Detail',
      ellipsis: true,
      minWidth: 200,
      render: (value?: string | null) =>
        value ? (
          <Tooltip content={value} position="top">
            <span className="font-mono text-xs">{value}</span>
          </Tooltip>
        ) : (
          '—'
        ),
    },
  ];

  return (
    <div className="space-y-4">
      <Card
        title={t('audit.list.title')}
        headerExtraContent={
          <Button size="small" loading={list.loading} onClick={list.reload}>
            {t('audit.list.refresh')}
          </Button>
        }
      >
        <p className="mb-4 text-[13px] text-[var(--semi-color-text-2)]">{t('audit.list.tip')}</p>

        {list.error && <Banner type="danger" closeIcon={null} description={list.error.message} className="mb-3" />}

        <Table
          size="small"
          rowKey="Id"
          loading={list.loading}
          dataSource={list.data?.Items ?? []}
          columns={columns}
          pagination={false}
        />

        <div className="mt-4 flex justify-end">
          <Pagination
            total={list.data?.Total ?? 0}
            currentPage={page}
            pageSize={pageSize}
            pageSizeOpts={[10, 20, 50, 100]}
            showSizeChanger
            showTotal
            onChange={setPage}
            onPageSizeChange={(size: number) => {
              setPageSize(size);
              setPage(1);
            }}
          />
        </div>
      </Card>
    </div>
  );
}
