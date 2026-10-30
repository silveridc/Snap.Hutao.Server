<script setup lang="ts">
import { AddIcon, DeleteIcon } from 'tdesign-icons-vue-next';
import { MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, ref } from 'vue';

import { getWallpaperPool, saveWallpaperPool } from '@/api/service';
import { t } from '@/locales';

interface WallpaperPoolItem {
  Url: string;
  SourceUrl: string;
  Author: string;
  Uploader: string;
}

interface WallpaperPoolRow extends WallpaperPoolItem {
  Key: number;
}

const MAX_POOL_SIZE = 50;

const loading = ref(false);
const saving = ref(false);
const rows = ref<WallpaperPoolRow[]>([]);
let nextKey = 1;

const columns = computed(() => [
  { colKey: 'Url', title: t('pages.wallpaper.columns.url'), width: 360 },
  { colKey: 'SourceUrl', title: t('pages.wallpaper.columns.sourceUrl') },
  { colKey: 'Author', title: t('pages.wallpaper.columns.author'), width: 150 },
  { colKey: 'Uploader', title: t('pages.wallpaper.columns.uploader'), width: 150 },
  { colKey: 'op', title: t('pages.wallpaper.columns.op'), width: 80, fixed: 'right' },
]);

function createRow(item: Partial<WallpaperPoolItem> = {}): WallpaperPoolRow {
  return {
    Key: nextKey++,
    Url: item.Url ?? '',
    SourceUrl: item.SourceUrl ?? '',
    Author: item.Author ?? '',
    Uploader: item.Uploader ?? '',
  };
}

function addRow() {
  if (rows.value.length >= MAX_POOL_SIZE) {
    MessagePlugin.warning(t('pages.wallpaper.messages.tooMany', { max: MAX_POOL_SIZE }));
    return;
  }
  rows.value.push(createRow());
}

function removeRow(row: WallpaperPoolRow) {
  rows.value = rows.value.filter((item) => item !== row);
}

function isHttpsUrl(value: string) {
  try {
    return new URL(value).protocol === 'https:';
  } catch {
    return false;
  }
}

async function load() {
  loading.value = true;
  try {
    const res: any = await getWallpaperPool();
    rows.value = (res.data ?? []).map((item: WallpaperPoolItem) => createRow(item));
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.wallpaper.messages.loadFail'));
  } finally {
    loading.value = false;
  }
}

async function save() {
  for (let i = 0; i < rows.value.length; i++) {
    const item = rows.value[i];
    if (!item.Url.trim()) {
      MessagePlugin.warning(t('pages.wallpaper.messages.urlRequired', { n: i + 1 }));
      return;
    }
    if (!isHttpsUrl(item.Url.trim())) {
      MessagePlugin.warning(t('pages.wallpaper.messages.urlInvalid', { n: i + 1 }));
      return;
    }
    if (item.SourceUrl.trim() && !isHttpsUrl(item.SourceUrl.trim())) {
      MessagePlugin.warning(t('pages.wallpaper.messages.sourceUrlInvalid', { n: i + 1 }));
      return;
    }
  }

  saving.value = true;
  try {
    const payload = rows.value.map((item) => ({
      Url: item.Url.trim(),
      SourceUrl: item.SourceUrl.trim() || null,
      Author: item.Author.trim() || null,
      Uploader: item.Uploader.trim() || null,
    }));
    const res: any = await saveWallpaperPool(payload);
    MessagePlugin.success(res.message || t('pages.wallpaper.messages.saveSuccess'));
    await load();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.wallpaper.messages.saveFail'));
  } finally {
    saving.value = false;
  }
}

onMounted(load);
</script>

<template>
  <div class="hutao-page">
    <t-card :title="$t('pages.wallpaper.title')" :bordered="false" class="hutao-card">
      <template #subtitle>
        <span class="section-desc">{{ $t('pages.wallpaper.description') }}</span>
      </template>
      <template #actions>
        <t-space>
          <t-button variant="outline" size="small" :loading="loading" @click="load">{{ $t('pages.wallpaper.reload') }}</t-button>
          <t-button variant="outline" size="small" :disabled="rows.length >= MAX_POOL_SIZE" @click="addRow">
            <template #icon><add-icon /></template>
            {{ $t('pages.wallpaper.add') }}
          </t-button>
          <t-button theme="primary" size="small" :loading="saving" @click="save">{{ $t('pages.wallpaper.save') }}</t-button>
        </t-space>
      </template>

      <t-table
        row-key="Key"
        :data="rows"
        :columns="columns"
        :loading="loading"
        :empty="$t('pages.wallpaper.empty')"
        size="small"
      >
        <template #Url="{ row }">
          <t-input v-model="row.Url" :placeholder="$t('pages.wallpaper.placeholders.url')" clearable />
        </template>
        <template #SourceUrl="{ row }">
          <t-input v-model="row.SourceUrl" :placeholder="$t('pages.wallpaper.placeholders.sourceUrl')" clearable />
        </template>
        <template #Author="{ row }">
          <t-input v-model="row.Author" :placeholder="$t('pages.wallpaper.placeholders.author')" clearable />
        </template>
        <template #Uploader="{ row }">
          <t-input v-model="row.Uploader" :placeholder="$t('pages.wallpaper.placeholders.uploader')" clearable />
        </template>
        <template #op="{ row }">
          <t-button theme="danger" variant="text" size="small" @click="removeRow(row)">
            <delete-icon />
          </t-button>
        </template>
      </t-table>

      <p class="tip">{{ $t('pages.wallpaper.hint') }}</p>
    </t-card>
  </div>
</template>

<style lang="less" scoped>
.section-desc {
  color: var(--td-text-color-placeholder);
  font-size: 12px;
}
.tip {
  margin: 12px 0 0;
  color: var(--td-text-color-placeholder);
  font-size: 12px;
  line-height: 18px;
}
</style>
