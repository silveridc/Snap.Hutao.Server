import { useState } from 'react';
import type { ReactNode } from 'react';

import { IconCopy, IconDownload, IconRefresh } from '@douyinfe/semi-icons';
import {
  Banner,
  Button,
  Card,
  Checkbox,
  CheckboxGroup,
  DatePicker,
  Input,
  InputNumber,
  Modal,
  Pagination,
  Select,
  Space,
  Table,
  Tag,
  TextArea,
  Toast,
  Typography,
} from '@douyinfe/semi-ui';

import {
  batchDeleteRedeemCodes,
  deleteRedeemCode,
  generateRedeemCodes,
  getRedeemCodeUsages,
  getRedeemCodes,
  updateRedeemCode,
  useRedeemCode,
} from '@/api/service';
import { useAuth } from '@/auth/AuthProvider';
import { Field } from '@/components/Field';
import { t, useLocale } from '@/i18n';
import { useQuery } from '@/hooks/useQuery';
import { formatDateTime } from '@/lib/format';
import type { RedeemCodeItem, RedeemCodeUsageItem } from '@/types/api';

const TIME_LIMITED = 1;
const TIMES_LIMITED = 2;
const GACHA_LOG = 1;
const CDN = 2;
const PICKER_FORMAT = 'yyyy-MM-dd HH:mm:ss';
// 服务端把 pageSize 夹在 1..100，导出全部只能翻页取
const EXPORT_PAGE_SIZE = 100;
const EXPORT_MAX_ROWS = 5000;

const pad = (value: number): string => String(value).padStart(2, '0');

// 服务端给的是 ISO 串，DatePicker 按 format 解析的是本地时间字符串
function toPickerValue(value: string | null): string {
  if (!value) {
    return '';
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '';
  }
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
}

function limitsOf(type: number): string[] {
  const limits: string[] = [];
  if ((type & TIME_LIMITED) !== 0) {
    limits.push('time');
  }
  if ((type & TIMES_LIMITED) !== 0) {
    limits.push('times');
  }
  return limits;
}

function typeOf(limits: string[]): number {
  return (limits.includes('time') ? TIME_LIMITED : 0) | (limits.includes('times') ? TIMES_LIMITED : 0);
}

function serviceName(value: number): string {
  return value === CDN ? t('redeem.service.cdn') : t('redeem.service.gachalog');
}

function limitTags(type: number): ReactNode {
  if (type === 0) {
    return (
      <Tag size="small" color="grey">
        {t('redeem.limit.none')}
      </Tag>
    );
  }
  return (
    <Space spacing="tight">
      {(type & TIME_LIMITED) !== 0 && (
        <Tag size="small" color="light-blue">
          {t('redeem.limit.time')}
        </Tag>
      )}
      {(type & TIMES_LIMITED) !== 0 && (
        <Tag size="small" color="light-blue">
          {t('redeem.limit.times')}
        </Tag>
      )}
    </Space>
  );
}

