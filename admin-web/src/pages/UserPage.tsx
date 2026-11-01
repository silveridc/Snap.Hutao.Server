import { useState } from 'react';

import { IconRefresh } from '@douyinfe/semi-icons';
import {
  Banner,
  Button,
  Card,
  Descriptions,
  Input,
  InputNumber,
  Modal,
  Pagination,
  Radio,
  RadioGroup,
  Select,
  Space,
  Spin,
  Switch,
  Table,
  Tag,
  Toast,
  Typography,
} from '@douyinfe/semi-ui';

import {
  getUserDetail,
  getUserStatistics,
  getUsers,
  grantCdnDuration,
  grantGachaLogDuration,
  resetUserPassword,
  updateUserPrivilege,
} from '@/api/service';
import { useAuth } from '@/auth/AuthProvider';
import { t, useLocale } from '@/i18n';
import { useQuery } from '@/hooks/useQuery';
import { formatDateTime, formatNumber } from '@/lib/format';
import type { UserDetail, UserListItem } from '@/types/api';

interface Filters {
  keyword: string;
  role: string;
  expiringInDays: number;
}

const initialFilters: Filters = { keyword: '', role: '', expiringInDays: 0 };

// 授权到期是 unix 秒，0 表示从未授权
function ExpiryCell({ seconds }: { seconds: number }) {
  if (!seconds) {
    return <span className="text-semi-color-text-3">{t('user.list.neverAuthorized')}</span>;
  }
  const days = Math.ceil((seconds * 1000 - Date.now()) / 86400000);
  return (
    <div className="leading-tight">
      <div className="tabular">{formatDateTime(seconds)}</div>
      {days > 0 && days <= 30 && (
        <Tag size="small" color="orange">
          {t('user.list.expiringInDays', { days })}
        </Tag>
      )}
    </div>
  );
}

