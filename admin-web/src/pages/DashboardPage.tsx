import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router';

import {
  Banner,
  Button,
  Card,
  Progress,
  Space,
  Spin,
  Table,
  Tag,
  Toast,
  Tooltip,
} from '@douyinfe/semi-ui';
import { IconRefresh } from '@douyinfe/semi-icons';

import { getSystemOverview, getUserStatistics, runJob } from '@/api/service';
import { useAuth } from '@/auth/AuthProvider';
import { t, useLocale } from '@/i18n';
import { formatBytes, formatDateTime, formatDuration, formatNumber } from '@/lib/format';
import { useQuery } from '@/hooks/useQuery';
import type { SchedulerJobInfo } from '@/types/api';

function cpuColor(percent: number): string {
  if (percent >= 85) {
    return 'var(--semi-color-danger)';
  }
  if (percent >= 60) {
    return 'var(--semi-color-warning)';
  }
  return 'var(--semi-color-primary)';
}

// 任务实现类型是全限定名，列表里只留类名，完整名放到 tooltip
function shortType(value?: string | null): string {
  if (!value) {
    return '—';
  }
  const index = value.lastIndexOf('.');
  return index >= 0 ? value.slice(index + 1) : value;
}

interface Fact {
  label: string;
  value: string | number;
  mono?: boolean;
}

function Facts({ items, className = 'grid-cols-4' }: { items: Fact[]; className?: string }) {
  return (
    <div className={`grid gap-2 ${className}`}>
      {items.map((item) => (
        <div key={item.label} className="min-w-0">
          <div className={`text-[15px] font-semibold tabular ${item.mono ? 'font-mono break-all text-sm' : ''}`}>{item.value}</div>
          <div className="mt-0.5 text-xs text-[var(--semi-color-text-2)]">{item.label}</div>
        </div>
      ))}
    </div>
  );
}

function InfoRow({ label, value, mono = false }: { label: string; value: string | number; mono?: boolean }) {
  return (
    <div className="flex items-baseline justify-between gap-3 py-1">
      <span className="shrink-0 text-[13px] text-[var(--semi-color-text-2)]">{label}</span>
      <span className={`text-right text-[13px] ${mono ? 'font-mono' : ''}`}>{value}</span>
    </div>
  );
}