function csvCell(value: unknown): string {
  const text = String(value ?? '');
  return /[",\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text;
}

interface Draft {
  ServiceType: number;
  Value: number;
  Description: string;
  Limits: string[];
  ExpireAt: string;
  Times: number;
}

const EMPTY_DRAFT: Draft = { ServiceType: GACHA_LOG, Value: 30, Description: '', Limits: [], ExpireAt: '', Times: 1 };

function toDraft(row: RedeemCodeItem): Draft {
  return {
    ServiceType: row.ServiceType,
    Value: row.Value,
    Description: row.Description ?? '',
    Limits: limitsOf(row.Type),
    ExpireAt: (row.Type & TIME_LIMITED) === 0 ? '' : toPickerValue(row.ExpireTime),
    Times: row.TimesAllowed || 1,
  };
}

// 生成与编辑的表单项一致，只有多出来的「生成数量」
function DraftFields({
  draft,
  onChange,
  countField,
}: {
  draft: Draft;
  onChange: (next: Partial<Draft>) => void;
  countField?: ReactNode;
}) {
  const serviceOptions = [
    { value: GACHA_LOG, label: serviceName(GACHA_LOG) },
    { value: CDN, label: serviceName(CDN) },
  ];
  return (
    <div className="grid gap-4 sm:grid-cols-2">
      {countField}
      <Field label={t('redeem.gen.serviceType')}>
        <Select
          value={draft.ServiceType}
          optionList={serviceOptions}
          style={{ width: '100%' }}
          onChange={(value) => onChange({ ServiceType: Number(value) })}
        />
      </Field>
      <Field label={t('redeem.gen.valueDays')}>
        <InputNumber
          value={draft.Value}
          min={1}
          max={3650}
          style={{ width: '100%' }}
          onChange={(value) => onChange({ Value: Number(value) })}
        />
      </Field>
      <div className="sm:col-span-2">
        <Field label={t('redeem.gen.limitMode')} hint={t('redeem.gen.limitModeHint')}>
          <CheckboxGroup value={draft.Limits} onChange={(value) => onChange({ Limits: value.map(String) })}>
            <Checkbox value="time">{t('redeem.limit.time')}</Checkbox>
            <Checkbox value="times">{t('redeem.limit.times')}</Checkbox>
          </CheckboxGroup>
        </Field>
      </div>
      {draft.Limits.includes('time') && (
        <Field label={t('redeem.gen.expireTime')}>
          <DatePicker
            type="dateTime"
            format={PICKER_FORMAT}
            value={draft.ExpireAt}
            style={{ width: '100%' }}
            onChange={(_date, dateStr) => onChange({ ExpireAt: String(dateStr ?? '') })}
          />
        </Field>
      )}
      {draft.Limits.includes('times') && (
        <Field label={t('redeem.gen.times')}>
          <InputNumber
            value={draft.Times}
            min={1}
            max={100000}
            style={{ width: '100%' }}
            onChange={(value) => onChange({ Times: Number(value) })}
          />
        </Field>
      )}
      <div className="sm:col-span-2">
        <Field label={t('redeem.gen.description')}>
          <Input
            value={draft.Description}
            placeholder={t('redeem.gen.descriptionPlaceholder')}
            showClear
            onChange={(value) => onChange({ Description: value })}
          />
        </Field>
      </div>
    </div>
  );
}

// 勾了限制方式却没填值就拦下来，别把无意义的过期时间或 0 次上限写进库
function invalidDraftMessage(draft: Draft): string | null {
  if (draft.Limits.includes('time') && !draft.ExpireAt) {
    return t('redeem.messages.expireTimeRequired');
  }
  if (draft.Limits.includes('times') && (!draft.Times || draft.Times < 1)) {
    return t('redeem.messages.timesRequired');
  }
  return null;
}

function expireOf(draft: Draft): string {
  // 服务端只在 Type 带限时标记时读 ExpireTime，其余情况给个当前时间即可
  return draft.Limits.includes('time') ? new Date(draft.ExpireAt).toISOString() : new Date().toISOString();
}

export default function RedeemPage() {
  const { user } = useAuth();
  const locale = useLocale();
  const canWrite = Boolean(user?.CanWrite);

  const [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const list = useQuery(() => getRedeemCodes(page, pageSize, keyword), [keyword, page, pageSize, locale]);

  const [count, setCount] = useState(1);
  const [draft, setDraft] = useState<Draft>(EMPTY_DRAFT);
  const [generating, setGenerating] = useState(false);
  const [codes, setCodes] = useState<string[]>([]);

  const [useCode, setUseCode] = useState('');
  const [useName, setUseName] = useState('');
  const [useResult, setUseResult] = useState('');
  const [redeeming, setRedeeming] = useState(false);

  const [selected, setSelected] = useState<number[]>([]);
  const [exporting, setExporting] = useState(false);
  const [editing, setEditing] = useState<{ row: RedeemCodeItem; draft: Draft }>();
  const [saving, setSaving] = useState(false);

  const [uses, setUses] = useState<{ row: RedeemCodeItem; items: RedeemCodeUsageItem[] }>();
  const [usesLoading, setUsesLoading] = useState(false);

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
    setSelected([]);
  }

  async function copy(text: string, successText: string) {
    try {
      await navigator.clipboard.writeText(text);
      Toast.success(successText);
    } catch {
      Toast.warning(t('redeem.gen.copyFail'));
    }
  }

  async function generate() {
    if (!count || count < 1) {
      Toast.warning(t('redeem.messages.countRequired'));
      return;
    }
    const invalid = invalidDraftMessage(draft);
    if (invalid) {
      Toast.warning(invalid);
      return;
    }
    setGenerating(true);
    try {
      const created = await generateRedeemCodes({
        Count: count,
        Type: typeOf(draft.Limits),
        ServiceType: draft.ServiceType,
        Value: draft.Value,
        Description: draft.Description.trim(),
        ExpireTime: expireOf(draft),
        Times: draft.Limits.includes('times') ? draft.Times : 0,
      });
      setCodes(created);
      Toast.success(t('redeem.gen.resultCount', { n: created.length }));
      reloadFromFirstPage();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setGenerating(false);
    }
  }

  async function redeem() {
    const code = useCode.trim();
    if (!code) {
      Toast.warning(t('redeem.messages.codeRequired'));
      return;
    }
    setRedeeming(true);
    try {
      const result = await useRedeemCode(code, useName.trim() || null);
      setUseResult(
        t('redeem.use.result', {
          service: serviceName(result.Type),
          days: result.Value,
          expire: formatDateTime(result.ExpireTime),
        }),
      );
      setUseCode('');
      setUseName('');
      list.reload();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setRedeeming(false);
    }
  }

  async function saveEdit() {
    if (!editing) {
      return;
    }
    const invalid = invalidDraftMessage(editing.draft);
    if (invalid) {
      Toast.warning(invalid);
      return;
    }
    setSaving(true);
    try {
      const message = await updateRedeemCode({
        Id: editing.row.Id,
        Type: typeOf(editing.draft.Limits),
        ServiceType: editing.draft.ServiceType,
        Value: editing.draft.Value,
        Description: editing.draft.Description.trim(),
        ExpireTime: expireOf(editing.draft),
        TimesAllowed: editing.draft.Limits.includes('times') ? editing.draft.Times : 0,
      });
      Toast.success(message || t('common.success'));
      setEditing(undefined);
      list.reload();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setSaving(false);
    }
  }

  async function openUsages(row: RedeemCodeItem) {
    setUsesLoading(true);
    try {
      setUses({ row, items: await getRedeemCodeUsages(row.Id) });
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setUsesLoading(false);
    }
  }

  function confirmDelete(row: RedeemCodeItem) {
    Modal.confirm({
      title: t('redeem.dialog.removeTitle'),
      content: t('redeem.dialog.removeBody', { code: row.Code }),
      okText: t('common.delete'),
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          const message = await deleteRedeemCode(row.Id);
          Toast.success(message || t('common.success'));
          setSelected(selected.filter((id) => id !== row.Id));
          list.reload();
        } catch (error) {
          Toast.error((error as Error).message);
        }
      },
    });
  }

  function confirmBatchDelete() {
    if (!selected.length) {
      Toast.warning(t('redeem.messages.selectFirst'));
      return;
    }
    Modal.confirm({
      title: t('redeem.dialog.batchRemoveTitle'),
      content: t('redeem.dialog.batchRemoveBody', { n: selected.length }),
      okText: t('common.delete'),
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          // 批量删除接口只回条数、没有 l10nKey，文案由前端拼
          const removed = await batchDeleteRedeemCodes([...selected]);
          Toast.success(t('redeem.messages.batchDeleted', { n: removed }));
          setSelected([]);
          list.reload();
        } catch (error) {
          Toast.error((error as Error).message);
        }
      },
    });
  }

  async function exportCsv() {
    setExporting(true);
    try {
      const trimmed = keyword.trim();
      const first = await getRedeemCodes(1, EXPORT_PAGE_SIZE, trimmed);
      const items = [...first.Items];
      const pageCount = Math.min(
        Math.ceil(first.Total / EXPORT_PAGE_SIZE),
        Math.ceil(EXPORT_MAX_ROWS / EXPORT_PAGE_SIZE),
      );
      for (let next = 2; next <= pageCount; next += 1) {
        items.push(...(await getRedeemCodes(next, EXPORT_PAGE_SIZE, trimmed)).Items);
      }
      if (!items.length) {
        Toast.warning(t('redeem.messages.exportEmpty'));
        return;
      }
      if (first.Total > items.length) {
        Toast.warning(t('redeem.messages.exportLimited', { max: items.length }));
      }
      const header = ['Code', 'Service', 'Days', 'Limits', 'Used', 'Cap', 'Creator', 'CreateTime', 'ExpireTime', 'Description'];
      const lines = [header.join(',')];
      for (const item of items) {
        lines.push(
          [
            item.Code,
            serviceName(item.ServiceType),
            item.Value,
            limitsOf(item.Type)
              .map((limit) => t(`redeem.limit.${limit}`))
              .join(' + ') || t('redeem.limit.none'),
            item.UsedCount,
            (item.Type & TIMES_LIMITED) === 0 ? '' : item.TimesAllowed,
            item.Creator,
            item.CreateTime,
            (item.Type & TIME_LIMITED) === 0 ? '' : item.ExpireTime,
            item.Description,
          ]
            .map(csvCell)
            .join(','),
        );
      }
      // 带 BOM，Excel 才会按 UTF-8 打开
      const blob = new Blob([`\uFEFF${lines.join('\n')}`], { type: 'text/csv;charset=utf-8' });
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `redeem-codes-${new Date().toISOString().slice(0, 10)}.csv`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setExporting(false);
    }
  }

  const columns = [
    {
      title: t('redeem.list.colCode'),
      dataIndex: 'Code',
      width: 190,
      render: (value: string) => <span className="mono break-all text-xs">{value}</span>,
    },
    {
      title: t('redeem.list.colService'),
      dataIndex: 'ServiceType',
      width: 100,
      render: (value: number) => serviceName(value),
    },
    { title: t('redeem.list.colValue'), dataIndex: 'Value', width: 90 },
    {
      title: t('redeem.list.colLimit'),
      dataIndex: 'Type',
      width: 140,
      render: (value: number) => limitTags(value),
    },
    {
      title: t('redeem.list.colExpire'),
      dataIndex: 'ExpireTime',
      width: 175,
      render: (value: string, record: RedeemCodeItem) =>
        (record.Type & TIME_LIMITED) === 0 ? '—' : <span className="tabular">{formatDateTime(value)}</span>,
    },
    {
      title: t('redeem.list.colUsed'),
      dataIndex: 'UsedCount',
      width: 100,
      render: (value: number, record: RedeemCodeItem) => (
        <span className="tabular">
          {value} / {(record.Type & TIMES_LIMITED) === 0 ? '—' : record.TimesAllowed}
        </span>
      ),
    },
    {
      title: t('redeem.list.colDescription'),
      dataIndex: 'Description',
      ellipsis: true,
      minWidth: 140,
      render: (value: string | null) => value || '—',
    },
    { title: t('redeem.list.colCreator'), dataIndex: 'Creator', ellipsis: true, minWidth: 140 },
    {
      title: t('redeem.list.colCreateTime'),
      dataIndex: 'CreateTime',
      width: 175,
      render: (value: string) => <span className="tabular">{formatDateTime(value)}</span>,
    },
    ...(canWrite
      ? [
          {
            title: t('redeem.list.colOp'),
            width: 210,
            fixed: 'right' as const,
            render: (_value: unknown, record: RedeemCodeItem) => (
              <Space spacing="tight">
                <Button size="small" theme="borderless" disabled={!record.UsedCount} onClick={() => openUsages(record)}>
                  {t('redeem.actions.uses')}
                </Button>
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
        <div className="grid gap-4 xl:grid-cols-3">
          <Card title={t('redeem.gen.title')} className="xl:col-span-2">
            <Typography.Text type="tertiary" className="mb-4 block">
              {t('redeem.gen.tipPre')}
              <code className="mono">POST /api/admin/redeem-codes/generation</code>
              {t('redeem.gen.tipSuf')}
            </Typography.Text>
            <DraftFields
              draft={draft}
              onChange={(next) => setDraft({ ...draft, ...next })}
              countField={
                <Field label={t('redeem.gen.count')}>
                  <InputNumber
                    value={count}
                    min={1}
                    max={1000}
                    style={{ width: '100%' }}
                    onChange={(value) => setCount(Number(value))}
                  />
                </Field>
              }
            />
            <div className="mt-4">
              <Button theme="solid" type="primary" loading={generating} onClick={generate}>
                {t('redeem.gen.submit')}
              </Button>
            </div>
            {codes.length > 0 && (
              <div className="mt-4 space-y-2">
                <Space wrap spacing="tight">
                  <Tag size="small" color="green">
                    {t('redeem.gen.resultCount', { n: codes.length })}
                  </Tag>
                  <Button
                    size="small"
                    theme="light"
                    type="primary"
                    icon={<IconCopy />}
                    onClick={() => copy(codes.join('\n'), t('redeem.gen.copied'))}
                  >
                    {t('redeem.gen.copyAll')}
                  </Button>
                </Space>
                <TextArea value={codes.join('\n')} readonly autosize={{ minRows: 3, maxRows: 10 }} />
              </div>
            )}
          </Card>

          <Card title={t('redeem.use.title')}>
            <Typography.Text type="tertiary" className="mb-4 block">
              {t('redeem.use.tipPre')}
              <code className="mono">POST /api/redeem-codes/redemptions</code>
              {t('redeem.use.tipSuf')}
            </Typography.Text>
            <div className="space-y-4">
              <Field label={t('redeem.use.code')}>
                <Input
                  value={useCode}
                  placeholder={t('redeem.use.codePlaceholder')}
                  className="mono"
                  showClear
                  onChange={setUseCode}
                  onEnterPress={redeem}
                />
              </Field>
              <Field label={t('redeem.use.username')}>
                <Input
                  value={useName}
                  placeholder={t('redeem.use.usernamePlaceholder')}
                  showClear
                  onChange={setUseName}
                  onEnterPress={redeem}
                />
              </Field>
              <Button theme="solid" type="primary" loading={redeeming} onClick={redeem}>
                {t('redeem.use.submit')}
              </Button>
              {useResult && <Banner type="success" closeIcon={null} description={useResult} />}
            </div>
          </Card>
        </div>
      )}

      <Card
        title={t('redeem.list.title')}
        headerExtraContent={
          <Button size="small" icon={<IconRefresh />} loading={list.loading} onClick={list.reload}>
            {t('common.refresh')}
          </Button>
        }
      >
        <Space wrap className="mb-4" spacing="tight">
          <Input
            value={keyword}
            placeholder={t('redeem.list.searchPlaceholder')}
            showClear
            style={{ width: 260, maxWidth: '100%' }}
            onChange={setKeyword}
            onEnterPress={() => search(keyword)}
          />
          <Button theme="solid" type="primary" loading={list.loading} onClick={() => search(keyword)}>
            {t('common.search')}
          </Button>
          {canWrite && (
            <>
              <Button theme="light" type="danger" disabled={!selected.length} onClick={confirmBatchDelete}>
                {t('redeem.list.batchDelete', { n: selected.length })}
              </Button>
              <Button theme="light" icon={<IconDownload />} loading={exporting} onClick={exportCsv}>
                {t('redeem.list.exportCsv')}
              </Button>
            </>
          )}
        </Space>

        {list.error && <Banner type="danger" closeIcon={null} description={list.error.message} className="mb-3" />}

        <Table
          size="small"
          rowKey="Id"
          loading={list.loading}
          pagination={false}
          dataSource={list.data?.Items ?? []}
          columns={columns}
          rowSelection={
            canWrite
              ? {
                  selectedRowKeys: selected,
                  onChange: (keys) => setSelected((keys ?? []).map(Number)),
                }
              : undefined
          }
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
        title={t('redeem.edit.title')}
        okText={t('common.save')}
        cancelText={t('common.cancel')}
        confirmLoading={saving}
        width={640}
        onOk={saveEdit}
        onCancel={() => setEditing(undefined)}
      >
        {editing && (
          <div className="space-y-4">
            <Typography.Text type="tertiary" className="mono block !text-xs">
              {editing.row.Code}
            </Typography.Text>
            <DraftFields
              draft={editing.draft}
              onChange={(next) => setEditing({ ...editing, draft: { ...editing.draft, ...next } })}
            />
          </div>
        )}
      </Modal>

      <Modal
        visible={Boolean(uses)}
        title={uses ? t('redeem.uses.title', { code: uses.row.Code }) : ''}
        footer={null}
        width={640}
        onCancel={() => setUses(undefined)}
      >
        {uses && (
          <Table
            size="small"
            rowKey="UserId"
            loading={usesLoading}
            pagination={false}
            dataSource={uses.items}
            empty={t('redeem.uses.empty')}
            columns={[
              {
                title: t('redeem.uses.colUser'),
                dataIndex: 'UserName',
                ellipsis: true,
                render: (value: string | null) => value ?? '—',
              },
              { title: t('redeem.uses.colUserId'), dataIndex: 'UserId', width: 110 },
              {
                title: t('redeem.uses.colTime'),
                dataIndex: 'UseTime',
                width: 190,
                render: (value: string) => <span className="tabular">{formatDateTime(value)}</span>,
              },
            ]}
          />
        )}
      </Modal>
    </div>
  );
}
