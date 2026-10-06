<script setup lang="ts">
import { MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, reactive, ref } from 'vue';

import { approveLicenseByService, getLicenseApplications } from '@/api/service';
import { t } from '@/locales';

const form = reactive({ userName: '', code: '' });
const loading = ref(false);
const result = ref('');

async function submit() {
  if (!form.userName.trim() || !form.code.trim()) {
    MessagePlugin.warning(t('pages.license.messages.fillRequired'));
    return;
  }
  loading.value = true;
  result.value = '';
  try {
    const res: any = await approveLicenseByService(form.userName.trim(), form.code.trim());
    result.value = res.message || t('pages.license.messages.approveSuccess');
    MessagePlugin.success(t('pages.license.messages.approveSuccess'));
    await loadList();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.license.messages.approveFail'));
  } finally {
    loading.value = false;
  }
}

const listLoading = ref(false);
const onlyPending = ref(true);
const rows = ref<any[]>([]);
const pagination = reactive({ current: 1, pageSize: 10, total: 0 });
const approvingId = ref<number | null>(null);

const columns = computed(() => [
  { colKey: 'UserName', title: t('pages.license.columns.applicant'), ellipsis: true, minWidth: 180 },
  { colKey: 'ProjectUrl', title: t('pages.license.columns.projectUrl'), ellipsis: true, minWidth: 220 },
  { colKey: 'IsApproved', title: t('pages.license.columns.status'), width: 90 },
  { colKey: 'op', title: t('pages.license.columns.op'), width: 90, fixed: 'right' },
]);

async function loadList() {
  listLoading.value = true;
  try {
    const res: any = await getLicenseApplications(pagination.current, pagination.pageSize, onlyPending.value);
    const d = res.data ?? {};
    rows.value = d.Items ?? [];
    pagination.total = d.Total ?? 0;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.license.messages.loadListFail'));
  } finally {
    listLoading.value = false;
  }
}

function onFilterChange() {
  pagination.current = 1;
  loadList();
}

function onPageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
  loadList();
}

async function approveRow(row: any) {
  approvingId.value = row.PrimaryId;
  try {
    const res: any = await approveLicenseByService(row.UserName, row.ApprovalCode);
    MessagePlugin.success(res.message || t('pages.license.messages.approveSuccess'));
    await loadList();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.license.messages.approveFail'));
  } finally {
    approvingId.value = null;
  }
}

onMounted(loadList);
</script>

<template>
  <div class="hutao-page">
    <t-row :gutter="16">
      <t-col :span="4">
        <t-card :title="$t('pages.license.approve.title')" :bordered="false" class="hutao-card">
          <p class="tip">{{ $t('pages.license.approve.tipPre') }}<code>POST /Service/License/Approve</code>{{ $t('pages.license.approve.tipSuf') }}</p>
          <t-form label-align="top">
            <t-form-item :label="$t('pages.license.approve.emailLabel')">
              <t-input v-model="form.userName" :placeholder="$t('pages.license.approve.emailPlaceholder')" clearable />
            </t-form-item>
            <t-form-item :label="$t('pages.license.approve.codeLabel')">
              <t-input v-model="form.code" placeholder="approve code" clearable />
            </t-form-item>
            <t-form-item>
              <t-button theme="primary" :loading="loading" @click="submit">{{ $t('pages.license.approve.submit') }}</t-button>
            </t-form-item>
          </t-form>
          <t-alert v-if="result" class="result" theme="success" :message="result" />
        </t-card>
      </t-col>
      <t-col :span="8">
        <t-card :title="$t('pages.license.list.title')" :bordered="false" class="hutao-card">
          <template #actions>
            <t-space size="small">
              <t-checkbox v-model="onlyPending" @change="onFilterChange">{{ $t('pages.license.list.onlyPending') }}</t-checkbox>
              <t-button variant="outline" size="small" :loading="listLoading" @click="loadList">{{ $t('pages.license.list.refresh') }}</t-button>
            </t-space>
          </template>
          <t-table
            row-key="PrimaryId"
            :data="rows"
            :columns="columns"
            :loading="listLoading"
            :pagination="pagination"
            size="small"
            @page-change="onPageChange"
          >
            <template #IsApproved="{ row }">
              <t-tag v-if="row.IsApproved" theme="success" variant="light">{{ $t('pages.license.list.approved') }}</t-tag>
              <t-tag v-else theme="warning" variant="light">{{ $t('pages.license.list.pending') }}</t-tag>
            </template>
            <template #op="{ row }">
              <t-link
                v-if="!row.IsApproved"
                theme="primary"
                hover="color"
                :disabled="approvingId === row.PrimaryId"
                @click="approveRow(row)"
              >
                {{ $t('pages.license.list.approve') }}
              </t-link>
              <span v-else>—</span>
            </template>
          </t-table>
        </t-card>
      </t-col>
    </t-row>
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
.result {
  margin-top: 12px;
}
</style>
