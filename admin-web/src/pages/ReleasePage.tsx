import { useState } from 'react';

import { IconCopy, IconDeleteStroked, IconPlus, IconRefresh } from '@douyinfe/semi-icons';
import {
  Banner,
  Button,
  Card,
  Input,
  Modal,
  Pagination,
  Select,
  Space,
  Table,
  Tag,
  TextArea,
  Toast,
  Tooltip,
  Typography,
} from '@douyinfe/semi-ui';

import {
  activateRelease,
  deactivateRelease,
  getCurrentRelease,
  getReleaseBucketObjects,
  getReleases,
  publishRelease,
  publishReleaseFromBucket,
} from '@/api/service';
import { useAuth } from '@/auth/AuthProvider';
import { Field } from '@/components/Field';
import { t, useLocale } from '@/i18n';
import { useQuery } from '@/hooks/useQuery';
import { formatBytes, formatDateTime } from '@/lib/format';
import type { HutaoReleaseItem, ReleaseMirror } from '@/types/api';

const MIRROR_TYPE_OPTIONS = ['Direct', 'Archive', 'Browser'].map((value) => ({ value, label: value }));
const SHA256_PATTERN = /^[0-9a-fA-F]{64}$/;
// 安装包对象名里的四段版本号，如 Snap.Hutao-1.17.2.0-x64.msix
const VERSION_IN_KEY_PATTERN = /\d+\.\d+\.\d+\.\d+/;

const EMPTY_MIRROR: ReleaseMirror = { url: '', mirror_name: '', mirror_type: 'Direct' };

interface PublishDraft {
  Version: string;
  Validation: string;
  MinimumVersion: string;
  ReleaseNotes: string;
  Mirrors: ReleaseMirror[];
}

const EMPTY_PUBLISH: PublishDraft = { Version: '', Validation: '', MinimumVersion: '', ReleaseNotes: '', Mirrors: [EMPTY_MIRROR] };

interface BucketDraft {
  Version: string;
  MinimumVersion: string;
  MirrorName: string;
  Validation: string;
  ReleaseNotes: string;
}

const EMPTY_BUCKET: BucketDraft = { Version: '', MinimumVersion: '', MirrorName: '', Validation: '', ReleaseNotes: '' };

