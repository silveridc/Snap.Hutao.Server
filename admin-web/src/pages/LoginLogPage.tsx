import { useState } from 'react';

import { Banner, Button, Card, Input, Pagination, Select, Table, Tag } from '@douyinfe/semi-ui';
import type { ColumnProps } from '@douyinfe/semi-ui/lib/es/table';

import { getLoginLogs } from '@/api/service';
import { t, te, useLocale } from '@/i18n';
import { useQuery } from '@/hooks/useQuery';
import type { LoginLogItem } from '@/types/api';

// 属地是 IP2Region 的四段数组（国家/省份/城市/运营商），查不到的段是 "0"，去重后拼成可读文本
function fmtRegion(region?: (string | null)[] | null): string {
  if (!region?.length) {
    return '—';
  }
  const parts = region.map((part) => (part ?? '').trim()).filter((part) => part && part !== '0');
  return parts.length ? [...new Set(parts)].join(' ') : '—';
}

function fmtMethod(method?: string): string {
  return method && te(`loginlog.methods.${method}`) ? t(`loginlog.methods.${method}`) : method || '—';
}

// 失败原因存的是服务端词条键，能查到本地化就用本地化文案，查不到原样展示
function fmtReason(row: LoginLogItem): string {
  if (row.Success || !row.FailureKey) {
    return '—';
  }
  return te(`server.${row.FailureKey}`) ? t(`server.${row.FailureKey}`) : row.FailureKey;
}

function fmtDevice(row: LoginLogItem): string {
  const name = row.DeviceName || row.DeviceId;
  if (!name) {
    return '—';
  }
  return row.OperatingSystem ? `${name} · ${row.OperatingSystem}` : name;
}

export default function LoginLogPage() {
  useLocale();

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const [keyword, setKeyword] = useState('');
  const [success, setSuccess] = useState<'' | 'true' | 'false'>('');
  const [applied, setApplied] = useState<{ keyword: string; success: '' | 'true' | 'false' }>({ keyword: '', success: '' });

  const list = useQuery(
    () =>
      getLoginLogs({
        page,
        pageSize,
        keyword: applied.keyword || undefined,
        success: applied.success === '' ? undefined : applied.success === 'true',
      }),
    [page, pageSize, applied],
  );

  function search() {
    setApplied({ keyword: keyword.trim(), success });
    setPage(1);
  }

  function reset() {
    setKeyword('');
    setSuccess('');
    setApplied({ keyword: '', success: '' });
    setPage(1);
  }

  const columns: ColumnProps<LoginLogItem>[] = [
    { title: t('loginlog.columns.time'), dataIndex: 'LoginAt', width: 170, render: (value: string) => new Date(value).toLocaleString() },
    { title: t('loginlog.columns.user'), dataIndex: 'UserName', width: 220, ellipsis: true, render: (value?: string | null) => value || '—' },
    { title: t('loginlog.columns.method'), dataIndex: 'Method', width: 90, render: fmtMethod },
    {
      title: t('loginlog.columns.result'),
      dataIndex: 'Success',
      width: 80,
      render: (value: boolean) => (
        <Tag size="small" type="light" color={value ? 'green' : 'red'}>
          {value ? t('loginlog.filters.success') : t('loginlog.filters.failure')}
        </Tag>
      ),
    },
    { title: t('loginlog.columns.ip'), dataIndex: 'Ip', width: 140, render: (value?: string | null) => value || '—' },
    { title: t('loginlog.columns.region'), dataIndex: 'IpRegion', width: 170, ellipsis: true, render: (_v, row) => fmtRegion(row.IpRegion) },
    { title: t('loginlog.columns.device'), dataIndex: 'DeviceName', width: 220, ellipsis: true, render: (_v, row) => fmtDevice(row) },
    { title: t('loginlog.columns.reason'), dataIndex: 'FailureKey', ellipsis: true, minWidth: 160, render: (_v, row) => fmtReason(row) },
  ];

  return (
    <div className="space-y-4">
      <Card title={t('loginlog.title')}>
        <div className="mb-4 flex flex-wrap items-center gap-2">
          <Input
            className="w-72"
            placeholder={t('loginlog.filters.keywordPlaceholder')}
            value={keyword}
            showClear
            onEnterPress={search}
            onChange={(value) => setKeyword(value)}
          />
          <Select className="w-36" value={success} onChange={(value) => setSuccess(String(value) as '' | 'true' | 'false')}>
            <Select.Option value="">{t('loginlog.filters.all')}</Select.Option>
            <Select.Option value="true">{t('loginlog.filters.success')}</Select.Option>
            <Select.Option value="false">{t('loginlog.filters.failure')}</Select.Option>
          </Select>
          <Button theme="solid" type="primary" loading={list.loading} onClick={search}>
            {t('loginlog.filters.search')}
          </Button>
          <Button theme="light" onClick={reset}>
            {t('loginlog.filters.reset')}
          </Button>
          <span className="ml-auto text-xs text-[var(--semi-color-text-2)]">{t('loginlog.tip')}</span>
        </div>

        {list.error && <Banner type="danger" closeIcon={null} description={list.error.message} className="mb-3" />}

        <Table
          size="small"
          rowKey="Id"
          loading={list.loading}
          dataSource={list.data?.Items ?? []}
          columns={columns}
          empty={t('loginlog.empty')}
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
