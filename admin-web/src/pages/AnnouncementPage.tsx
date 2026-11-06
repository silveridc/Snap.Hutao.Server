import { useState } from 'react';

import { IconRefresh } from '@douyinfe/semi-icons';
import {
  Banner,
  Button,
  Card,
  Input,
  Modal,
  Pagination,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  TextArea,
  Toast,
  Typography,
} from '@douyinfe/semi-ui';

import {
  deleteAnnouncement,
  getAnnouncements,
  setAnnouncementEnabled,
  updateAnnouncement,
  uploadAnnouncement,
} from '@/api/service';
import { useAuth } from '@/auth/AuthProvider';
import { Field } from '@/components/Field';
import { t, useLocale } from '@/i18n';
import { useQuery } from '@/hooks/useQuery';
import { formatDateTime } from '@/lib/format';
import type { AnnouncementForm, AnnouncementItem } from '@/types/api';

const SEVERITY_KEYS = ['informational', 'success', 'warning', 'error'];
const SEVERITY_COLORS = ['grey', 'green', 'orange', 'red'] as const;

interface Draft {
  Title: string;
  Content: string;
  Severity: number;
  Link: string;
  MaxPresentVersion: string;
}

const EMPTY_DRAFT: Draft = { Title: '', Content: '', Severity: 0, Link: '', MaxPresentVersion: '' };

function toForm(draft: Draft): AnnouncementForm {
  return {
    Title: draft.Title.trim(),
    Content: draft.Content,
    Severity: draft.Severity,
    Link: draft.Link.trim(),
    MaxPresentVersion: draft.MaxPresentVersion.trim() || null,
  };
}

function toDraft(row: AnnouncementItem): Draft {
  return {
    Title: row.Title ?? '',
    Content: row.Content ?? '',
    Severity: row.Severity ?? 0,
    Link: row.Link ?? '',
    MaxPresentVersion: row.MaxPresentVersion ?? '',
  };
}

function DraftFields({ draft, onChange }: { draft: Draft; onChange: (next: Partial<Draft>) => void }) {
  const severityOptions = SEVERITY_KEYS.map((key, value) => ({ value, label: t(`announcement.severity.${key}`) }));
  return (
    <div className="grid gap-4 sm:grid-cols-2">
      <Field label={t('announcement.form.titleLabel')}>
        <Input
          value={draft.Title}
          placeholder={t('announcement.form.titlePlaceholder')}
          showClear
          onChange={(value) => onChange({ Title: value })}
        />
      </Field>
      <Field label={t('announcement.form.severity')}>
        <Select
          value={draft.Severity}
          optionList={severityOptions}
          style={{ width: '100%' }}
          onChange={(value) => onChange({ Severity: Number(value) })}
        />
      </Field>
      <Field label={t('announcement.form.link')}>
        <Input
          value={draft.Link}
          placeholder={t('announcement.form.linkPlaceholder')}
          showClear
          onChange={(value) => onChange({ Link: value })}
        />
      </Field>
      <Field label={t('announcement.form.maxVersion')}>
        <Input
          value={draft.MaxPresentVersion}
          placeholder={t('announcement.form.maxVersionPlaceholder')}
          showClear
          onChange={(value) => onChange({ MaxPresentVersion: value })}
        />
      </Field>
      <div className="sm:col-span-2">
        <Field label={t('announcement.form.content')}>
          <TextArea
            value={draft.Content}
            placeholder={t('announcement.form.contentPlaceholder')}
            autosize={{ minRows: 4, maxRows: 12 }}
            onChange={(value) => onChange({ Content: value })}
          />
        </Field>
      </div>
    </div>
  );
}

