import { useState } from 'react';

import { Banner, Button, Card, Input, Modal, Space, Table, TagInput, Toast } from '@douyinfe/semi-ui';
import type { ColumnProps } from '@douyinfe/semi-ui/lib/es/table';

import { invalidateCache, listCacheKeys } from '@/api/service';
import { t, useLocale } from '@/i18n';
import { useQuery } from '@/hooks/useQuery';

const PRESETS: Array<{ labelKey: string; pattern: string }> = [
  { labelKey: 'cache.presets.abyssStats', pattern: '*Statistics*' },
  { labelKey: 'cache.presets.gachaDistribution', pattern: '*GachaDistribution*' },
  { labelKey: 'cache.presets.gachaEventStats', pattern: 'GachaEventStatistics' },
  { labelKey: 'cache.presets.theaterStats', pattern: 'RoleCombatStatistics:*' },
  { labelKey: 'cache.presets.enka', pattern: 'Enka:*' },
  { labelKey: 'cache.presets.wallpaper', pattern: '*Wallpaper*' },
  { labelKey: 'cache.presets.announcements', pattern: 'Announcements:*' },
  { labelKey: 'cache.presets.maintainer', pattern: 'User:IsMaintainer:*' },
  { labelKey: 'cache.presets.userStats', pattern: 'Admin:UserStatistics' },
];

export default function CachePage() {
  useLocale();

  const [customKeys, setCustomKeys] = useState<string[]>([]);
  const [customPatterns, setCustomPatterns] = useState<string[]>([]);
  const [invalidating, setInvalidating] = useState(false);
  const [lastResult, setLastResult] = useState('');

  const [browsePattern, setBrowsePattern] = useState('*');
  const [appliedPattern, setAppliedPattern] = useState('*');
  const [selected, setSelected] = useState<string[]>([]);
  const keyList = useQuery(() => listCacheKeys(appliedPattern || '*', 500), [appliedPattern]);

  async function runInvalidate(payload: { Keys?: string[]; Patterns?: string[] }) {
    setInvalidating(true);
    setLastResult('');
    try {
      const result = await invalidateCache({ Keys: payload.Keys ?? [], Patterns: payload.Patterns ?? [] });
      setLastResult(t('cache.messages.result', { keys: result?.RemovedKeys ?? 0, patterns: result?.RemovedByPattern ?? 0 }));
      Toast.success(t('cache.messages.invalidateSuccess'));
      if (appliedPattern) {
        keyList.reload();
      }
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setInvalidating(false);
    }
  }

  function invalidateAll() {
    Modal.confirm({
      title: t('cache.dialog.clearAllHeader'),
      content: t('cache.dialog.clearAllBody'),
      okText: t('cache.dialog.clearAllConfirm'),
      cancelText: t('common.cancel'),
      onOk: () => runInvalidate({ Patterns: ['*'] }),
    });
  }

  function submitCustom() {
    if (!customKeys.length && !customPatterns.length) {
      Toast.warning(t('cache.messages.emptyInput'));
      return;
    }
    runInvalidate({ Keys: [...customKeys], Patterns: [...customPatterns] });
  }

  function queryKeys() {
    setAppliedPattern(browsePattern || '*');
    setSelected([]);
  }

  function expireSelected() {
    if (!selected.length) {
      Toast.warning(t('cache.messages.selectFirst'));
      return;
    }
    runInvalidate({ Keys: [...selected] });
  }

  const keyColumns: ColumnProps<{ key: string }>[] = [
    { title: t('cache.browse.colKey'), dataIndex: 'key', render: (value: string) => <span className="font-mono text-xs">{value}</span> },
  ];

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 gap-4 xl:grid-cols-12">
        <Card className="xl:col-span-7" title={t('cache.quick.title')}>
          <p className="mb-4 text-[13px] text-[var(--semi-color-text-2)]">
            {t('cache.quick.tipPre')}
            <code className="mx-1 rounded bg-[var(--semi-color-fill-0)] px-1 py-0.5 font-mono text-xs">POST /Service/Cache/Invalidate</code>
            {t('cache.quick.tipSuf')}
          </p>

          <Space wrap>
            {PRESETS.map((preset) => (
              <Button key={preset.pattern} type="tertiary" loading={invalidating} onClick={() => runInvalidate({ Patterns: [preset.pattern] })}>
                {t(preset.labelKey)}
              </Button>
            ))}
            <Button type="danger" theme="light" loading={invalidating} onClick={invalidateAll}>
              {t('cache.quick.invalidateAll')}
            </Button>
          </Space>

          {lastResult && <Banner type="success" closeIcon={null} description={lastResult} className="mt-3" />}
        </Card>

        <Card className="xl:col-span-5" title={t('cache.custom.title')}>
          <div className="space-y-4">
            <div>
              <div className="mb-2 text-[13px]">{t('cache.custom.keysLabel')}</div>
              <TagInput value={customKeys} placeholder={t('cache.custom.keysPlaceholder')} showClear onChange={(value) => setCustomKeys(value ?? [])} />
            </div>
            <div>
              <div className="mb-2 text-[13px]">{t('cache.custom.patternsLabel')}</div>
              <TagInput value={customPatterns} placeholder={t('cache.custom.patternsPlaceholder')} showClear onChange={(value) => setCustomPatterns(value ?? [])} />
            </div>
            <Button theme="solid" loading={invalidating} onClick={submitCustom}>
              {t('cache.custom.submit')}
            </Button>
          </div>
        </Card>
      </div>

      <Card title={t('cache.browse.title')}>
        <Space className="mb-4">
          <Input
            value={browsePattern}
            placeholder={t('cache.browse.patternPlaceholder')}
            style={{ width: 320 }}
            showClear
            onChange={setBrowsePattern}
            onEnterPress={queryKeys}
          />
          <Button theme="solid" loading={keyList.loading} onClick={queryKeys}>
            {t('cache.browse.query')}
          </Button>
          <Button type="danger" theme="light" disabled={!selected.length} loading={invalidating} onClick={expireSelected}>
            {t('cache.browse.expireSelected', { count: selected.length })}
          </Button>
        </Space>

        {keyList.error && <Banner type="danger" closeIcon={null} description={keyList.error.message} className="mb-3" />}

        <Table
          size="small"
          rowKey="key"
          loading={keyList.loading}
          dataSource={(keyList.data ?? []).map((key) => ({ key }))}
          columns={keyColumns}
          rowSelection={{
            selectedRowKeys: selected,
            onChange: (rowKeys?: Array<string | number>) => setSelected((rowKeys ?? []).map((key) => String(key))),
          }}
          pagination={false}
          maxHeight={480}
        />
      </Card>
    </div>
  );
}
