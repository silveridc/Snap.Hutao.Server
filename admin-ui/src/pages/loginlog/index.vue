<script setup lang="ts">
import { MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, reactive, ref } from 'vue';

import { getLoginLogs } from '@/api/service';
import { i18n, t } from '@/locales';

const loading = ref(false);
const rows = ref<any[]>([]);
const pagination = reactive({ current: 1, pageSize: 20, total: 0 });

const filters = reactive({
  keyword: '',
  success: '' as '' | 'true' | 'false',
});

const resultOptions = computed(() => [
  { label: t('pages.loginlog.filters.all'), value: '' },
  { label: t('pages.loginlog.filters.success'), value: 'true' },
  { label: t('pages.loginlog.filters.failure'), value: 'false' },
]);

const columns = computed(() => [
  { colKey: 'LoginAt', title: t('pages.loginlog.columns.time'), width: 170 },
  { colKey: 'UserName', title: t('pages.loginlog.columns.user'), width: 220, ellipsis: true },
  { colKey: 'Method', title: t('pages.loginlog.columns.method'), width: 90 },
  { colKey: 'Success', title: t('pages.loginlog.columns.result'), width: 80 },
  { colKey: 'Ip', title: t('pages.loginlog.columns.ip'), width: 140 },
  { colKey: 'IpRegion', title: t('pages.loginlog.columns.region'), width: 170, ellipsis: true },
  { colKey: 'device', title: t('pages.loginlog.columns.device'), width: 220, ellipsis: true },
  { colKey: 'FailureKey', title: t('pages.loginlog.columns.reason'), ellipsis: true, minWidth: 160 },
]);

// 属地是 IP2Region 的四段数组（国家/省份/城市/运营商），查不到的段是 "0"，去重后拼成可读文本
function fmtRegion(region?: string[] | null) {
  if (!region?.length) return '—';
  const parts = region.map((part) => (part ?? '').trim()).filter((part) => part && part !== '0');
  return parts.length ? [...new Set(parts)].join(' ') : '—';
}

function fmtTime(value?: string) {
  if (!value) return '—';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('zh-CN');
}

function fmtMethod(method?: string) {
  const key = `pages.loginlog.methods.${method}`;
  return method && i18n.global.te(key) ? t(key) : method || '—';
}

function fmtDevice(row: any) {
  const name = row.DeviceName || row.DeviceId;
  if (!name) return '—';
  return row.OperatingSystem ? `${name} · ${row.OperatingSystem}` : name;
}

// 失败原因存的是服务端词条键，能查到本地化就用本地化文案，查不到原样展示
function fmtReason(row: any) {
  if (row.Success) return '—';
  const key = row.FailureKey;
  if (!key) return '—';
  const i18nKey = `pages.server.${key}`;
  return i18n.global.te(i18nKey) ? t(i18nKey) : key;
}

async function load() {
  loading.value = true;
  try {
    const params: Record<string, unknown> = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    };
    if (filters.keyword.trim()) params.keyword = filters.keyword.trim();
    if (filters.success !== '') params.success = filters.success === 'true';

    const res: any = await getLoginLogs(params);
    const d = res.data ?? {};
    rows.value = d.Items ?? [];
    pagination.total = d.Total ?? 0;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.loginlog.messages.loadFail'));
  } finally {
    loading.value = false;
  }
}

function search() {
  pagination.current = 1;
  load();
}

function reset() {
  filters.keyword = '';
  filters.success = '';
  pagination.current = 1;
  load();
}

function onPageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
  load();
}

onMounted(load);
</script>

<template>
  <div class="hutao-page">
    <t-card :title="$t('pages.loginlog.title')" :bordered="false" class="hutao-card">
      <div class="filter-bar">
        <t-input
          v-model="filters.keyword"
          :placeholder="$t('pages.loginlog.filters.keywordPlaceholder')"
          clearable
          style="width: 280px"
          @enter="search"
        />
        <t-select v-model="filters.success" :options="resultOptions" style="width: 140px" />
        <t-button theme="primary" :loading="loading" @click="search">{{ $t('pages.loginlog.filters.search') }}</t-button>
        <t-button variant="outline" @click="reset">{{ $t('pages.loginlog.filters.reset') }}</t-button>
        <span class="tip">{{ $t('pages.loginlog.tip') }}</span>
      </div>

      <t-table
        row-key="Id"
        :data="rows"
        :columns="columns"
        :loading="loading"
        :pagination="pagination"
        :empty="$t('pages.loginlog.empty')"
        size="small"
        @page-change="onPageChange"
      >
        <template #LoginAt="{ row }">{{ fmtTime(row.LoginAt) }}</template>
        <template #UserName="{ row }">{{ row.UserName || '—' }}</template>
        <template #Method="{ row }">{{ fmtMethod(row.Method) }}</template>
        <template #Success="{ row }">
          <t-tag v-if="row.Success" theme="success" variant="light">{{ $t('pages.loginlog.filters.success') }}</t-tag>
          <t-tag v-else theme="danger" variant="light">{{ $t('pages.loginlog.filters.failure') }}</t-tag>
        </template>
        <template #Ip="{ row }">{{ row.Ip || '—' }}</template>
        <template #IpRegion="{ row }">{{ fmtRegion(row.IpRegion) }}</template>
        <template #device="{ row }">{{ fmtDevice(row) }}</template>
        <template #FailureKey="{ row }">{{ fmtReason(row) }}</template>
      </t-table>
    </t-card>
  </div>
</template>

<style lang="less" scoped>
.filter-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  margin-bottom: 16px;
}
.tip {
  margin-left: auto;
  font-size: 12px;
  color: var(--td-text-color-placeholder);
}
</style>
