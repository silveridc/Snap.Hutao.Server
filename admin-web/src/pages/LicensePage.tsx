import { useState } from 'react';

import { Banner, Button, Card, Checkbox, Input, Pagination, Table, Tag, Toast } from '@douyinfe/semi-ui';
import type { ColumnProps } from '@douyinfe/semi-ui/lib/es/table';

import { approveLicenseByService, getLicenseApplications } from '@/api/service';
import { t, useLocale } from '@/i18n';
import { useQuery } from '@/hooks/useQuery';
import type { LicenseApplicationItem } from '@/types/api';

export default function LicensePage() {
  useLocale();

  const [userName, setUserName] = useState('');
  const [code, setCode] = useState('');
  const [approving, setApproving] = useState(false);
  const [result, setResult] = useState('');

  async function submit() {
    if (!userName.trim() || !code.trim()) {
      Toast.warning(t('license.messages.fillRequired'));
      return;
    }
    setApproving(true);
    setResult('');
    try {
      const message = await approveLicenseByService(userName.trim(), code.trim());
      setResult(message || t('license.messages.approveSuccess'));
      Toast.success(t('license.messages.approveSuccess'));
      list.reload();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setApproving(false);
    }
  }

  const [onlyPending, setOnlyPending] = useState(true);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [approvingId, setApprovingId] = useState<number | null>(null);
  const list = useQuery(() => getLicenseApplications(page, pageSize, onlyPending), [page, pageSize, onlyPending]);

  function onFilterChange(next: boolean) {
    setOnlyPending(next);
    setPage(1);
  }

  async function approveRow(row: LicenseApplicationItem) {
    setApprovingId(row.PrimaryId);
    try {
      const message = await approveLicenseByService(row.UserName ?? '', row.ApprovalCode);
      Toast.success(message || t('license.messages.approveSuccess'));
      list.reload();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setApprovingId(null);
    }
  }

  const columns: ColumnProps<LicenseApplicationItem>[] = [
    { title: t('license.columns.applicant'), dataIndex: 'UserName', ellipsis: true, minWidth: 180 },
    {
      title: t('license.columns.projectUrl'),
      dataIndex: 'ProjectUrl',
      ellipsis: true,
      minWidth: 220,
      render: (value: string) => (
        <a className="font-mono text-xs" href={value} target="_blank" rel="noreferrer">
          {value}
        </a>
      ),
    },
    {
      title: t('license.columns.status'),
      dataIndex: 'IsApproved',
      width: 100,
      render: (value: boolean) => (
        <Tag size="small" type="light" color={value ? 'green' : 'amber'}>
          {value ? t('license.list.approved') : t('license.list.pending')}
        </Tag>
      ),
    },
    {
      title: t('license.columns.op'),
      key: 'op',
      width: 90,
      render: (_v, row) =>
        row.IsApproved ? (
          '—'
        ) : (
          <Button size="small" theme="borderless" type="primary" loading={approvingId === row.PrimaryId} onClick={() => approveRow(row)}>
            {t('license.list.approve')}
          </Button>
        ),
    },
  ];

  return (
    <div className="grid grid-cols-1 gap-4 xl:grid-cols-12">
      <Card className="xl:col-span-4" title={t('license.approve.title')}>
        <p className="mb-4 text-[13px] text-[var(--semi-color-text-2)]">
          {t('license.approve.tipPre')}
          <code className="mx-1 rounded bg-[var(--semi-color-fill-0)] px-1 py-0.5 font-mono text-xs">POST /Service/License/Approve</code>
          {t('license.approve.tipSuf')}
        </p>

        <div className="space-y-3">
          <div>
            <div className="mb-1 text-[13px]">{t('license.approve.emailLabel')}</div>
            <Input value={userName} placeholder={t('license.approve.emailPlaceholder')} showClear onChange={setUserName} />
          </div>
          <div>
            <div className="mb-1 text-[13px]">{t('license.approve.codeLabel')}</div>
            <Input value={code} placeholder="approve code" showClear onChange={setCode} />
          </div>
          <Button theme="solid" loading={approving} onClick={submit}>
            {t('license.approve.submit')}
          </Button>
        </div>

        {result && <Banner type="success" closeIcon={null} description={result} className="mt-3" />}
      </Card>

      <Card
        className="xl:col-span-8"
        title={t('license.list.title')}
        headerExtraContent={
          <div className="flex items-center gap-3">
            <Checkbox checked={onlyPending} onChange={(e) => onFilterChange(Boolean(e.target.checked))}>
              {t('license.list.onlyPending')}
            </Checkbox>
            <Button size="small" type="tertiary" loading={list.loading} onClick={list.reload}>
              {t('license.list.refresh')}
            </Button>
          </div>
        }
      >
        {list.error && <Banner type="danger" closeIcon={null} description={list.error.message} className="mb-3" />}

        <Table
          size="small"
          rowKey="PrimaryId"
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
