import { useNavigate } from 'react-router';

import { IconRefresh } from '@douyinfe/semi-icons';
import { Banner, Button, Card, Empty, Space, Table, Tag, Typography } from '@douyinfe/semi-ui';

import { getAuditLogs, getCurrentRelease, getLicenseApplications, getSystemOverview, getUserStatistics } from '@/api/service';
import { useAuth } from '@/auth/AuthProvider';
import { navItems } from '@/config/nav';
import { useQuery } from '@/hooks/useQuery';
import { t, useLocale } from '@/i18n';
import { formatBytes, formatDateTime, formatDuration, formatNumber, formatTime } from '@/lib/format';
import type { AuditLogItem, HutaoReleaseItem, SystemOverview, UserStatistics } from '@/types/api';

interface HomeData {
  stats: UserStatistics | null;
  overview: SystemOverview | null;
  release: HutaoReleaseItem | null;
  pendingLicenses: number;
  audits: AuditLogItem[];
  failed: string[];
}

const LABELS: Record<string, string> = {
  stats: 'home.growth.title',
  overview: 'home.health.title',
  release: 'home.release.title',
  license: 'home.pending.license',
  audit: 'home.audit.title',
};

async function loadHome(): Promise<HomeData> {
  const [statsResult, overviewResult, releaseResult, licenseResult, auditResult] = await Promise.allSettled([
    getUserStatistics(),
    getSystemOverview(),
    getCurrentRelease(),
    getLicenseApplications(1, 1, true),
    getAuditLogs(1, 6),
  ]);

  const failed: string[] = [];
  // 单块数据失败不影响其它卡片，失败的分区名收集起来在顶部提示
  function settle<T>(result: PromiseSettledResult<T>, name: string): T | null {
    if (result.status === 'fulfilled') {
      return result.value;
    }
    failed.push(name);
    return null;
  }

  return {
    stats: settle(statsResult, 'stats'),
    overview: settle(overviewResult, 'overview'),
    release: settle(releaseResult, 'release'),
    pendingLicenses: settle(licenseResult, 'license')?.Total ?? 0,
    audits: settle(auditResult, 'audit')?.Items ?? [],
    failed,
  };
}

function greetingKey(): string {
  const hour = new Date().getHours();
  if (hour < 6) {
    return 'home.greeting.night';
  }
  if (hour < 12) {
    return 'home.greeting.morning';
  }
  if (hour < 18) {
    return 'home.greeting.afternoon';
  }
  return 'home.greeting.evening';
}

function StatusTag({ ok, unknown }: { ok: boolean; unknown?: boolean }) {
  if (unknown) {
    return (
      <Tag size="small" color="grey">
        {t('home.health.unknown')}
      </Tag>
    );
  }
  return (
    <Tag size="small" color={ok ? 'green' : 'red'}>
      {ok ? t('home.health.ok') : t('home.health.down')}
    </Tag>
  );
}

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="min-w-0">
      <div className="text-[22px] font-semibold leading-8 tabular text-semi-color-text-0">{value}</div>
      <div className="truncate text-xs text-semi-color-text-2">{label}</div>
      {hint && <div className="truncate text-xs text-semi-color-text-3">{hint}</div>}
    </div>
  );
}