export default function AnnouncementPage() {
  const { user } = useAuth();
  const locale = useLocale();
  const canWrite = Boolean(user?.CanWrite);

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const list = useQuery(() => getAnnouncements(page, pageSize), [page, pageSize, locale]);

  const [draft, setDraft] = useState<Draft>(EMPTY_DRAFT);
  const [publishing, setPublishing] = useState(false);

  const [editing, setEditing] = useState<{ row: AnnouncementItem; draft: Draft }>();
  const [saving, setSaving] = useState(false);

  // 已在第一页时依赖不变，需要手动重取
  function reloadFromFirstPage() {
    if (page === 1) {
      list.reload();
    } else {
      setPage(1);
    }
  }

  async function publish() {
    const form = toForm(draft);
    if (!form.Title || !form.Content.trim()) {
      Toast.warning(t('announcement.messages.titleContentRequired'));
      return;
    }
    setPublishing(true);
    try {
      const message = await uploadAnnouncement(form);
      Toast.success(message || t('common.success'));
      setDraft(EMPTY_DRAFT);
      reloadFromFirstPage();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setPublishing(false);
    }
  }

  async function saveEdit() {
    if (!editing) {
      return;
    }
    const form = toForm(editing.draft);
    if (!form.Title || !form.Content.trim()) {
      Toast.warning(t('announcement.messages.titleContentRequired'));
      return;
    }
    setSaving(true);
    try {
      const message = await updateAnnouncement(editing.row.Id, editing.row.Locale, form);
      Toast.success(message || t('common.success'));
      setEditing(undefined);
      list.reload();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setSaving(false);
    }
  }

  async function toggleEnabled(row: AnnouncementItem, enabled: boolean) {
    try {
      const message = await setAnnouncementEnabled(row.Id, enabled, row.Locale);
      Toast.success(message || t('common.success'));
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      list.reload();
    }
  }

  function confirmDelete(row: AnnouncementItem) {
    Modal.confirm({
      title: t('announcement.dialog.removeTitle'),
      content: t('announcement.dialog.removeBody', { title: row.Title, id: row.Id, locale: row.Locale }),
      okText: t('common.delete'),
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          const message = await deleteAnnouncement(row.Id, row.Locale);
          Toast.success(message || t('common.success'));
          list.reload();
        } catch (error) {
          Toast.error((error as Error).message);
        }
      },
    });
  }

  const columns = [
    { title: 'ID', dataIndex: 'Id', width: 90 },
    { title: t('announcement.list.colTitle'), dataIndex: 'Title', ellipsis: true, minWidth: 180 },
    { title: t('announcement.list.colLocale'), dataIndex: 'Locale', width: 90 },
    {
      title: t('announcement.list.colSeverity'),
      dataIndex: 'Severity',
      width: 90,
      render: (value: number) => (
        <Tag size="small" color={SEVERITY_COLORS[value] ?? 'grey'}>
          {t(`announcement.severity.${SEVERITY_KEYS[value] ?? 'informational'}`)}
        </Tag>
      ),
    },
    {
      title: t('announcement.list.colMaxVersion'),
      dataIndex: 'MaxPresentVersion',
      width: 140,
      render: (value: string | null) => value || '—',
    },
    {
      title: t('announcement.list.colEnabled'),
      dataIndex: 'Enabled',
      width: 80,
      render: (value: boolean, record: AnnouncementItem) => (
        <Switch checked={value} disabled={!canWrite} onChange={(next) => toggleEnabled(record, next)} />
      ),
    },
    {
      title: t('announcement.list.colUpdateTime'),
      dataIndex: 'LastUpdateTime',
      width: 180,
      render: (value: number) => <span className="tabular">{formatDateTime(value)}</span>,
    },
    ...(canWrite
      ? [
          {
            title: t('announcement.list.colOp'),
            width: 130,
            fixed: 'right' as const,
            render: (_value: unknown, record: AnnouncementItem) => (
              <Space spacing="tight" wrap>
                <Button size="small" theme="borderless" onClick={() => setEditing({ row: record, draft: toDraft(record) })}>
                  {t('common.edit')}
                </Button>
                <Button size="small" theme="borderless" type="danger" onClick={() => confirmDelete(record)}>
                  {t('common.delete')}
                </Button>
              </Space>
            ),
          },
        ]
      : []),
  ];

  return (
    <div className="space-y-4">
      {canWrite && (
        <Card title={t('announcement.form.title')}>
          <Typography.Text type="tertiary" className="mb-4 block">
            {t('announcement.form.tip')}
          </Typography.Text>
          <DraftFields draft={draft} onChange={(next) => setDraft({ ...draft, ...next })} />
          <div className="mt-4">
            <Button theme="solid" type="primary" loading={publishing} onClick={publish}>
              {t('announcement.form.submit')}
            </Button>
          </div>
        </Card>
      )}

      <Card
        title={t('announcement.list.title')}
        headerExtraContent={
          <Button size="small" icon={<IconRefresh />} loading={list.loading} onClick={list.reload}>
            {t('common.refresh')}
          </Button>
        }
      >
        {list.error && <Banner type="danger" closeIcon={null} description={list.error.message} className="mb-3" />}

        <Table
          size="small"
          rowKey={(record?: AnnouncementItem) => `${record?.Id}:${record?.Locale}`}
          loading={list.loading}
          pagination={false}
          dataSource={list.data?.Items ?? []}
          columns={columns}
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

      <Modal
        visible={Boolean(editing)}
        title={t('announcement.edit.title')}
        okText={t('common.save')}
        cancelText={t('common.cancel')}
        confirmLoading={saving}
        width={720}
        onOk={saveEdit}
        onCancel={() => setEditing(undefined)}
      >
        {editing && (
          <div className="space-y-4">
            <Typography.Text type="tertiary" className="block !text-xs">
              {t('announcement.edit.meta', { id: editing.row.Id, locale: editing.row.Locale })}
            </Typography.Text>
            <DraftFields draft={editing.draft} onChange={(next) => setEditing({ ...editing, draft: { ...editing.draft, ...next } })} />
          </div>
        )}
      </Modal>
    </div>
  );
}
