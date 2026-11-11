import { useMemo, useState } from 'react';

import { Banner, Card, Empty, Progress, Radio, RadioGroup, Table, Tag } from '@douyinfe/semi-ui';

import { getAbyssAnalytics, getDamageAnalytics, getGachaAnalytics } from '@/api/service';
import { t, useLocale } from '@/i18n';
import { formatNumber } from '@/lib/format';
import { useQuery } from '@/hooks/useQuery';

const DAY_OPTIONS = [7, 30, 90, 0];

const POOL_KEYS: Record<number, string> = {
  100: '100',
  200: '200',
  301: '301',
  302: '302',
  400: '400',
  500: '500',
};

function poolName(type: number): string {
  const key = POOL_KEYS[type];
  return key ? t(`analytics.gacha.pool.${key}`) : String(type);
}

function Fact({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="min-w-0">
      <div className="text-[22px] font-semibold leading-tight tabular">{value}</div>
      <div className="mt-1 text-xs text-[var(--semi-color-text-2)]">{label}</div>
    </div>
  );
}

function fmtInt(value?: number | null): string {
  return value === null || value === undefined ? '—' : formatNumber(value);
}

function fmtPercent(value?: number | null): string {
  return value === null || value === undefined ? '—' : `${(value * 100).toFixed(2)}%`;
}

function fmtNumber(value?: number | null, digits = 2): string {
  return value === null || value === undefined ? '—' : Number(value).toFixed(digits);
}

function BarRow({ name, count, max, danger = false }: { name: string; count: number; max: number; danger?: boolean }) {
  return (
    <div className="grid grid-cols-[96px_1fr_64px] items-center gap-3 py-1 text-xs">
      <span className="truncate font-mono text-[var(--semi-color-text-2)]">{name}</span>
      <div className="h-1.5 overflow-hidden rounded-sm bg-[var(--semi-color-fill-0)]">
        <div
          className={`h-full rounded-sm ${danger ? 'bg-[var(--semi-color-danger)]' : 'bg-[var(--semi-color-primary)]'}`}
          style={{ width: `${Math.max(4, (count / max) * 100)}%` }}
        />
      </div>
      <span className="text-right tabular">{fmtInt(count)}</span>
    </div>
  );
}

