<script setup lang="ts">
import { MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, reactive, ref } from 'vue';

import { getAuditLogs } from '@/api/service';
import { t } from '@/locales';

const loading = ref(false);
const rows = ref<any[]>([]);
const pagination = reactive({ current: 1, pageSize: 20, total: 0 });

const columns = computed(() => [
  { colKey: 'Timestamp', title: t('pages.audit.list.colTime'), width: 180 },
  { colKey: 'admin', title: t('pages.audit.list.colOperator'), width: 200, ellipsis: true },
  { colKey: 'Action', title: t('pages.audit.list.colAction'), width: 260, ellipsis: true },
  { colKey: 'Success', title: t('pages.audit.list.colResult'), width: 80 },
  { colKey: 'Detail', title: t('pages.audit.list.colDetail'), ellipsis: true, minWidth: 240 },
]);

function fmt(sec?: number) {
  if (!sec || sec <= 0) return '—';
  const d = new Date(sec * 1000);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('zh-CN');
}

async function load() {
  loading.value = true;
  try {
    const res: any = await getAuditLogs(pagination.current, pagination.pageSize);
    const d = res.data ?? {};
    rows.value = d.Items ?? [];
    pagination.total = d.Total ?? 0;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.audit.messages.loadFail'));
  } finally {
    loading.value = false;
  }
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
    <t-card :title="$t('pages.audit.list.title')" :bordered="false" class="hutao-card">
      <template #actions>
        <t-button variant="outline" size="small" :loading="loading" @click="load">{{ $t('pages.audit.list.refresh') }}</t-button>
      </template>
      <p class="tip">{{ $t('pages.audit.list.tip') }}</p>
      <t-table
        row-key="Id"
        :data="rows"
        :columns="columns"
        :loading="loading"
        :pagination="pagination"
        size="small"
        @page-change="onPageChange"
      >
        <template #Timestamp="{ row }">{{ fmt(row.Timestamp) }}</template>
        <template #admin="{ row }">{{ row.AdminUserName || '—' }} (#{{ row.AdminUserId }})</template>
        <template #Success="{ row }">
          <t-tag v-if="row.Success" theme="success" variant="light">{{ $t('pages.audit.list.success') }}</t-tag>
          <t-tag v-else theme="danger" variant="light">{{ $t('pages.audit.list.failure') }}</t-tag>
        </template>
        <template #Detail="{ row }">
          <t-tooltip v-if="row.Detail" :content="row.Detail" placement="top">
            <span class="detail-cell">{{ row.Detail }}</span>
          </t-tooltip>
          <span v-else>—</span>
        </template>
      </t-table>
    </t-card>
  </div>
</template>

<style lang="less" scoped>
.hutao-card {
  margin-bottom: 16px;
}
.tip {
  margin-bottom: 16px;
  color: var(--td-text-color-secondary);
  font-size: 13px;
}
.detail-cell {
  font-family: var(--td-font-family-medium, monospace);
  font-size: 12px;
}
</style>