export default function DashboardPage() {
  useLocale();
  const { user, refresh } = useAuth();
  const navigate = useNavigate();

  const overview = useQuery(
    () =>
      Promise.all([getSystemOverview(), getUserStatistics()]).then(([ov, stats]) => ({ ov, stats })),
    [],
  );
  const [runningJob, setRunningJob] = useState('');
  const [refreshing, setRefreshing] = useState(false);

  const loading = overview.loading;
  const ov = overview.data?.ov;
  const stats = overview.data?.stats;
  const runtime = ov?.Runtime;
  const resource = ov?.Resource;
  const redis = ov?.Redis;
  const scheduler = ov?.Scheduler;

  const cpuPercent = Math.min(100, Math.max(0, Number(resource?.ProcessCpuPercent ?? 0)));
  const workingSet = formatBytes(resource?.WorkingSetBytes);
  const privateMemory = formatBytes(resource?.PrivateMemoryBytes);
  const managedHeap = formatBytes(resource?.ManagedHeapBytes);
  // 托管堆占私有内存的比例：比例偏高通常意味着 GC 压力大，比单看绝对值有信息量
  const heapShare = useMemo(() => {
    const heap = Number(resource?.ManagedHeapBytes ?? 0);
    const total = Number(resource?.PrivateMemoryBytes ?? 0);
    return total > 0 ? Math.min(100, Math.round((heap / total) * 100)) : 0;
  }, [resource?.ManagedHeapBytes, resource?.PrivateMemoryBytes]);

  // INFO 被服务端禁用时，除接入地址与 Ping 外的指标都拿不到，直接留白比铺一排「—」干净
  const redisItems = useMemo(() => {
    if (!redis) {
      return [];
    }
    const rows: Array<[string, string | number | null | undefined]> = [
      [t('dashboard.redis.endpoint'), redis.Endpoint],
      [t('dashboard.redis.ping'), redis.PingMs === null || redis.PingMs === undefined ? null : `${redis.PingMs} ms`],
      [t('dashboard.redis.version'), redis.Version],
      [t('dashboard.redis.usedMemory'), redis.UsedMemory],
      [t('dashboard.redis.policy'), redis.MaxMemoryPolicy],
      [t('dashboard.redis.clients'), redis.ConnectedClients],
      [t('dashboard.redis.keys'), redis.KeyCount],
      [t('dashboard.redis.uptimeDays'), redis.UptimeDays],
      [t('dashboard.redis.instanceName'), redis.InstanceName],
    ];
    return rows.filter(([, value]) => value !== null && value !== undefined && value !== '') as Array<[string, string | number]>;
  }, [redis, overview.data]);

  const runtimeItems = useMemo(() => {
    if (!runtime) {
      return [];
    }
    return [
      [t('dashboard.system.appVersion'), runtime.ApplicationVersion, true],
      [t('dashboard.system.machine'), runtime.MachineName, true],
      [t('dashboard.system.os'), runtime.OperatingSystem, false],
      [t('dashboard.system.framework'), runtime.Framework, true],
      [t('dashboard.system.processId'), runtime.ProcessId, true],
      [t('dashboard.system.startedAt'), formatDateTime(runtime.StartedAt), false],
      [t('dashboard.system.localTime'), formatDateTime(runtime.LocalTime), false],
      [t('dashboard.system.timeZone'), runtime.TimeZoneId, true],
    ] as Array<[string, string | number, boolean]>;
  }, [runtime, overview.data]);

  const accountItems = useMemo(() => {
    if (!user) {
      return [];
    }
    const yesNo = (flag: boolean) => (flag ? t('dashboard.account.yes') : t('dashboard.account.no'));
    return [
      [t('dashboard.account.email'), user.UserName ?? user.NormalizedUserName ?? '—', false],
      [t('dashboard.account.authKind'), user.AuthKind === 'admin' ? t('dashboard.account.kindAdmin') : t('dashboard.account.kindUser'), false],
      [t('dashboard.account.maintainer'), yesNo(user.IsMaintainer), false],
      [t('dashboard.account.canWrite'), yesNo(user.CanWrite), false],
      [t('dashboard.account.licensedDeveloper'), yesNo(user.IsLicensedDeveloper), false],
      [t('dashboard.account.gachaLogExpireAt'), formatDateTime(user.GachaLogExpireAt), false],
      [t('dashboard.account.cdnExpireAt'), formatDateTime(user.CdnExpireAt), false],
    ] as Array<[string, string, boolean]>;
  }, [user]);

  async function runJobNow(row: SchedulerJobInfo) {
    if (runningJob) {
      return;
    }
    setRunningJob(row.Name);
    try {
      await runJob(row.Name);
      Toast.success(t('dashboard.scheduler.runSuccess', { name: row.Name }));
      overview.reload();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setRunningJob('');
    }
  }

  async function refreshAccount() {
    setRefreshing(true);
    try {
      await refresh();
      Toast.success(t('dashboard.account.refresh'));
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setRefreshing(false);
    }
  }

  const databaseColumns = [
    { title: t('dashboard.database.colName'), dataIndex: 'Name', width: 120 },
    {
      title: t('dashboard.database.colStatus'),
      dataIndex: 'Connected',
      width: 90,
      render: (value: boolean) => (
        <Tag size="small" color={value ? 'green' : 'red'} type="light">
          {value ? t('dashboard.redis.online') : t('dashboard.redis.offline')}
        </Tag>
      ),
    },
    { title: t('dashboard.database.colVersion'), dataIndex: 'ServerVersion', width: 140, render: (value?: string | null) => value || '—' },
    {
      title: t('dashboard.database.colAddress'),
      dataIndex: 'Host',
      minWidth: 200,
      render: (_value: unknown, record: { Host?: string | null; Port?: number | null }) => (
        <span className="font-mono text-xs">{record.Host ? `${record.Host}:${record.Port ?? '—'}` : '—'}</span>
      ),
    },
    { title: t('dashboard.database.colDatabase'), dataIndex: 'Database', minWidth: 160, render: (value?: string | null) => value || '—' },
    { title: t('dashboard.database.colPool'), dataIndex: 'MaxPoolSize', width: 110, align: 'right' as const },
    {
      title: t('dashboard.database.colRoundtrip'),
      dataIndex: 'RoundtripMs',
      width: 110,
      align: 'right' as const,
      render: (value?: number | null) => <span className="font-mono text-xs">{value === null || value === undefined ? '—' : `${value} ms`}</span>,
    },
  ];

  const jobColumns = [
    { title: t('dashboard.scheduler.colName'), dataIndex: 'Name', width: 220, ellipsis: true },
    {
      title: t('dashboard.scheduler.colType'),
      dataIndex: 'JobType',
      minWidth: 200,
      ellipsis: true,
      render: (value?: string | null) => (
        <Tooltip content={value || '—'} position="topLeft">
          <span className="font-mono text-xs">{shortType(value)}</span>
        </Tooltip>
      ),
    },
    { title: t('dashboard.scheduler.colCron'), dataIndex: 'Cron', width: 140, render: (value?: string | null) => <span className="font-mono text-xs">{value || '—'}</span> },
    { title: t('dashboard.scheduler.colState'), dataIndex: 'State', width: 110 },
    { title: t('dashboard.scheduler.colNext'), dataIndex: 'NextFireAt', width: 170, render: (value?: string | null) => formatDateTime(value) },
    { title: t('dashboard.scheduler.colPrevious'), dataIndex: 'PreviousFireAt', width: 170, render: (value?: string | null) => formatDateTime(value) },
    {
      title: t('dashboard.scheduler.colOp'),
      dataIndex: 'Name',
      key: 'op',
      width: 110,
      fixed: 'right' as const,
      render: (_value: unknown, record: SchedulerJobInfo) => (
        <Button
          size="small"
          theme="borderless"
          type="primary"
          loading={runningJob === record.Name}
          disabled={Boolean(runningJob) && runningJob !== record.Name}
          onClick={() => runJobNow(record)}
        >
          {runningJob === record.Name ? t('dashboard.scheduler.running') : t('dashboard.scheduler.runNow')}
        </Button>
      ),
    },
  ];

  const shortcuts = [
    { title: t('dashboard.shortcuts.statistics'), path: '/statistics' },
    { title: t('dashboard.shortcuts.compensation'), path: '/compensation' },
    { title: t('dashboard.shortcuts.announcement'), path: '/announcement' },
    { title: t('dashboard.shortcuts.redeem'), path: '/redeem' },
  ];

  return (
    <div className="space-y-4">
      {overview.error && <Banner type="danger" closeIcon={null} description={overview.error.message} />}

      <div className="grid gap-4 xl:grid-cols-3">
        <Card
          title={
            <Space>
              <span>{t('dashboard.system.title')}</span>
              {runtime && (
                <Tag size="small" type="light" className="font-mono">
                  {runtime.ApplicationVersion}
                </Tag>
              )}
            </Space>
          }
          headerExtraContent={
            <Button size="small" theme="borderless" icon={<IconRefresh />} loading={loading} onClick={overview.reload} />
          }
        >
          {loading && !runtime ? (
            <div className="flex h-64 items-center justify-center">
              <Spin size="large" />
            </div>
          ) : (
            <>
              <div className="border-b border-[var(--semi-color-border)] pb-4">
                <div className="text-[30px] font-semibold leading-tight tracking-tight tabular">{formatDuration(runtime?.UptimeSeconds)}</div>
                <div className="mt-1 text-[13px] text-[var(--semi-color-text-2)]">{t('dashboard.system.uptime')}</div>
              </div>
              <div className="pt-1">
                {runtimeItems.map(([label, value, mono]) => (
                  <InfoRow key={label} label={label} value={value} mono={mono} />
                ))}
              </div>
            </>
          )}
        </Card>

        <Card title={t('dashboard.resource.title')}>
          {loading && !resource ? (
            <div className="flex h-64 items-center justify-center">
              <Spin size="large" />
            </div>
          ) : (
            <>
              <div className="border-b border-dashed border-[var(--semi-color-border)] py-3">
                <div className="mb-2 flex items-baseline justify-between gap-2">
                  <span className="text-[13px] text-[var(--semi-color-text-2)]">{t('dashboard.resource.cpu')}</span>
                  <span className="text-[22px] font-semibold leading-tight tabular">
                    {cpuPercent.toFixed(1)}
                    <span className="ml-0.5 text-xs font-medium text-[var(--semi-color-text-2)]">%</span>
                  </span>
                </div>
                <Progress percent={cpuPercent} showInfo={false} stroke={cpuColor(cpuPercent)} strokeWidth={6} aria-label="cpu" />
              </div>

              <div className="border-b border-dashed border-[var(--semi-color-border)] py-3">
                <div className="mb-2 flex items-baseline justify-between gap-2">
                  <span className="text-[13px] text-[var(--semi-color-text-2)]">{t('dashboard.resource.workingSet')}</span>
                  <span className="text-[22px] font-semibold leading-tight tabular">{workingSet}</span>
                </div>
                <div className="text-xs text-[var(--semi-color-text-2)]">
                  {t('dashboard.resource.privateMemory')} {privateMemory}
                </div>
              </div>

              <div className="py-3">
                <div className="mb-2 flex items-baseline justify-between gap-2">
                  <span className="text-[13px] text-[var(--semi-color-text-2)]">{t('dashboard.resource.managedHeap')}</span>
                  <span className="text-[22px] font-semibold leading-tight tabular">{managedHeap}</span>
                </div>
                <Progress percent={heapShare} showInfo={false} stroke="var(--semi-color-success)" strokeWidth={4} aria-label="heap" />
                <div className="mt-1 text-xs text-[var(--semi-color-text-2)]">
                  {t('dashboard.resource.heapShare')} {heapShare}%
                </div>
              </div>

              <Facts
                className="grid-cols-3 pt-2"
                items={[
                  { label: t('dashboard.resource.cores'), value: resource?.ProcessorCount ?? '—' },
                  { label: t('dashboard.resource.threads'), value: resource?.ThreadCount ?? '—' },
                  { label: t('dashboard.resource.cpuSeconds'), value: `${Number(resource?.ProcessCpuSeconds ?? 0).toFixed(2)}s` },
                ]}
              />
            </>
          )}
        </Card>

        <Card
          title={
            <Space>
              <span>{t('dashboard.redis.title')}</span>
              {redis && (
                <Tag size="small" type="light" color={redis.Connected ? 'green' : 'red'}>
                  {redis.Connected ? t('dashboard.redis.online') : t('dashboard.redis.offline')}
                </Tag>
              )}
            </Space>
          }
        >
          {loading && !redis ? (
            <div className="flex h-64 items-center justify-center">
              <Spin size="large" />
            </div>
          ) : (
            <>
              <div>
                {redisItems.map(([label, value]) => (
                  <InfoRow key={label} label={label} value={value} mono />
                ))}
              </div>
              {redis?.Error && (
                <div className="mt-3 flex items-center gap-1.5 rounded-md bg-[var(--semi-color-warning-light-default)] px-2.5 py-2 text-xs text-[var(--semi-color-warning)]">
                  {t('dashboard.redis.infoUnavailable')}
                  <Tooltip content={redis.Error}>
                    <span className="cursor-help underline">?</span>
                  </Tooltip>
                </div>
              )}
            </>
          )}
        </Card>
      </div>

      <Card title={t('dashboard.database.title')}>
        <Table
          size="small"
          rowKey="Name"
          loading={loading}
          dataSource={ov?.Databases ?? []}
          columns={databaseColumns}
          pagination={false}
          empty={t('dashboard.overview.noData')}
        />
        {ov?.Databases.filter((db) => db.Error).map((db) => (
          <Banner key={db.Name} type="warning" closeIcon={null} description={`${db.Name}: ${db.Error}`} className="mt-3" />
        ))}
      </Card>

      <Card title={t('dashboard.scheduler.title')}>
        {scheduler && (
          <div className="border-b border-[var(--semi-color-border)] pb-4">
            <Facts
              className="grid-cols-2 sm:grid-cols-5"
              items={[
                { label: t('dashboard.scheduler.state'), value: scheduler.State || '—' },
                { label: t('dashboard.scheduler.name'), value: scheduler.Name || '—' },
                { label: t('dashboard.scheduler.jobCount'), value: scheduler.JobCount ?? '—' },
                { label: t('dashboard.scheduler.triggerCount'), value: scheduler.TriggerCount ?? '—' },
                { label: t('dashboard.scheduler.instanceId'), value: scheduler.InstanceId || '—', mono: true },
              ]}
            />
          </div>
        )}
        <Table
          size="small"
          rowKey="Name"
          loading={loading}
          dataSource={scheduler?.Jobs ?? []}
          columns={jobColumns}
          pagination={false}
          empty={t('dashboard.overview.noData')}
          className="mt-2"
        />
        {scheduler?.Error && <Banner type="warning" closeIcon={null} description={scheduler.Error} className="mt-3" />}
      </Card>

      <Card title={t('dashboard.growth.title')}>
        <Facts
          className="grid-cols-2 sm:grid-cols-4"
          items={[
            { label: t('dashboard.growth.total'), value: stats ? formatNumber(stats.TotalUsers) : '—' },
            { label: t('dashboard.growth.today'), value: stats?.NewUsersToday ?? 0 },
            { label: t('dashboard.growth.last7'), value: stats?.NewUsersLast7Days ?? 0 },
            { label: t('dashboard.growth.last30'), value: stats?.NewUsersLast30Days ?? 0 },
          ]}
        />
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card
          title={t('dashboard.account.title')}
          headerExtraContent={
            <Button size="small" loading={refreshing} onClick={refreshAccount}>
              {t('dashboard.account.refresh')}
            </Button>
          }
        >
          {accountItems.map(([label, value, mono]) => (
            <InfoRow key={label} label={label} value={value} mono={mono} />
          ))}
        </Card>

        <Card title={t('dashboard.shortcuts.title')}>
          <Space wrap>
            {shortcuts.map((item) => (
              <Button key={item.path} onClick={() => navigate(item.path)}>
                {item.title}
              </Button>
            ))}
          </Space>
        </Card>
      </div>
    </div>
  );
}