export default function AnalyticsPage() {
  useLocale();

  const [days, setDays] = useState(30);
  const query = useQuery(
    () => Promise.all([getGachaAnalytics(days), getAbyssAnalytics(), getDamageAnalytics(10)]).then(([gacha, abyss, damage]) => ({ gacha, abyss, damage })),
    [days],
  );

  const gacha = query.data?.gacha;
  const abyss = query.data?.abyss;
  const damage = query.data?.damage;
  const spiral = abyss?.SpiralAbyss;
  const theater = abyss?.Theater;

  const gachaTop = gacha?.TopStar5Items ?? [];
  const maxItemCount = Math.max(1, ...gachaTop.map((item) => item.Count));
  const damageTop = damage?.TopAvatars ?? [];
  const maxDamage = Math.max(1, ...damageTop.map((item) => item.TotalDamage));

  const star5Rate = useMemo(
    () => (gacha?.TotalPulls ? fmtPercent(gacha.TotalStar5 / gacha.TotalPulls) : '—'),
    [gacha?.TotalPulls, gacha?.TotalStar5],
  );

  const gachaColumns = [
    { title: t('analytics.gacha.colPool'), dataIndex: 'GachaType', width: 160, render: (value: number) => poolName(value) },
    { title: t('analytics.gacha.colPulls'), dataIndex: 'Pulls', width: 120, align: 'right' as const, render: fmtInt },
    { title: t('analytics.gacha.colStar5'), dataIndex: 'Star5', width: 110, align: 'right' as const, render: fmtInt },
    { title: t('analytics.gacha.colRate'), dataIndex: 'Star5Rate', width: 120, align: 'right' as const, render: fmtPercent },
    { title: t('analytics.gacha.colAverage'), dataIndex: 'AveragePullsPerStar5', minWidth: 140, align: 'right' as const, render: (value: number) => fmtNumber(value) },
  ];

  const floorColumns = [
    { title: t('analytics.abyss.colFloor'), dataIndex: 'Index', width: 100 },
    { title: t('analytics.abyss.colRecords'), dataIndex: 'Records', width: 120, align: 'right' as const, render: fmtInt },
    { title: t('analytics.abyss.colAverageStar'), dataIndex: 'AverageStar', minWidth: 140, align: 'right' as const, render: (value: number) => fmtNumber(value) },
  ];

  const damageColumns = [
    { title: t('analytics.damage.colRank'), dataIndex: 'AvatarId', key: 'rank', width: 70, render: (_value: unknown, _record: unknown, rowIndex: number) => rowIndex + 1 },
    { title: t('analytics.damage.colAvatar'), dataIndex: 'AvatarId', width: 120, render: (value: number) => <span className="font-mono text-xs">{value}</span> },
    { title: t('analytics.damage.colDamage'), dataIndex: 'TotalDamage', minWidth: 160, align: 'right' as const, render: fmtInt },
    { title: t('analytics.damage.colShare'), dataIndex: 'Share', width: 100, align: 'right' as const, render: fmtPercent },
    {
      title: '',
      dataIndex: 'TotalDamage',
      key: 'bar',
      minWidth: 260,
      render: (value: number) => <Progress percent={Math.max(4, (value / maxDamage) * 100)} showInfo={false} stroke="var(--semi-color-danger)" strokeWidth={6} aria-label="damage" className="w-full" />,
    },
  ];

  const noData = t('analytics.messages.noData');

  return (
    <div className="space-y-4">
      {query.error && <Banner type="danger" closeIcon={null} description={query.error.message} />}

      <Card
        title={t('analytics.gacha.title')}
        headerExtraContent={
          <RadioGroup type="button" buttonSize="small" value={days} onChange={(event) => setDays(Number(event.target.value))}>
            {DAY_OPTIONS.map((option) => (
              <Radio key={option} value={option}>
                {option === 0 ? t('analytics.gacha.daysAll') : t(`analytics.gacha.days${option}`)}
              </Radio>
            ))}
          </RadioGroup>
        }
        loading={query.loading}
      >
        <div className="grid grid-cols-2 gap-3 py-1 sm:grid-cols-4">
          <Fact label={t('analytics.gacha.totalPulls')} value={fmtInt(gacha?.TotalPulls)} />
          <Fact label={t('analytics.gacha.totalStar5')} value={fmtInt(gacha?.TotalStar5)} />
          <Fact label={t('analytics.gacha.star5Rate')} value={star5Rate} />
          <Fact label={t('analytics.gacha.untrusted')} value={fmtInt(gacha?.UntrustedPulls)} />
        </div>

        <Table
          size="small"
          rowKey="GachaType"
          dataSource={gacha?.Pools ?? []}
          columns={gachaColumns}
          pagination={false}
          empty={noData}
          className="mt-2"
        />

        {gachaTop.length > 0 && (
          <div className="pt-2">
            <div className="mb-2 mt-4 text-[13px] font-semibold">{t('analytics.gacha.topItems')}</div>
            {gachaTop.map((item) => (
              <BarRow key={item.ItemId} name={String(item.ItemId)} count={item.Count} max={maxItemCount} />
            ))}
          </div>
        )}
      </Card>

      <Card title={t('analytics.abyss.title')} loading={query.loading}>
        <div className="grid gap-4 lg:grid-cols-2">
          <Card title={t('analytics.abyss.spiral')} className="bg-[var(--semi-color-fill-0)]">
            <div className="grid grid-cols-3 gap-3 py-1">
              <Fact label={t('analytics.abyss.records')} value={fmtInt(spiral?.Records)} />
              <Fact label={t('analytics.abyss.uniqueUids')} value={fmtInt(spiral?.UniqueUids)} />
              <Fact label={t('analytics.abyss.fullStar')} value={fmtInt(spiral?.FullStarRecords)} />
            </div>
            <Table
              size="small"
              rowKey="Index"
              dataSource={spiral?.Floors ?? []}
              columns={floorColumns}
              pagination={false}
              empty={noData}
            />
          </Card>

          <Card title={t('analytics.abyss.theater')} className="bg-[var(--semi-color-fill-0)]">
            <div className="grid grid-cols-2 gap-3 py-1">
              <Fact label={t('analytics.abyss.records')} value={fmtInt(theater?.Records)} />
              <Fact label={t('analytics.abyss.uniqueUids')} value={fmtInt(theater?.UniqueUids)} />
            </div>
            <div className="mb-2 mt-4 text-[13px] font-semibold">{t('analytics.abyss.topAvatars')}</div>
            {(theater?.TopAvatars ?? []).length > 0 ? (
              theater!.TopAvatars.map((avatar) => (
                <div key={avatar.AvatarId} className="flex items-center justify-between border-b border-dashed border-[var(--semi-color-border)] py-1 text-xs last:border-b-0">
                  <span className="font-mono">{avatar.AvatarId}</span>
                  <span className="tabular">{fmtInt(avatar.Count)}</span>
                </div>
              ))
            ) : (
              <Empty description={noData} />
            )}
          </Card>
        </div>

        <div className="mb-2 mt-4 text-[13px] font-semibold">{t('analytics.abyss.spiralTop')}</div>
        <div className="flex flex-wrap gap-2">
          {(spiral?.TopAvatars ?? []).length > 0 ? (
            spiral!.TopAvatars.map((avatar) => (
              <Tag key={avatar.AvatarId} type="light" color="blue">
                {avatar.AvatarId} · {fmtInt(avatar.Count)}
              </Tag>
            ))
          ) : (
            <span className="text-xs text-[var(--semi-color-text-2)]">{noData}</span>
          )}
        </div>
      </Card>

      <Card title={t('analytics.damage.title')} loading={query.loading}>
        <p className="mb-3 text-[13px] text-[var(--semi-color-text-2)]">{t('analytics.damage.tip')}</p>
        <div className="grid grid-cols-2 gap-3 py-1">
          <Fact label={t('analytics.damage.totalDamage')} value={fmtInt(damage?.TotalDamage)} />
          <Fact label={t('analytics.damage.samples')} value={fmtInt(damage?.SampleRecords)} />
        </div>
        <Table
          size="small"
          rowKey="AvatarId"
          dataSource={damageTop}
          columns={damageColumns}
          pagination={false}
          empty={noData}
          className="mt-2"
        />
      </Card>
    </div>
  );
}
