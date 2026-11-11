import { Banner, Button, Card, Progress, Table } from '@douyinfe/semi-ui';
import type { ColumnProps } from '@douyinfe/semi-ui/lib/es/table';

import { getClientVersionList } from '@/api/service';
import { t, useLocale } from '@/i18n';
import { formatNumber } from '@/lib/format';
import { useQuery } from '@/hooks/useQuery';
import type { ClientVersionCount } from '@/types/api';

// 服务端存的是 unix 秒
function fmtTime(seconds?: number): string {
  if (!seconds) {
    return '—';
  }
  const date = new Date(seconds * 1000);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString();
}

export default function ClientVersionPage() {
  useLocale();

  const list = useQuery(() => getClientVersionList(), []);
  const rows = list.data?.Versions ?? [];
  const totalDevices = list.data?.TotalDevices ?? 0;

  function share(deviceCount: number): number {
    return totalDevices > 0 ? Math.round((deviceCount / totalDevices) * 1000) / 10 : 0;
  }

  const columns: ColumnProps<ClientVersionCount>[] = [
    { title: t('clientversion.list.colVersion'), dataIndex: 'Version', width: 160, render: (value: string) => <span className="font-mono text-xs">{value}</span> },
    { title: t('clientversion.list.colDevices'), dataIndex: 'DeviceCount', width: 100, render: (value: number) => formatNumber(value) },
    {
      title: t('clientversion.list.colRatio'),
      dataIndex: 'DeviceCount',
      key: 'ratio',
      minWidth: 220,
      render: (value: number) => (
        <div className="flex items-center gap-2">
          <Progress percent={share(value)} showInfo={false} strokeWidth={6} aria-label="share" className="flex-1" />
          <span className="w-12 shrink-0 text-right text-xs tabular">{share(value)}%</span>
        </div>
      ),
    },
    { title: t('clientversion.list.colFirstSeen'), dataIndex: 'FirstSeenAt', width: 190, render: fmtTime },
    { title: t('clientversion.list.colLastSeen'), dataIndex: 'LastSeenAt', width: 190, render: fmtTime },
  ];

  return (
    <div className="space-y-4">
      <Card
        title={t('clientversion.title')}
        headerExtraContent={
          <Button size="small" loading={list.loading} onClick={list.reload}>
            {t('clientversion.refresh')}
          </Button>
        }
      >
        <p className="mb-4 text-[13px] text-[var(--semi-color-text-2)]">
          {t('clientversion.tipPre')}<code>client_versions</code>{t('clientversion.tipSuf')}
        </p>

        <div className="mb-4 flex gap-10 text-[13px]">
          <span>
            {t('clientversion.totalDevices')}
            <span className="ml-2 font-semibold tabular">{formatNumber(totalDevices)}</span>
          </span>
          <span>
            {t('clientversion.versionCount')}
            <span className="ml-2 font-semibold tabular">{rows.length}</span>
          </span>
        </div>

        {list.error && <Banner type="danger" closeIcon={null} description={list.error.message} className="mb-3" />}

        <Table
          size="small"
          rowKey="Version"
          loading={list.loading}
          dataSource={rows}
          columns={columns}
          pagination={false}
          empty={t('clientversion.list.empty')}
        />
      </Card>
    </div>
  );
}