export default function ReleasePage() {
  const { user } = useAuth();
  const locale = useLocale();
  const canWrite = Boolean(user?.CanWrite);

  const current = useQuery(getCurrentRelease, [locale]);

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const list = useQuery(() => getReleases(page, pageSize), [page, pageSize, locale]);

  const [prefixDraft, setPrefixDraft] = useState('');
  const [prefix, setPrefix] = useState('');
  const bucket = useQuery(() => getReleaseBucketObjects(prefix), [prefix, locale]);
  const [objectKey, setObjectKey] = useState('');
  const [bucketDraft, setBucketDraft] = useState<BucketDraft>(EMPTY_BUCKET);
  const [bucketPublishing, setBucketPublishing] = useState(false);

  const [draft, setDraft] = useState<PublishDraft>(EMPTY_PUBLISH);
  const [publishing, setPublishing] = useState(false);

  const objects = bucket.data?.Objects ?? [];
  // 换前缀后原对象可能已不在清单里，选中值跟着失效
  const selectedKey = objects.some((object) => object.Key === objectKey) ? objectKey : '';
  const objectOptions = objects.map((object) => ({ value: object.Key, label: `${object.Key} · ${formatBytes(object.Size)}` }));

  function refreshObjects() {
    const next = prefixDraft.trim();
    if (next === prefix) {
      bucket.reload();
    } else {
      setPrefix(next);
    }
  }

  // 发布成功后回到第一页，并刷新当前激活版本
  function reloadAfterPublish() {
    if (page === 1) {
      list.reload();
    } else {
      setPage(1);
    }
    current.reload();
  }

  function onObjectChange(value: string) {
    setObjectKey(value);
    if (value && !bucketDraft.Version.trim()) {
      setBucketDraft({ ...bucketDraft, Version: VERSION_IN_KEY_PATTERN.exec(value.split('/').pop() ?? '')?.[0] ?? '' });
    }
  }

  async function copyHash(value: string) {
    try {
      await navigator.clipboard.writeText(value);
      Toast.success(t('release.current.copiedHash'));
    } catch {
      Toast.error(t('release.current.copyFail'));
    }
  }

  async function publishFromBucket() {
    if (!selectedKey) {
      Toast.warning(t('release.bucket.objectRequired'));
      return;
    }
    const validation = bucketDraft.Validation.trim();
    if (validation && !SHA256_PATTERN.test(validation)) {
      Toast.warning(t('release.bucket.validationInvalid'));
      return;
    }
    setBucketPublishing(true);
    try {
      const message = await publishReleaseFromBucket({
        Key: selectedKey,
        Version: bucketDraft.Version.trim() || null,
        Validation: validation || null,
        MirrorName: bucketDraft.MirrorName.trim() || null,
        MinimumVersion: bucketDraft.MinimumVersion.trim() || null,
        ReleaseNotes: bucketDraft.ReleaseNotes.trim() || null,
      });
      Toast.success(message || t('common.success'));
      setObjectKey('');
      setBucketDraft(EMPTY_BUCKET);
      reloadAfterPublish();
      bucket.reload();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setBucketPublishing(false);
    }
  }

  async function publish() {
    const version = draft.Version.trim();
    const validation = draft.Validation.trim();
    if (!version) {
      Toast.warning(t('release.messages.versionRequired'));
      return;
    }
    if (!validation) {
      Toast.warning(t('release.messages.validationRequired'));
      return;
    }
    const emptyIndex = draft.Mirrors.findIndex((mirror) => !mirror.mirror_name.trim() || !mirror.url.trim());
    if (emptyIndex >= 0) {
      Toast.warning(t('release.messages.mirrorEmpty', { n: emptyIndex + 1 }));
      return;
    }
    setPublishing(true);
    try {
      const message = await publishRelease({
        Version: version,
        Validation: validation,
        MinimumVersion: draft.MinimumVersion.trim() || null,
        Mirrors: draft.Mirrors.map((mirror) => ({
          url: mirror.url.trim(),
          mirror_name: mirror.mirror_name.trim(),
          mirror_type: mirror.mirror_type,
        })),
        ReleaseNotes: draft.ReleaseNotes.trim() || null,
      });
      Toast.success(message || t('common.success'));
      setDraft(EMPTY_PUBLISH);
      reloadAfterPublish();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setPublishing(false);
    }
  }

  function setMirror(index: number, next: Partial<ReleaseMirror>) {
    setDraft({
      ...draft,
      Mirrors: draft.Mirrors.map((mirror, position) => (position === index ? { ...mirror, ...next } : mirror)),
    });
  }

  function confirmActivate(row: HutaoReleaseItem) {
    Modal.confirm({
      title: t('release.dialog.activateTitle'),
      content: t('release.dialog.activateBody', { version: row.Version }),
      okText: t('release.actions.activate'),
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          const message = await activateRelease(row.Id);
          Toast.success(message || t('common.success'));
          list.reload();
          current.reload();
        } catch (error) {
          Toast.error((error as Error).message);
        }
      },
    });
  }

  function confirmDeactivate(row: HutaoReleaseItem) {
    Modal.confirm({
      title: t('release.dialog.deactivateTitle'),
      content: t('release.dialog.deactivateBody', { version: row.Version }),
      okText: t('release.actions.deactivate'),
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          const message = await deactivateRelease(row.Id);
          Toast.success(message || t('common.success'));
          list.reload();
          current.reload();
        } catch (error) {
          Toast.error((error as Error).message);
        }
      },
    });
  }

  const release = current.data;

  const columns = [
    { title: 'ID', dataIndex: 'Id', width: 90 },
    {
      title: t('release.list.colVersion'),
      dataIndex: 'Version',
      width: 150,
      render: (value: string) => <span className="mono">{value}</span>,
    },
    {
      title: 'SHA-256',
      dataIndex: 'Validation',
      minWidth: 200,
      ellipsis: { showTitle: false },
      render: (value: string) => (
        <Tooltip content={<span className="mono break-all">{value}</span>} position="top">
          <span className="mono text-xs text-semi-color-text-2">{value}</span>
        </Tooltip>
      ),
    },
    {
      title: t('release.list.colMirrors'),
      dataIndex: 'Mirrors',
      width: 90,
      render: (value: ReleaseMirror[]) => value.length,
    },
    {
      title: t('release.list.colStatus'),
      dataIndex: 'IsActive',
      width: 90,
      render: (value: boolean) =>
        value ? (
          <Tag size="small" color="green">
            {t('release.list.active')}
          </Tag>
        ) : (
          <Tag size="small" color="grey">
            {t('release.list.history')}
          </Tag>
        ),
    },
    {
      title: t('release.list.colPublisher'),
      dataIndex: 'PublishedByUserName',
      width: 150,
      render: (value: string | null) => value ?? '—',
    },
    {
      title: t('release.list.colPublishedAt'),
      dataIndex: 'PublishedAt',
      width: 190,
      render: (value: string) => <span className="tabular">{formatDateTime(value)}</span>,
    },
    ...(canWrite
      ? [
          {
            title: t('release.list.colOp'),
            width: 110,
            fixed: 'right' as const,
            render: (_value: unknown, record: HutaoReleaseItem) =>
              record.IsActive ? (
                <Button size="small" theme="borderless" type="danger" onClick={() => confirmDeactivate(record)}>
                  {t('release.actions.deactivate')}
                </Button>
              ) : (
                <Button size="small" theme="borderless" type="primary" onClick={() => confirmActivate(record)}>
                  {t('release.actions.activate')}
                </Button>
              ),
          },
        ]
      : []),
  ];

  return (
    <div className="space-y-4">
      {release && (
        <Card title={t('release.current.title')}>
          <div className="grid gap-x-8 sm:grid-cols-2 xl:grid-cols-3">
            <div className="info-row">
              <span className="info-label">{t('release.current.version')}</span>
              <span className="info-value mono">{release.Version}</span>
            </div>
            <div className="info-row">
              <span className="info-label">{t('release.current.publisher')}</span>
              <span className="info-value">{release.PublishedByUserName ?? '—'}</span>
            </div>
            <div className="info-row">
              <span className="info-label">{t('release.current.publishedAt')}</span>
              <span className="info-value tabular">{formatDateTime(release.PublishedAt)}</span>
            </div>
            <div className="info-row">
              <span className="info-label">{t('release.current.mirrors')}</span>
              <span className="info-value tabular">{release.Mirrors.length}</span>
            </div>
            <div className="info-row">
              <span className="info-label">{t('release.current.minimumVersion')}</span>
              <span className="info-value mono">{release.MinimumVersion ?? '—'}</span>
            </div>
            <div className="info-row sm:col-span-2 xl:col-span-3">
              <span className="info-label">SHA-256</span>
              <span className="info-value">
                <Space spacing="tight" align="center">
                  <span className="mono break-all text-xs">{release.Validation}</span>
                  <Tooltip content={t('release.current.copyHash')} position="top">
                    <Button size="small" theme="borderless" icon={<IconCopy />} onClick={() => copyHash(release.Validation)} />
                  </Tooltip>
                </Space>
              </span>
            </div>
            {release.ReleaseNotes && (
              <div className="info-row sm:col-span-2 xl:col-span-3">
                <span className="info-label">{t('release.current.releaseNotes')}</span>
                <span className="info-value">{release.ReleaseNotes}</span>
              </div>
            )}
          </div>
        </Card>
      )}

      {canWrite && (
        <Card title={t('release.bucket.title')}>
          <Typography.Text type="tertiary" className="mb-4 block">
            {t('release.bucket.tip')}
          </Typography.Text>
          {bucket.error && <Banner type="danger" closeIcon={null} description={bucket.error.message} className="mb-4" />}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t('release.bucket.prefix')}>
              <Space wrap spacing="tight">
                <Input
                  value={prefixDraft}
                  placeholder={t('release.bucket.prefixPlaceholder')}
                  showClear
                  style={{ width: 220, maxWidth: '100%' }}
                  onChange={setPrefixDraft}
                  onEnterPress={refreshObjects}
                />
                <Button theme="light" type="primary" loading={bucket.loading} onClick={refreshObjects}>
                  {t('release.bucket.refreshObjects')}
                </Button>
              </Space>
            </Field>
            <Field label={t('release.bucket.object')}>
              <Select
                value={selectedKey}
                optionList={objectOptions}
                placeholder={t('release.bucket.objectPlaceholder')}
                filter
                showClear
                loading={bucket.loading}
                style={{ width: '100%' }}
                onChange={(value) => onObjectChange(String(value ?? ''))}
              />
            </Field>
            <Field label={t('release.form.version')} hint={t('release.bucket.versionAutoHint')}>
              <Input
                value={bucketDraft.Version}
                placeholder={t('release.form.versionPlaceholder')}
                showClear
                onChange={(value) => setBucketDraft({ ...bucketDraft, Version: value })}
              />
            </Field>
            <Field label={t('release.form.minimumVersion')} hint={t('release.form.minimumVersionHint')}>
              <Input
                value={bucketDraft.MinimumVersion}
                placeholder={t('release.form.minimumVersionPlaceholder')}
                showClear
                onChange={(value) => setBucketDraft({ ...bucketDraft, MinimumVersion: value })}
              />
            </Field>
            <Field label={t('release.bucket.mirrorName')} hint={t('release.bucket.mirrorNameHint')}>
              <Input
                value={bucketDraft.MirrorName}
                placeholder={bucket.data?.Host ?? ''}
                showClear
                onChange={(value) => setBucketDraft({ ...bucketDraft, MirrorName: value })}
              />
            </Field>
            <Field label={t('release.bucket.validation')} hint={t('release.bucket.validationHint')}>
              <Input
                value={bucketDraft.Validation}
                className="mono"
                placeholder={t('release.bucket.validationPlaceholder')}
                showClear
                onChange={(value) => setBucketDraft({ ...bucketDraft, Validation: value })}
              />
            </Field>
            <div className="sm:col-span-2">
              <Field label={t('release.form.releaseNotes')}>
                <TextArea
                  value={bucketDraft.ReleaseNotes}
                  placeholder={t('release.form.releaseNotesPlaceholder')}
                  autosize={{ minRows: 2, maxRows: 6 }}
                  onChange={(value) => setBucketDraft({ ...bucketDraft, ReleaseNotes: value })}
                />
              </Field>
            </div>
            <div className="sm:col-span-2 flex flex-wrap items-center gap-3">
              <Button theme="solid" type="primary" loading={bucketPublishing} onClick={publishFromBucket}>
                {t('release.bucket.submit')}
              </Button>
              {bucketPublishing && (
                <Typography.Text type="tertiary" className="!text-xs">
                  {bucketDraft.Validation.trim() ? t('release.bucket.publishingWithHash') : t('release.bucket.publishing')}
                </Typography.Text>
              )}
              {!bucketPublishing && bucket.data?.Truncated && (
                <Typography.Text type="tertiary" className="!text-xs">
                  {t('release.bucket.truncated')}
                </Typography.Text>
              )}
            </div>
          </div>
        </Card>
      )}

      {canWrite && (
        <Card title={t('release.form.title')}>
          <Typography.Text type="tertiary" className="mb-4 block">
            {t('release.form.tipPre')}
            <code className="mono">POST /api/admin/releases</code>
            {t('release.form.tipSuf')}
          </Typography.Text>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label={t('release.form.version')}>
              <Input
                value={draft.Version}
                placeholder={t('release.form.versionPlaceholder')}
                showClear
                onChange={(value) => setDraft({ ...draft, Version: value })}
              />
            </Field>
            <Field label={t('release.form.validation')}>
              <Input
                value={draft.Validation}
                className="mono"
                placeholder={t('release.form.validationPlaceholder')}
                showClear
                onChange={(value) => setDraft({ ...draft, Validation: value })}
              />
            </Field>
            <Field label={t('release.form.minimumVersion')} hint={t('release.form.minimumVersionHint')}>
              <Input
                value={draft.MinimumVersion}
                placeholder={t('release.form.minimumVersionPlaceholder')}
                showClear
                onChange={(value) => setDraft({ ...draft, MinimumVersion: value })}
              />
            </Field>
            <div className="sm:col-span-2">
              <Field label={t('release.form.mirrors')}>
                <div className="space-y-2">
                  {draft.Mirrors.map((mirror, index) => (
                    <div key={index} className="flex flex-wrap items-center gap-2">
                      <Input
                        value={mirror.mirror_name}
                        placeholder={t('release.form.mirrorNamePlaceholder')}
                        showClear
                        style={{ width: 180 }}
                        onChange={(value) => setMirror(index, { mirror_name: value })}
                      />
                      <Select
                        value={mirror.mirror_type}
                        optionList={MIRROR_TYPE_OPTIONS}
                        style={{ width: 130 }}
                        onChange={(value) => setMirror(index, { mirror_type: String(value) })}
                      />
                      <Input
                        className="min-w-0 flex-1"
                        value={mirror.url}
                        placeholder={t('release.form.mirrorUrlPlaceholder')}
                        showClear
                        onChange={(value) => setMirror(index, { url: value })}
                      />
                      <Button
                        size="small"
                        theme="borderless"
                        type="danger"
                        icon={<IconDeleteStroked />}
                        disabled={draft.Mirrors.length <= 1}
                        onClick={() => setDraft({ ...draft, Mirrors: draft.Mirrors.filter((_, position) => position !== index) })}
                      >
                        {t('release.form.removeMirror')}
                      </Button>
                    </div>
                  ))}
                  <Button
                    size="small"
                    theme="light"
                    type="primary"
                    icon={<IconPlus />}
                    onClick={() => setDraft({ ...draft, Mirrors: [...draft.Mirrors, EMPTY_MIRROR] })}
                  >
                    {t('release.form.addMirror')}
                  </Button>
                </div>
              </Field>
            </div>
            <div className="sm:col-span-2">
              <Field label={t('release.form.releaseNotes')}>
                <TextArea
                  value={draft.ReleaseNotes}
                  placeholder={t('release.form.releaseNotesPlaceholder')}
                  autosize={{ minRows: 2, maxRows: 8 }}
                  onChange={(value) => setDraft({ ...draft, ReleaseNotes: value })}
                />
              </Field>
            </div>
            <div className="sm:col-span-2">
              <Button theme="solid" type="primary" loading={publishing} onClick={publish}>
                {t('release.form.submit')}
              </Button>
            </div>
          </div>
        </Card>
      )}

      <Card
        title={t('release.list.title')}
        headerExtraContent={
          <Button size="small" icon={<IconRefresh />} loading={list.loading} onClick={list.reload}>
            {t('common.refresh')}
          </Button>
        }
      >
        {list.error && <Banner type="danger" closeIcon={null} description={list.error.message} className="mb-3" />}

        <Table
          size="small"
          rowKey="Id"
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
    </div>
  );
}
