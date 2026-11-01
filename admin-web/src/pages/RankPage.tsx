import { useEffect, useState } from 'react';

import { Banner, Button, Card, InputNumber, Select, Table, Typography } from '@douyinfe/semi-ui';

import { getRankKeys, getRankLeaderboard } from '@/api/service';
import { t, useLocale } from '@/i18n';
import { useQuery } from '@/hooks/useQuery';
import type { RankEntry } from '@/types/api';

export default function RankPage() {
  useLocale();

  const keys = useQuery(() => getRankKeys(), []);
  const [selectedKey, setSelectedKey] = useState('');
  const [count, setCount] = useState(50);
  const [rows, setRows] = useState<RankEntry[]>();
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<Error>();

  const keyOptions = (keys.data ?? []).map((key) => ({ label: key, value: key }));

  // 首次拿到键列表时自动选默认榜并加载
  useEffect(() => {
    if (!selectedKey && keys.data?.length) {
      const initial = keys.data.find((key) => key === 'Hutao.Damage.Avatar.All') ?? keys.data[0];
      if (initial) {
        setSelectedKey(initial);
        loadBoard(initial);
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [keys.data]);

  async function loadBoard(key = selectedKey) {
    if (!key) {
      return;
    }
    setLoading(true);
    setError(undefined);
    try {
      setRows(await getRankLeaderboard(key, count));
    } catch (err) {
      setError(err instanceof Error ? err : new Error(String(err)));
    } finally {
      setLoading(false);
    }
  }

  async function selectKey(value: string) {
    setSelectedKey(value);
    await loadBoard(value);
  }

  const columns = [
    { title: '#', dataIndex: 'Uid', key: 'index', width: 70, render: (_value: unknown, _record: unknown, rowIndex: number) => rowIndex + 1 },
    { title: 'UID', dataIndex: 'Uid', width: 180, render: (value: string) => <span className="font-mono text-xs">{value}</span> },
    { title: t('rank.view.colValue'), dataIndex: 'Value', render: (value: number) => value.toLocaleString() },
  ];

  return (
    <div className="space-y-4">
      <Card title={t('rank.view.title')}>
        <Typography.Text type="tertiary" className="mb-4 block">
          {t('rank.view.tipPre')}<code>Hutao.*.Avatar.*</code>{t('rank.view.tipSuf')}
        </Typography.Text>

        <div className="mb-4 flex flex-wrap items-center gap-2">
          <Select
            className="w-80"
            placeholder={t('rank.view.selectPlaceholder')}
            loading={keys.loading}
            filter
            value={selectedKey || undefined}
            optionList={keyOptions}
            onChange={(value) => selectKey(String(value))}
          />
          <InputNumber value={count} min={1} max={500} style={{ width: 160 }} onChange={(value) => setCount(Number(value) || 1)} />
          <Button theme="solid" type="primary" loading={loading} onClick={() => (selectedKey ? loadBoard() : undefined)}>
            {t('rank.view.query')}
          </Button>
          <Button theme="light" loading={keys.loading} onClick={keys.reload}>
            {t('rank.view.refreshKeys')}
          </Button>
        </div>

        {error && <Banner type="danger" closeIcon={null} description={error.message} className="mb-3" />}

        <Table
          size="small"
          rowKey="Uid"
          loading={loading}
          dataSource={rows ?? []}
          columns={columns}
          pagination={false}
          maxHeight={560}
          empty={t('rank.messages.selectFirst')}
        />
      </Card>
    </div>
  );
}
