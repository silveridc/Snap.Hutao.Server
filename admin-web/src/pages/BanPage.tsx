import { useState } from 'react';

import { IconRefresh } from '@douyinfe/semi-icons';
import {
  Banner,
  Button,
  Card,
  Input,
  Modal,
  Pagination,
  Space,
  Table,
  TextArea,
  Toast,
  Typography,
} from '@douyinfe/semi-ui';

import { banUid, batchBanUids, getBans, unbanUid } from '@/api/service';
import { useAuth } from '@/auth/AuthProvider';
import { Field } from '@/components/Field';
import { t, useLocale } from '@/i18n';
import { useQuery } from '@/hooks/useQuery';
import { formatDateTime } from '@/lib/format';
import type { BanItem } from '@/types/api';

const UID_PATTERN = /^\d{9,10}$/;

function parseUids(text: string): string[] {
  return text
    .split(/[\s,，]+/)
    .map((value) => value.trim())
    .filter(Boolean);
}

export default function BanPage() {
  const { user } = useAuth();
  const locale = useLocale();
  const canWrite = Boolean(user?.CanWrite);

  const [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const list = useQuery(() => getBans(page, pageSize, keyword), [keyword, page, pageSize, locale]);

  const [uid, setUid] = useState('');
  const [reason, setReason] = useState('');
  const [banning, setBanning] = useState(false);

  const [batchText, setBatchText] = useState('');
  const [batchReason, setBatchReason] = useState('');
  const [batching, setBatching] = useState(false);

  // 已在第一页时依赖不变，需要手动重取
  function reloadFromFirstPage() {
    if (page === 1) {
      list.reload();
    } else {
      setPage(1);
    }
  }

  function search(value: string) {
    setKeyword(value.trim());
    setPage(1);
  }

  async function submitBan() {
    const trimmed = uid.trim();
    if (!UID_PATTERN.test(trimmed)) {
      Toast.warning(t('ban.messages.uidFormat'));
      return;
    }
    setBanning(true);
    try {
      const message = await banUid(trimmed, reason.trim());
      Toast.success(message || t('common.success'));
      setUid('');
      setReason('');
      reloadFromFirstPage();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setBanning(false);
    }
  }

  async function submitBatchBan() {
    const uids = parseUids(batchText);
    if (!uids.length) {
      Toast.warning(t('ban.messages.emptyUids'));
      return;
    }
    const invalid = uids.filter((value) => !UID_PATTERN.test(value));
    if (invalid.length) {
      Toast.warning(t('ban.messages.invalidUids', { count: invalid.length }));
      return;
    }
    setBatching(true);
    try {
      const message = await batchBanUids(uids, batchReason.trim());
      Toast.success(message || t('common.success'));
      setBatchText('');
      setBatchReason('');
      reloadFromFirstPage();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setBatching(false);
    }
  }

  function confirmUnban(row: BanItem) {
    Modal.confirm({
      title: t('ban.dialog.unbanHeader'),
      content: t('ban.dialog.unbanBody', { uid: row.Uid }),
      okText: t('ban.dialog.unbanConfirm'),
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          const message = await unbanUid(row.Uid);
          Toast.success(message || t('common.success'));
          list.reload();
        } catch (error) {
          Toast.error((error as Error).message);
        }
      },
    });
  }

  return (
    <div className="space-y-4">
      {canWrite && (
        <div className="grid gap-4 lg:grid-cols-2">
          <Card title={t('ban.add.title')}>
            <Typography.Text type="tertiary" className="mb-4 block">
              {t('ban.add.tip')}
            </Typography.Text>
            <div className="space-y-4">
              <Field label={t('ban.add.uidLabel')}>
                <Input
                  value={uid}
                  placeholder={t('ban.add.uidPlaceholder')}
                  showClear
                  onChange={setUid}
                  onEnterPress={submitBan}
                />
              </Field>
              <Field label={t('ban.add.reasonLabel')}>
                <Input
                  value={reason}
                  placeholder={t('ban.add.reasonPlaceholder')}
                  showClear
                  onChange={setReason}
                  onEnterPress={submitBan}
                />
              </Field>
              <Button theme="solid" type="danger" loading={banning} onClick={submitBan}>
                {t('ban.add.submit')}
              </Button>
            </div>
          </Card>

          <Card title={t('ban.batch.title')}>
            <Typography.Text type="tertiary" className="mb-4 block">
              {t('ban.batch.tip')}
            </Typography.Text>
            <div className="space-y-4">
              <Field label={t('ban.batch.uidListLabel')}>
                <TextArea
                  value={batchText}
                  placeholder={t('ban.batch.uidListPlaceholder')}
                  autosize={{ minRows: 3, maxRows: 10 }}
                  onChange={setBatchText}
                />
              </Field>
              <Field label={t('ban.batch.reasonLabel')}>
                <Input
                  value={batchReason}
                  placeholder={t('ban.batch.reasonPlaceholder')}
                  showClear
                  onChange={setBatchReason}
                  onEnterPress={submitBatchBan}
                />
              </Field>
              <Button theme="solid" type="danger" loading={batching} onClick={submitBatchBan}>
                {t('ban.batch.submit')}
              </Button>
            </div>
          </Card>
        </div>
      )}

      <Card
        title={t('ban.list.title')}
        headerExtraContent={
          <Button size="small" icon={<IconRefresh />} loading={list.loading} onClick={list.reload}>
            {t('common.refresh')}
          </Button>
        }
      >
        <Space wrap className="mb-4" spacing="tight">
          <Input
            value={keyword}
            placeholder={t('ban.list.searchPlaceholder')}
            showClear
            style={{ width: 260 }}
            onChange={setKeyword}
            onEnterPress={() => search(keyword)}
          />
          <Button theme="solid" type="primary" onClick={() => search(keyword)}>
            {t('common.search')}
          </Button>
        </Space>

        {list.error && <Banner type="danger" closeIcon={null} description={list.error.message} className="mb-3" />}

        <Table
          size="small"
          rowKey="Uid"
          loading={list.loading}
          pagination={false}
          dataSource={list.data?.Items ?? []}
          columns={[
            { title: 'UID', dataIndex: 'Uid', width: 160 },
            {
              title: t('ban.list.colReason'),
              dataIndex: 'Reason',
              ellipsis: true,
              render: (value: string | null) => value || '—',
            },
            {
              title: t('ban.list.colBanTime'),
              dataIndex: 'BanTime',
              width: 200,
              render: (value: number) => <span className="tabular">{formatDateTime(value)}</span>,
            },
            {
              title: t('ban.list.colOp'),
              width: 100,
              fixed: 'right',
              render: (_value: unknown, record: BanItem) =>
                canWrite ? (
                  <Button size="small" theme="borderless" type="danger" onClick={() => confirmUnban(record)}>
                    {t('ban.list.unban')}
                  </Button>
                ) : null,
            },
          ]}
        />

        <div className="mt-4 flex justify-end">
          <Pagination
            total={list.data?.Total ?? 0}
            currentPage={page}
            pageSize={pageSize}
            pageSizeOpts={[10, 20, 50, 100]}
            showSizeChanger
            showTotal
            onChange={(nextPage, size) => {
              setPage(nextPage);
              setPageSize(size);
            }}
          />
        </div>
      </Card>
    </div>
  );
}
