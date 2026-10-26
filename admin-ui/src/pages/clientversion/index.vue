<script setup lang="ts">
import { MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, ref } from 'vue';

import { getClientVersionList } from '@/api/service';
import { t } from '@/locales';

const loading = ref(false);
const rows = ref<any[]>([]);
const totalDevices = ref(0);

const columns = computed(() => [
  { colKey: 'Version', title: t('pages.clientversion.list.colVersion'), width: 160 },
  { colKey: 'DeviceCount', title: t('pages.clientversion.list.colDevices'), width: 100 },
  { colKey: 'ratio', title: t('pages.clientversion.list.colRatio'), minWidth: 220 },
  { colKey: 'FirstSeenAt', title: t('pages.clientversion.list.colFirstSeen'), width: 190 },
  { colKey: 'LastSeenAt', title: t('pages.clientversion.list.colLastSeen'), width: 190 },
]);

function share(deviceCount: number) {
  return totalDevices.value > 0 ? Math.round((deviceCount / totalDevices.value) * 1000) / 10 : 0;
}

// 服务端存的是 unix 秒
function fmtTime(seconds?: number) {
  if (!seconds) return '—';
  const d = new Date(seconds * 1000);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('zh-CN');
}

async function load() {
  loading.value = true;
  try {
    const res: any = await getClientVersionList();
    const d = res.data ?? {};
    rows.value = d.Versions ?? [];
    totalDevices.value = d.TotalDevices ?? 0;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.clientversion.messages.listFail'));
  } finally {
    loading.value = false;
  }
}

onMounted(load);
</script>

<template>
  <div class="hutao-page">
    <t-card :title="$t('pages.clientversion.title')" :bordered="false">
      <template #actions>
        <t-button variant="outline" size="small" :loading="loading" @click="load">
          {{ $t('pages.clientversion.refresh') }}
        </t-button>
      </template>

      <p class="tip">
        {{ $t('pages.clientversion.tipPre') }}<code>client_versions</code>{{ $t('pages.clientversion.tipSuf') }}
      </p>

      <t-descriptions :column="2" layout="horizontal" size="small" class="summary">
        <t-descriptions-item :label="$t('pages.clientversion.totalDevices')">{{ totalDevices }}</t-descriptions-item>
        <t-descriptions-item :label="$t('pages.clientversion.versionCount')">{{ rows.length }}</t-descriptions-item>
      </t-descriptions>

      <t-table row-key="Version" :data="rows" :columns="columns" :loading="loading" size="small">
        <template #empty>{{ $t('pages.clientversion.list.empty') }}</template>
        <template #Version="{ row }">
          <span class="mono">{{ row.Version }}</span>
        </template>
        <template #ratio="{ row }">
          <t-progress class="share-bar" :percentage="share(row.DeviceCount)" size="small" />
        </template>
        <template #FirstSeenAt="{ row }">{{ fmtTime(row.FirstSeenAt) }}</template>
        <template #LastSeenAt="{ row }">{{ fmtTime(row.LastSeenAt) }}</template>
      </t-table>
    </t-card>
  </div>
</template>

<style lang="less" scoped>
.tip {
  margin-bottom: 16px;
  color: var(--td-text-color-secondary);
  font-size: 13px;
}
.summary {
  margin-bottom: 16px;
}
.mono {
  font-family: 'Cascadia Code', 'Fira Code', Consolas, monospace;
}
.share-bar {
  width: 100%;
}
</style>
