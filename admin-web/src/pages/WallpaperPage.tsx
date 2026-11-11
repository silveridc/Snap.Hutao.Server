import { useState } from 'react';

import { IconPlus } from '@douyinfe/semi-icons';
import { Button, Card, Input, Table, Toast } from '@douyinfe/semi-ui';
import type { ColumnProps } from '@douyinfe/semi-ui/lib/es/table';

import { getWallpaperPool, saveWallpaperPool } from '@/api/service';
import { t, useLocale } from '@/i18n';
import { useQuery } from '@/hooks/useQuery';

const MAX_POOL_SIZE = 50;

interface WallpaperPoolRow {
  Key: number;
  Url: string;
  SourceUrl: string;
  Author: string;
  Uploader: string;
}

function isHttpsUrl(value: string): boolean {
  try {
    return new URL(value).protocol === 'https:';
  } catch {
    return false;
  }
}

export default function WallpaperPage() {
  useLocale();

  const [rows, setRows] = useState<WallpaperPoolRow[]>([]);
  const [saving, setSaving] = useState(false);
  const list = useQuery(async () => {
    const items = await getWallpaperPool();
    setRows(
      items.map((item, index) => ({
        Key: index,
        Url: item.Url ?? '',
        SourceUrl: item.SourceUrl ?? '',
        Author: item.Author ?? '',
        Uploader: item.Uploader ?? '',
      })),
    );
    return items;
  }, []);

  function updateRow(key: number, patch: Partial<WallpaperPoolRow>) {
    setRows((prev) => prev.map((row) => (row.Key === key ? { ...row, ...patch } : row)));
  }

  function addRow() {
    if (rows.length >= MAX_POOL_SIZE) {
      Toast.warning(t('wallpaper.messages.tooMany', { max: MAX_POOL_SIZE }));
      return;
    }
    setRows((prev) => [...prev, { Key: Date.now(), Url: '', SourceUrl: '', Author: '', Uploader: '' }]);
  }

  function removeRow(key: number) {
    setRows((prev) => prev.filter((row) => row.Key !== key));
  }

  async function save() {
    for (const [index, item] of rows.entries()) {
      if (!item.Url.trim()) {
        Toast.warning(t('wallpaper.messages.urlRequired', { n: index + 1 }));
        return;
      }
      if (!isHttpsUrl(item.Url.trim())) {
        Toast.warning(t('wallpaper.messages.urlInvalid', { n: index + 1 }));
        return;
      }
      if (item.SourceUrl.trim() && !isHttpsUrl(item.SourceUrl.trim())) {
        Toast.warning(t('wallpaper.messages.sourceUrlInvalid', { n: index + 1 }));
        return;
      }
    }

    setSaving(true);
    try {
      const payload = rows.map((item) => ({
        Url: item.Url.trim(),
        SourceUrl: item.SourceUrl.trim() || null,
        Author: item.Author.trim() || null,
        Uploader: item.Uploader.trim() || null,
      }));
      const message = await saveWallpaperPool(payload);
      Toast.success(message || t('wallpaper.messages.saveSuccess'));
      list.reload();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setSaving(false);
    }
  }

  const columns: ColumnProps<WallpaperPoolRow>[] = [
    {
      title: t('wallpaper.columns.url'),
      dataIndex: 'Url',
      width: 360,
      render: (_v, row) => (
        <Input value={row.Url} placeholder={t('wallpaper.placeholders.url')} onChange={(value) => updateRow(row.Key, { Url: value })} />
      ),
    },
    {
      title: t('wallpaper.columns.sourceUrl'),
      dataIndex: 'SourceUrl',
      render: (_v, row) => (
        <Input value={row.SourceUrl} placeholder={t('wallpaper.placeholders.sourceUrl')} onChange={(value) => updateRow(row.Key, { SourceUrl: value })} />
      ),
    },
    {
      title: t('wallpaper.columns.author'),
      dataIndex: 'Author',
      width: 160,
      render: (_v, row) => (
        <Input value={row.Author} placeholder={t('wallpaper.placeholders.author')} onChange={(value) => updateRow(row.Key, { Author: value })} />
      ),
    },
    {
      title: t('wallpaper.columns.uploader'),
      dataIndex: 'Uploader',
      width: 160,
      render: (_v, row) => (
        <Input value={row.Uploader} placeholder={t('wallpaper.placeholders.uploader')} onChange={(value) => updateRow(row.Key, { Uploader: value })} />
      ),
    },
    {
      title: t('wallpaper.columns.op'),
      key: 'op',
      width: 90,
      render: (_v, row) => (
        <Button theme="borderless" type="danger" size="small" onClick={() => removeRow(row.Key)}>
          {t('common.delete')}
        </Button>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <Card
        title={t('wallpaper.title')}
        headerLine
        headerExtraContent={
          <div className="flex items-center gap-2">
            <Button size="small" type="tertiary" loading={list.loading} onClick={list.reload}>
              {t('wallpaper.reload')}
            </Button>
            <Button size="small" type="tertiary" icon={<IconPlus />} disabled={rows.length >= MAX_POOL_SIZE} onClick={addRow}>
              {t('wallpaper.add')}
            </Button>
            <Button size="small" theme="solid" loading={saving} onClick={save}>
              {t('wallpaper.save')}
            </Button>
          </div>
        }
      >
        <p className="mb-4 text-[13px] text-[var(--semi-color-text-2)]">{t('wallpaper.description')}</p>

        <Table
          size="small"
          rowKey="Key"
          loading={list.loading}
          dataSource={rows}
          columns={columns}
          pagination={false}
          empty={t('wallpaper.empty')}
        />

        <p className="mt-3 text-xs leading-[18px] text-[var(--semi-color-text-2)]">{t('wallpaper.hint')}</p>
      </Card>
    </div>
  );
}