export default function HomePage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const locale = useLocale();
  const { data, loading, reload } = useQuery(loadHome, [locale]);

  const stats = data?.stats ?? null;
  const overview = data?.overview ?? null;
  const release = data?.release ?? null;
  const databases = overview?.Databases ?? [];
  const redis = overview?.Redis ?? null;
  const scheduler = overview?.Scheduler ?? null;

  const tiles = stats
    ? [
        { label: t('home.growth.total'), value: formatNumber(stats.TotalUsers) },
        { label: t('home.growth.today'), value: formatNumber(stats.NewUsersToday) },
        { label: t('home.growth.last7'), value: formatNumber(stats.NewUsersLast7Days) },
        { label: t('home.growth.last30'), value: formatNumber(stats.NewUsersLast30Days) },
      ]
    : [];

  const facts = stats
    ? [
        { label: t('home.growth.maintainers'), value: formatNumber(stats.Maintainers) },
        { label: t('home.growth.developers'), value: formatNumber(stats.LicensedDevelopers) },
        { label: t('home.growth.banned'), value: formatNumber(stats.BannedUids) },
        { label: t('home.growth.activeGacha'), value: formatNumber(stats.ActiveGachaLogUsers) },
        { label: t('home.growth.activeCdn'), value: formatNumber(stats.ActiveCdnUsers) },
        { label: t('home.growth.gachaItems'), value: formatNumber(stats.GachaItemCount) },
      ]
    : [];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0">
          <Typography.Title heading={4} className="!mb-1 !text-[20px] !whitespace-normal break-all">
            {t(greetingKey(), { name: user?.UserName ?? '' })}
          </Typography.Title>
          <Typography.Text type="tertiary" className="!text-[13px]">
            {t('home.greeting.sub', { date: new Date().toLocaleDateString(locale === 'zh_CN' ? 'zh-CN' : 'en-US') })}
            {data && !loading ? ` · ${t('home.updatedAt', { time: formatTime(new Date().toISOString()) })}` : ''}
          </Typography.Text>
        </div>
        <Button icon={<IconRefresh />} loading={loading} onClick={reload}>
          {t('common.refresh')}
        </Button>
      </div>

      {data && data.failed.length > 0 && (
        <Banner
          type="warning"
          closeIcon={null}
          description={t('home.errors.partial', { names: data.failed.map((name) => t(LABELS[name] ?? name)).join('、') })}
        />
      )}

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card title={t('home.growth.title')} className="xl:col-span-1">
          {tiles.length === 0 ? (
            <Empty description={t('common.empty')} />
          ) : (
            <>
              <div className="grid grid-cols-2 gap-4">
                {tiles.map((tile) => (
                  <Stat key={tile.label} label={tile.label} value={tile.value} />
                ))}
              </div>
              <div className="mt-4 grid grid-cols-2 gap-x-4 gap-y-2 border-t border-dashed border-semi-color-border pt-3 md:grid-cols-3">
                {facts.map((fact) => (
                  <div key={fact.label} className="flex min-w-0 items-baseline gap-1 text-xs">
                    <span className="shrink-0 tabular font-semibold text-semi-color-text-0">{fact.value}</span>
                    {/* truncate 对行内 span 无效（需要块级盒），改成 flex 子项才能真正省略号截断 */}
                    <span className="min-w-0 truncate text-semi-color-text-2">{fact.label}</span>
                  </div>
                ))}
              </div>
            </>
          )}
        </Card>

        <Card title={t('home.health.title')} className="xl:col-span-1">
          <div className="mb-3">
            <div className="text-[22px] font-semibold leading-8 tabular text-semi-color-text-0">
              {formatDuration(overview?.Runtime.UptimeSeconds)}
            </div>
            <div className="text-xs text-semi-color-text-2">
              {t('home.health.uptime')} · <span className="mono">{overview?.Runtime.ApplicationVersion ?? '—'}</span>
            </div>
          </div>

          <div className="space-y-1">
            <div className="info-row">
              <span className="info-label">{t('home.health.database')}</span>
              <span className="info-value">
                {databases.length === 0 ? (
                  <StatusTag ok={false} unknown />
                ) : (
                  <Space spacing="tight">
                    {databases.map((db) => (
                      <Tag key={db.Name} size="small" color={db.Connected ? 'green' : 'red'}>
                        {db.Name}
                      </Tag>
                    ))}
                  </Space>
                )}
              </span>
            </div>
            <div className="info-row">
              <span className="info-label">{t('home.health.redis')}</span>
              <span className="info-value">
                <StatusTag ok={Boolean(redis?.Connected)} unknown={!redis} />
              </span>
            </div>
            <div className="info-row">
              <span className="info-label">{t('home.health.scheduler')}</span>
              <span className="info-value">
                <StatusTag ok={scheduler !== null && scheduler.State === 'Started'} unknown={!scheduler} />
                {scheduler && (
                  <span className="ml-2 text-xs text-semi-color-text-2">
                    {t('home.health.jobsRunning', { count: scheduler.JobCount })}
                  </span>
                )}
              </span>
            </div>
            <div className="info-row">
              <span className="info-label">{t('home.health.cpu')}</span>
              <span className="info-value tabular">
                {overview ? `${overview.Resource.ProcessCpuPercent.toFixed(1)} %` : '—'}
              </span>
            </div>
            <div className="info-row">
              <span className="info-label">{t('home.health.memory')}</span>
              <span className="info-value tabular">{formatBytes(overview?.Resource.WorkingSetBytes)}</span>
            </div>
          </div>

          {databases
            .filter((db) => db.Error)
            .map((db) => (
              <div key={db.Name} className="mt-2 text-xs text-semi-color-danger">
                {t('home.health.probeError', { name: db.Name, error: db.Error ?? '' })}
              </div>
            ))}
        </Card>

        <Card title={t('home.release.title')} className="xl:col-span-1">
          {!release ? (
            <Empty description={t('home.release.none')} />
          ) : (
            <>
              <div className="mb-3 text-[22px] font-semibold leading-8 mono">{release.Version}</div>
              <div className="info-row">
                <span className="info-label">{t('home.release.publishedAt')}</span>
                <span className="info-value">{formatDateTime(release.PublishedAt)}</span>
              </div>
              <div className="info-row">
                <span className="info-label">{t('home.release.publishedBy')}</span>
                <span className="info-value">{release.PublishedByUserName ?? '—'}</span>
              </div>
              <div className="info-row">
                <span className="info-label">{t('home.release.mirrors')}</span>
                <span className="info-value tabular">{release.Mirrors.length}</span>
              </div>
              <div className="info-row">
                <span className="info-label">{t('home.release.minimumVersion')}</span>
                <span className="info-value mono">{release.MinimumVersion ?? '—'}</span>
              </div>
              <Button className="mt-3 block w-full" onClick={() => navigate('/release')}>
                {t('home.release.go')}
              </Button>
            </>
          )}
        </Card>
      </div>

      <div className="grid grid-cols-1 gap-4 xl:grid-cols-3">
        <Card title={t('home.pending.title')} className="xl:col-span-1">
          <div className="info-row">
            <span className="info-label">{t('home.pending.license')}</span>
            <span className="info-value">
              {data && data.pendingLicenses > 0 ? (
                <Space spacing="tight">
                  <Tag size="small" color="orange">
                    {t('home.pending.licenseCount', { count: data.pendingLicenses })}
                  </Tag>
                  <Button size="small" theme="borderless" type="primary" onClick={() => navigate('/license')}>
                    {t('home.pending.view')}
                  </Button>
                </Space>
              ) : (
                <span className="text-semi-color-text-2">{t('home.pending.licenseNone')}</span>
              )}
            </span>
          </div>

          <Typography.Text type="tertiary" className="mb-2 mt-4 block !text-xs">
            {t('home.quick.title')}
          </Typography.Text>
          <Space spacing="tight" wrap>
            {navItems
              .filter((item) => item.path !== '/')
              .slice(0, 8)
              .map((item) => (
                <Button key={item.path} size="small" onClick={() => navigate(item.path)}>
                  {t(item.navKey)}
                </Button>
              ))}
          </Space>
        </Card>

        <Card title={t('home.audit.title')} className="xl:col-span-2">
          <Table
            size="small"
            rowKey="Id"
            pagination={false}
            dataSource={data?.audits ?? []}
            columns={[
              { title: t('home.audit.operator'), dataIndex: 'AdminUserName', width: 180, render: (value) => value ?? '—' },
              { title: t('home.audit.action'), dataIndex: 'Action', minWidth: 200 },
              {
                title: t('home.audit.result'),
                dataIndex: 'Success',
                width: 90,
                render: (value: boolean) => (
                  <Tag size="small" color={value ? 'green' : 'red'}>
                    {value ? t('home.audit.success') : t('home.audit.failure')}
                  </Tag>
                ),
              },
              { title: t('home.audit.time'), dataIndex: 'Timestamp', width: 170, render: (value) => formatDateTime(value) },
            ]}
          />
          <div className="mt-3 text-right">
            <Button size="small" theme="borderless" type="primary" onClick={() => navigate('/audit')}>
              {t('home.audit.viewAll')}
            </Button>
          </div>
        </Card>
      </div>
    </div>
  );
}