export default function UserPage() {
  const { user } = useAuth();
  const locale = useLocale();
  const canWrite = Boolean(user?.CanWrite);

  const statistics = useQuery(getUserStatistics, [locale]);

  const [draft, setDraft] = useState<Filters>(initialFilters);
  const [filters, setFilters] = useState<Filters>(initialFilters);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);
  const list = useQuery(
    () =>
      getUsers(page, pageSize, {
        keyword: filters.keyword || undefined,
        maintainer: filters.role === 'maintainer' ? true : undefined,
        developer: filters.role === 'developer' ? true : undefined,
        expiringInDays: filters.expiringInDays || undefined,
      }),
    [filters, page, pageSize, locale],
  );

  const [durationRow, setDurationRow] = useState<UserListItem | null>(null);
  const [service, setService] = useState<'gacha' | 'cdn'>('gacha');
  const [days, setDays] = useState(30);

  const [privilegeRow, setPrivilegeRow] = useState<UserListItem | null>(null);
  const [isMaintainer, setIsMaintainer] = useState(false);
  const [isLicensedDeveloper, setIsLicensedDeveloper] = useState(false);

  const [passwordRow, setPasswordRow] = useState<UserListItem | null>(null);
  const [newPassword, setNewPassword] = useState('');

  const [detailRow, setDetailRow] = useState<UserListItem | null>(null);
  const [detail, setDetail] = useState<UserDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);

  const [submitting, setSubmitting] = useState(false);

  const roleOptions = [
    { value: '', label: t('user.list.allRoles') },
    { value: 'maintainer', label: t('user.list.maintainersOnly') },
    { value: 'developer', label: t('user.list.developersOnly') },
  ];

  const expiryOptions = [
    { value: 0, label: t('user.list.expiryAny') },
    { value: 7, label: t('user.list.expiry7Days') },
    { value: 30, label: t('user.list.expiry30Days') },
  ];

  function apply(next: Partial<Filters>) {
    const merged = { ...filters, ...next };
    setFilters(merged);
    setDraft(merged);
    setPage(1);
  }

  function refreshAll() {
    void Promise.all([statistics.reload(), list.reload()]);
  }

  async function submitDuration() {
    if (!durationRow) {
      return;
    }
    if (!durationRow.UserName) {
      Toast.warning(t('user.messages.noUserName'));
      return;
    }
    setSubmitting(true);
    try {
      const message =
        service === 'gacha'
          ? await grantGachaLogDuration(durationRow.UserName, days)
          : await grantCdnDuration(durationRow.UserName, days);
      Toast.success(message || t('common.success'));
      setDurationRow(null);
      refreshAll();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setSubmitting(false);
    }
  }

  async function submitPrivilege() {
    if (!privilegeRow) {
      return;
    }
    setSubmitting(true);
    try {
      const message = await updateUserPrivilege(privilegeRow.Id, isMaintainer, isLicensedDeveloper);
      Toast.success(message || t('common.success'));
      setPrivilegeRow(null);
      refreshAll();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setSubmitting(false);
    }
  }

  async function submitPassword() {
    if (!passwordRow) {
      return;
    }
    if (newPassword.length < 8) {
      Toast.warning(t('user.messages.passwordTooShort'));
      return;
    }
    setSubmitting(true);
    try {
      const message = await resetUserPassword(passwordRow.Id, newPassword);
      Toast.success(message || t('common.success'));
      setPasswordRow(null);
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setSubmitting(false);
    }
  }

  async function openDetail(row: UserListItem) {
    setDetailRow(row);
    setDetail(null);
    setDetailLoading(true);
    try {
      setDetail(await getUserDetail(row.Id));
    } catch (error) {
      setDetailRow(null);
      Toast.error((error as Error).message);
    } finally {
      setDetailLoading(false);
    }
  }

  const tiles = statistics.data
    ? [
        { label: t('user.stats.totalUsers'), value: formatNumber(statistics.data.TotalUsers) },
        { label: t('user.stats.maintainers'), value: formatNumber(statistics.data.Maintainers) },
        { label: t('user.stats.licensedDevelopers'), value: formatNumber(statistics.data.LicensedDevelopers) },
        { label: t('user.stats.activeGachaLogUsers'), value: formatNumber(statistics.data.ActiveGachaLogUsers) },
        { label: t('user.stats.activeCdnUsers'), value: formatNumber(statistics.data.ActiveCdnUsers) },
        { label: t('user.stats.bannedUids'), value: formatNumber(statistics.data.BannedUids) },
        { label: t('user.stats.gachaItemCount'), value: formatNumber(statistics.data.GachaItemCount) },
        { label: t('user.stats.abyssRecords'), value: formatNumber(statistics.data.SpiralAbyssRecordCount) },
        { label: t('user.stats.theaterRecords'), value: formatNumber(statistics.data.RoleCombatRecordCount) },
      ]
    : [];

  return (
    <div className="space-y-4">
      <Card
        title={t('user.stats.title')}
        headerExtraContent={
          <Button size="small" icon={<IconRefresh />} loading={statistics.loading} onClick={statistics.reload}>
            {t('common.refresh')}
          </Button>
        }
      >
        {statistics.error && <Banner type="danger" closeIcon={null} description={statistics.error.message} className="mb-3" />}
        <Spin spinning={statistics.loading && tiles.length === 0}>
          <div className="grid grid-cols-2 gap-x-4 gap-y-4 sm:grid-cols-3 lg:grid-cols-5">
            {tiles.map((tile) => (
              <div key={tile.label} className="min-w-0">
                <div className="text-[22px] font-semibold leading-8 tabular text-semi-color-text-0">{tile.value}</div>
                <div className="truncate text-xs text-semi-color-text-2">{tile.label}</div>
              </div>
            ))}
          </div>
        </Spin>
      </Card>

      <Card
        title={t('user.list.title')}
        headerExtraContent={
          <Button size="small" icon={<IconRefresh />} loading={list.loading} onClick={list.reload}>
            {t('common.refresh')}
          </Button>
        }
      >
        <Space wrap className="mb-4" spacing="tight">
          <Input
            value={draft.keyword}
            placeholder={t('user.list.searchPlaceholder')}
            showClear
            style={{ width: 260 }}
            onChange={(value) => setDraft((prev) => ({ ...prev, keyword: value }))}
            onEnterPress={() => apply({ keyword: draft.keyword.trim() })}
          />
          <Select
            value={draft.role}
            optionList={roleOptions}
            style={{ width: 150 }}
            onChange={(value) => apply({ role: String(value ?? '') })}
          />
          <Select
            value={draft.expiringInDays}
            optionList={expiryOptions}
            style={{ width: 160 }}
            onChange={(value) => apply({ expiringInDays: Number(value) })}
          />
          <Button theme="solid" type="primary" onClick={() => apply({ keyword: draft.keyword.trim() })}>
            {t('common.search')}
          </Button>
        </Space>

        {list.error && <Banner type="danger" closeIcon={null} description={list.error.message} className="mb-3" />}

        <Table
          size="small"
          rowKey="Id"
          loading={list.loading}
          pagination={false}
          dataSource={list.data?.Items ?? []}
          columns={[
            { title: 'ID', dataIndex: 'Id', width: 90 },
            {
              title: t('user.list.colUserName'),
              dataIndex: 'UserName',
              width: 240,
              render: (value: string | null, record: UserListItem) => (
                <div className="min-w-0">
                  <div className="truncate">{value ?? '—'}</div>
                  <Typography.Text type="tertiary" size="small" className="tabular">
                    {t('user.list.idSuffix', { id: record.Id })}
                  </Typography.Text>
                </div>
              ),
            },
            {
              title: t('user.list.colMaintainer'),
              dataIndex: 'IsMaintainer',
              width: 90,
              render: (value: boolean) =>
                value ? (
                  <Tag size="small" color="green">
                    {t('common.yes')}
                  </Tag>
                ) : (
                  '—'
                ),
            },
            {
              title: t('user.list.colDeveloper'),
              dataIndex: 'IsLicensedDeveloper',
              width: 90,
              render: (value: boolean) =>
                value ? (
                  <Tag size="small" color="light-blue">
                    {t('common.yes')}
                  </Tag>
                ) : (
                  '—'
                ),
            },
            {
              title: t('user.list.colGachaExpireAt'),
              dataIndex: 'GachaLogExpireAt',
              width: 190,
              render: (value: number) => <ExpiryCell seconds={value} />,
            },
            {
              title: t('user.list.colCdnExpireAt'),
              dataIndex: 'CdnExpireAt',
              width: 190,
              render: (value: number) => <ExpiryCell seconds={value} />,
            },
            {
              title: t('user.list.colOperation'),
              width: 300,
              fixed: 'right',
              render: (_value: unknown, record: UserListItem) => (
                <Space spacing="tight">
                  <Button size="small" theme="borderless" type="primary" onClick={() => openDetail(record)}>
                    {t('user.list.detail')}
                  </Button>
                  {canWrite && (
                    <>
                      <Button
                        size="small"
                        theme="borderless"
                        type="primary"
                        onClick={() => {
                          setDurationRow(record);
                          setService('gacha');
                          setDays(30);
                        }}
                      >
                        {t('user.list.adjustDuration')}
                      </Button>
                      <Button
                        size="small"
                        theme="borderless"
                        type="primary"
                        onClick={() => {
                          setPrivilegeRow(record);
                          setIsMaintainer(record.IsMaintainer);
                          setIsLicensedDeveloper(record.IsLicensedDeveloper);
                        }}
                      >
                        {t('user.list.privilege')}
                      </Button>
                      <Button
                        size="small"
                        theme="borderless"
                        type="warning"
                        onClick={() => {
                          setPasswordRow(record);
                          setNewPassword('');
                        }}
                      >
                        {t('user.list.resetPassword')}
                      </Button>
                    </>
                  )}
                </Space>
              ),
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
            onChange={(currentPage, size) => {
              setPage(currentPage);
              setPageSize(size);
            }}
          />
        </div>
      </Card>

      <Modal
        title={t('user.duration.title')}
        visible={durationRow !== null}
        confirmLoading={submitting}
        okText={t('user.duration.submit')}
        cancelText={t('common.cancel')}
        onOk={submitDuration}
        onCancel={() => setDurationRow(null)}
      >
        <div className="space-y-4">
          <div>
            <Typography.Text type="tertiary" className="mb-1 block !text-xs">
              {t('user.duration.targetUser')}
            </Typography.Text>
            <Input value={durationRow?.UserName ?? ''} readonly />
          </div>
          <div>
            <Typography.Text type="tertiary" className="mb-1 block !text-xs">
              {t('user.duration.serviceType')}
            </Typography.Text>
            <RadioGroup value={service} onChange={(event) => setService(event.target.value as 'gacha' | 'cdn')}>
              <Radio value="gacha">{t('user.duration.gachaLog')}</Radio>
              <Radio value="cdn">{t('user.duration.cdn')}</Radio>
            </RadioGroup>
          </div>
          <div>
            <Typography.Text type="tertiary" className="mb-1 block !text-xs">
              {t('user.duration.days')}
            </Typography.Text>
            <InputNumber value={days} min={-3650} max={3650} step={1} style={{ width: 160 }} onChange={(value) => setDays(Number(value) || 0)} />
          </div>
        </div>
      </Modal>

      <Modal
        title={t('user.privilege.title')}
        visible={privilegeRow !== null}
        confirmLoading={submitting}
        okText={t('common.save')}
        cancelText={t('common.cancel')}
        onOk={submitPrivilege}
        onCancel={() => setPrivilegeRow(null)}
      >
        <div className="space-y-4">
          <div>
            <Typography.Text type="tertiary" className="mb-1 block !text-xs">
              {t('user.privilege.targetUser')}
            </Typography.Text>
            <Input value={privilegeRow?.UserName ?? ''} readonly />
          </div>
          <div className="info-row">
            <span className="info-label">{t('user.privilege.maintainer')}</span>
            <Switch checked={isMaintainer} onChange={setIsMaintainer} />
          </div>
          <div className="info-row">
            <span className="info-label">{t('user.privilege.licensedDeveloper')}</span>
            <Switch checked={isLicensedDeveloper} onChange={setIsLicensedDeveloper} />
          </div>
        </div>
      </Modal>

      <Modal
        title={t('user.password.title')}
        visible={passwordRow !== null}
        confirmLoading={submitting}
        okText={t('user.password.submit')}
        cancelText={t('common.cancel')}
        onOk={submitPassword}
        onCancel={() => setPasswordRow(null)}
      >
        <div className="space-y-4">
          <div>
            <Typography.Text type="tertiary" className="mb-1 block !text-xs">
              {t('user.password.targetUser')}
            </Typography.Text>
            <Input value={passwordRow?.UserName ?? ''} readonly />
          </div>
          <div>
            <Typography.Text type="tertiary" className="mb-1 block !text-xs">
              {t('user.password.newPassword')}
            </Typography.Text>
            <Input
              type="password"
              value={newPassword}
              placeholder={t('user.password.newPasswordPlaceholder')}
              showClear
              onChange={setNewPassword}
              onEnterPress={submitPassword}
            />
          </div>
        </div>
      </Modal>

      <Modal
        title={t('user.detail.title')}
        visible={detailRow !== null}
        width={620}
        onCancel={() => setDetailRow(null)}
        footer={<Button onClick={() => setDetailRow(null)}>{t('common.close')}</Button>}
      >
        <Spin spinning={detailLoading}>
          {detail && (
            <>
              <Descriptions
                size="small"
                data={[
                  { key: t('user.list.colUserName'), value: detail.UserName ?? '—' },
                  { key: t('user.detail.redeemUsedCount'), value: formatNumber(detail.RedeemUsedCount) },
                  { key: t('user.privilege.maintainer'), value: detail.IsMaintainer ? t('common.yes') : t('common.no') },
                  {
                    key: t('user.privilege.licensedDeveloper'),
                    value: detail.IsLicensedDeveloper ? t('common.yes') : t('common.no'),
                  },
                  { key: t('user.list.colGachaExpireAt'), value: formatDateTime(detail.GachaLogExpireAt) },
                  { key: t('user.list.colCdnExpireAt'), value: formatDateTime(detail.CdnExpireAt) },
                ]}
              />
              <Typography.Text strong className="mb-2 mt-4 block">
                {t('user.detail.gachaUidTitle', { n: detail.GachaUids.length })}
              </Typography.Text>
              <Table
                size="small"
                rowKey="Uid"
                pagination={false}
                dataSource={detail.GachaUids}
                columns={[
                  { title: t('user.detail.colUid'), dataIndex: 'Uid', width: 160 },
                  { title: t('user.detail.colItemCount'), dataIndex: 'ItemCount', width: 120 },
                ]}
                scroll={{ y: 260 }}
              />
            </>
          )}
        </Spin>
      </Modal>
    </div>
  );
}
