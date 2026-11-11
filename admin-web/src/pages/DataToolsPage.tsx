import { useState } from 'react';

import { Banner, Button, Card, Input, Modal, Pagination, Table, Tag, Toast, Typography } from '@douyinfe/semi-ui';
import type { ColumnProps } from '@douyinfe/semi-ui/lib/es/table';

import {
  addAllowedVersion,
  getInvalidGachaUids,
  getAllowedVersions,
  removeAllowedVersion,
  removeInvalidGachaUid,
} from '@/api/service';
import { t, useLocale } from '@/i18n';
import { useQuery } from '@/hooks/useQuery';

export default function DataToolsPage() {
  useLocale();

  const [newVersion, setNewVersion] = useState('');
  const [addingVersion, setAddingVersion] = useState(false);
  const versions = useQuery(getAllowedVersions, []);

  async function addVersion() {
    const header = newVersion.trim();
    if (!header) {
      Toast.warning(t('datatools.messages.headerRequired'));
      return;
    }
    setAddingVersion(true);
    try {
      const message = await addAllowedVersion(header);
      Toast.success(message || t('datatools.messages.addSuccess'));
      setNewVersion('');
      versions.reload();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setAddingVersion(false);
    }
  }

  function confirmRemoveVersion(header: string) {
    Modal.confirm({
      title: t('datatools.dialog.removeVersionHeader'),
      content: t('datatools.dialog.removeVersionBody', { header }),
      okText: t('datatools.dialog.removeVersionConfirm'),
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          const message = await removeAllowedVersion(header);
          Toast.success(message || t('datatools.messages.removeSuccess'));
          versions.reload();
        } catch (error) {
          Toast.error((error as Error).message);
        }
      },
    });
  }

  const [keyword, setKeyword] = useState('');
  const [appliedKeyword, setAppliedKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const uids = useQuery(() => getInvalidGachaUids(page, pageSize, appliedKeyword), [page, pageSize, appliedKeyword]);

  function search() {
    setAppliedKeyword(keyword.trim());
    setPage(1);
  }

  function confirmRemoveUid(uid: string) {
    Modal.confirm({
      title: t('datatools.dialog.removeUidHeader'),
      content: t('datatools.dialog.removeUidBody', { uid }),
      okText: t('datatools.dialog.removeUidConfirm'),
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          const message = await removeInvalidGachaUid(uid);
          Toast.success(message || t('datatools.messages.removeSuccess'));
          uids.reload();
        } catch (error) {
          Toast.error((error as Error).message);
        }
      },
    });
  }

  const uidColumns: ColumnProps<{ uid: string }>[] = [
    { title: 'UID', dataIndex: 'uid', render: (uid: string) => <span className="font-mono text-xs">{uid}</span> },
    {
      title: t('datatools.uids.colOp'),
      key: 'op',
      width: 120,
      render: (_v, row) => (
        <Button theme="borderless" type="primary" size="small" onClick={() => confirmRemoveUid(row.uid)}>
          {t('datatools.uids.remove')}
        </Button>
      ),
    },
  ];

  return (
    <div className="grid grid-cols-1 gap-4 xl:grid-cols-12">
      <Card
        className="xl:col-span-5"
        title={t('datatools.versions.title')}
        headerExtraContent={
          <Button size="small" type="tertiary" loading={versions.loading} onClick={versions.reload}>
            {t('datatools.versions.refresh')}
          </Button>
        }
      >
        <p className="mb-4 text-[13px] text-[var(--semi-color-text-2)]">
          {t('datatools.versions.tipPre')}
          <code className="mx-1 rounded bg-[var(--semi-color-fill-0)] px-1 py-0.5 font-mono text-xs">allowed_versions</code>
          {t('datatools.versions.tipSuf')}
        </p>

        {versions.error && <Banner type="danger" closeIcon={null} description={versions.error.message} className="mb-3" />}

        <div className="mb-4 flex gap-2">
          <Input
            value={newVersion}
            placeholder={t('datatools.versions.inputPlaceholder')}
            showClear
            onChange={setNewVersion}
            onEnterPress={addVersion}
          />
          <Button theme="solid" loading={addingVersion} onClick={addVersion}>
            {t('datatools.versions.add')}
          </Button>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {versions.data?.map((header) => (
            <Tag
              key={header}
              color="blue"
              type="light"
              size="large"
              closable
              onClose={() => confirmRemoveVersion(header)}
            >
              {header}
            </Tag>
          ))}
        </div>
        {!versions.loading && !versions.data?.length && (
          <Typography.Text type="tertiary">{t('datatools.versions.empty')}</Typography.Text>
        )}
      </Card>

      <Card className="xl:col-span-7" title={t('datatools.uids.title')}>
        <p className="mb-4 text-[13px] text-[var(--semi-color-text-2)]">
          {t('datatools.uids.tipPre')}
          <code className="mx-1 rounded bg-[var(--semi-color-fill-0)] px-1 py-0.5 font-mono text-xs">invalid_gacha_uids</code>
          {t('datatools.uids.tipSuf')}
        </p>

        <div className="mb-4 flex gap-2">
          <Input
            value={keyword}
            placeholder={t('datatools.uids.searchPlaceholder')}
            showClear
            style={{ width: 260 }}
            onChange={setKeyword}
            onEnterPress={search}
          />
          <Button theme="solid" loading={uids.loading} onClick={search}>
            {t('datatools.uids.search')}
          </Button>
        </div>

        {uids.error && <Banner type="danger" closeIcon={null} description={uids.error.message} className="mb-3" />}

        <Table
          size="small"
          rowKey="uid"
          loading={uids.loading}
          dataSource={(uids.data?.Items ?? []).map((uid) => ({ uid }))}
          columns={uidColumns}
          pagination={false}
        />

        <div className="mt-4 flex justify-end">
          <Pagination
            total={uids.data?.Total ?? 0}
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
