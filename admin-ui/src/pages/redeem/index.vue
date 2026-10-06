<script setup lang="ts">
import { DialogPlugin, MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, reactive, ref } from 'vue';

import {
  batchDeleteRedeemCodes,
  deleteRedeemCode,
  generateRedeemCodes,
  getRedeemList,
  updateRedeemCode,
  useRedeemCode,
} from '@/api/service';
import { t } from '@/locales';

const SERVICE_OPTIONS = computed(() => [
  { label: t('pages.redeem.service.gachalog'), value: 1 },
  { label: t('pages.redeem.service.cdn'), value: 2 },
]);

const gen = reactive({
  Count: 1,
  ServiceType: 1,
  Value: 30,
  Description: '',
  limits: [] as string[],
  ExpireTime: '',
  Times: 1,
});
const genLoading = ref(false);
const codes = ref<string[]>([]);

const timeLimited = computed(() => gen.limits.includes('time'));
const timesLimited = computed(() => gen.limits.includes('times'));
const codesText = computed(() => codes.value.join('\n'));

async function generate() {
  if (!gen.Count || gen.Count < 1) {
    MessagePlugin.warning(t('pages.redeem.messages.countRequired'));
    return;
  }
  if (timeLimited.value && !gen.ExpireTime) {
    MessagePlugin.warning(t('pages.redeem.messages.expireTimeRequired'));
    return;
  }
  const type = (timeLimited.value ? 1 : 0) | (timesLimited.value ? 2 : 0);
  const payload = {
    Count: gen.Count,
    Type: type,
    ServiceType: gen.ServiceType,
    Value: gen.Value,
    Description: gen.Description.trim(),
    ExpireTime: gen.ExpireTime ? new Date(gen.ExpireTime).toISOString() : new Date().toISOString(),
    Times: timesLimited.value ? gen.Times : 0,
  };
  genLoading.value = true;
  try {
    const res: any = await generateRedeemCodes(payload);
    codes.value = res.data?.Codes || [];
    MessagePlugin.success(t('pages.redeem.messages.generateSuccess', { n: codes.value.length }));
    pagination.current = 1;
    loadList();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.redeem.messages.generateFail'));
  } finally {
    genLoading.value = false;
  }
}

async function copyCodes() {
  try {
    await navigator.clipboard.writeText(codesText.value);
    MessagePlugin.success(t('pages.redeem.messages.copied'));
  } catch {
    MessagePlugin.warning(t('pages.redeem.messages.copyFail'));
  }
}

const use = reactive({ code: '', username: '' });
const useLoading = ref(false);
const useResult = ref('');

async function redeem() {
  if (!use.code.trim()) {
    MessagePlugin.warning(t('pages.redeem.messages.codeRequired'));
    return;
  }
  useLoading.value = true;
  useResult.value = '';
  try {
    const res: any = await useRedeemCode({ code: use.code.trim(), username: use.username.trim() || null });
    useResult.value = res.message || t('pages.redeem.messages.redeemSuccess');
    MessagePlugin.success(t('pages.redeem.messages.redeemSuccess'));
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.redeem.messages.redeemFail'));
  } finally {
    useLoading.value = false;
  }
}

const SERVICE_TEXT = computed<Record<number, string>>(() => ({
  1: t('pages.redeem.service.gachalogShort'),
  2: t('pages.redeem.service.cdnShort'),
}));
function typeText(type: number) {
  const parts: string[] = [];
  if (type & 1) parts.push(t('pages.redeem.limit.time'));
  if (type & 2) parts.push(t('pages.redeem.limit.times'));
  return parts.length ? parts.join(' + ') : t('pages.redeem.limit.none');
}
function fmtTime(value?: string) {
  if (!value) return '—';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('zh-CN');
}

const listLoading = ref(false);
const keyword = ref('');
const rows = ref<any[]>([]);
const pagination = reactive({ current: 1, pageSize: 10, total: 0 });

const listColumns = computed(() => [
  { colKey: 'row-select', type: 'multiple', width: 50 },
  { colKey: 'Code', title: t('pages.redeem.list.colCode'), width: 200 },
  { colKey: 'ServiceType', title: t('pages.redeem.list.colService'), width: 90 },
  { colKey: 'Value', title: t('pages.redeem.list.colValue'), width: 80 },
  { colKey: 'Type', title: t('pages.redeem.list.colType'), width: 120 },
  { colKey: 'UsedCount', title: t('pages.redeem.list.colUsed'), width: 70 },
  { colKey: 'Creator', title: t('pages.redeem.list.colCreator'), ellipsis: true, minWidth: 120 },
  { colKey: 'CreateTime', title: t('pages.redeem.list.colCreateTime'), width: 180 },
  { colKey: 'op', title: t('pages.redeem.list.colOp'), width: 130, fixed: 'right' },
]);

const selectedIds = ref<number[]>([]);

async function loadList() {
  listLoading.value = true;
  try {
    const res: any = await getRedeemList(pagination.current, pagination.pageSize, keyword.value.trim());
    const d = res.data ?? {};
    rows.value = d.Items ?? [];
    pagination.total = d.Total ?? 0;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.redeem.messages.listFail'));
  } finally {
    listLoading.value = false;
  }
}

function onSearch() {
  pagination.current = 1;
  loadList();
}

function onPageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
  loadList();
}

onMounted(loadList);

const editDialog = reactive({
  visible: false,
  submitting: false,
  Id: 0,
  Code: '',
  Type: 0,
  ServiceType: 1,
  Value: 30,
  Description: '',
  ExpireTime: '',
  TimesAllowed: 1,
  limits: [] as string[],
});

const editTimeLimited = computed(() => editDialog.limits.includes('time'));
const editTimesLimited = computed(() => editDialog.limits.includes('times'));

function openEdit(row: any) {
  editDialog.Id = row.Id;
  editDialog.Code = row.Code;
  editDialog.Type = row.Type;
  editDialog.ServiceType = row.ServiceType;
  editDialog.Value = row.Value;
  editDialog.Description = row.Description ?? '';
  editDialog.ExpireTime = row.ExpireTime ? String(row.ExpireTime) : '';
  editDialog.TimesAllowed = row.TimesAllowed ?? 0;
  const limits: string[] = [];
  if (row.Type & 1) limits.push('time');
  if (row.Type & 2) limits.push('times');
  editDialog.limits = limits;
  editDialog.visible = true;
}

async function submitEdit() {
  if (editTimeLimited.value && !editDialog.ExpireTime) {
    MessagePlugin.warning(t('pages.redeem.messages.expireTimeRequired'));
    return;
  }
  editDialog.submitting = true;
  try {
    const type = (editTimeLimited.value ? 1 : 0) | (editTimesLimited.value ? 2 : 0);
    const payload = {
      Id: editDialog.Id,
      Type: type,
      ServiceType: editDialog.ServiceType,
      Value: editDialog.Value,
      Description: editDialog.Description.trim(),
      ExpireTime: editDialog.ExpireTime ? new Date(editDialog.ExpireTime).toISOString() : new Date().toISOString(),
      TimesAllowed: editTimesLimited.value ? editDialog.TimesAllowed : 0,
    };
    const res: any = await updateRedeemCode(payload);
    MessagePlugin.success(res.message || t('pages.redeem.messages.editSuccess'));
    editDialog.visible = false;
    await loadList();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.redeem.messages.editFail'));
  } finally {
    editDialog.submitting = false;
  }
}

function removeCode(row: any) {
  const dialog = DialogPlugin.confirm({
    header: t('pages.redeem.dialog.removeHeader'),
    body: t('pages.redeem.dialog.removeConfirmBody', { code: row.Code }),
    theme: 'warning',
    confirmBtn: t('pages.redeem.actions.delete'),
    onConfirm: async () => {
      dialog.hide();
      try {
        const res: any = await deleteRedeemCode(row.Id);
        MessagePlugin.success(res.message || t('pages.redeem.messages.deleteSuccess'));
        await loadList();
      } catch (err) {
        MessagePlugin.error((err as Error).message || t('pages.redeem.messages.deleteFail'));
      }
    },
  });
}

function batchDelete() {
  if (!selectedIds.value.length) {
    MessagePlugin.warning(t('pages.redeem.messages.selectFirst'));
    return;
  }
  const dialog = DialogPlugin.confirm({
    header: t('pages.redeem.dialog.batchRemoveHeader'),
    body: t('pages.redeem.dialog.batchRemoveConfirmBody', { n: selectedIds.value.length }),
    theme: 'warning',
    confirmBtn: t('pages.redeem.actions.delete'),
    onConfirm: async () => {
      dialog.hide();
      try {
        const res: any = await batchDeleteRedeemCodes([...selectedIds.value]);
        MessagePlugin.success(res.message || t('pages.redeem.messages.deleteSuccess'));
        selectedIds.value = [];
        await loadList();
      } catch (err) {
        MessagePlugin.error((err as Error).message || t('pages.redeem.messages.deleteFail'));
      }
    },
  });
}

function csvCell(v: any) {
  const s = String(v ?? '');
  return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}

async function exportCsv() {
  try {
    const res: any = await getRedeemList(1, 10000, keyword.value.trim());
    const items = res.data?.Items ?? [];
    if (!items.length) {
      MessagePlugin.warning(t('pages.redeem.messages.noExportData'));
      return;
    }
    const header = ['Code', 'Service', 'ValueDays', 'Type', 'UsedCount', 'Creator', 'CreateTime', 'ExpireTime', 'TimesAllowed', 'Description'];
    const lines = [header.join(',')];
    for (const it of items) {
      lines.push([
        it.Code,
        SERVICE_TEXT.value[it.ServiceType] ?? it.ServiceType,
        it.Value,
        typeText(it.Type),
        it.UsedCount,
        it.Creator,
        fmtTime(it.CreateTime),
        fmtTime(it.ExpireTime),
        it.TimesAllowed,
        it.Description ?? '',
      ].map(csvCell).join(','));
    }
    const blob = new Blob([`﻿${lines.join('\n')}`], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `redeem-codes-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.redeem.messages.exportFail'));
  }
}
</script>

<template>
  <div class="hutao-page">
    <t-row :gutter="16">
      <t-col :span="8">
        <t-card :title="$t('pages.redeem.gen.title')" class="hutao-card" :bordered="false">
          <p class="tip">{{ $t('pages.redeem.gen.tipPre') }}<code>POST /Service/Redeem/Generate</code>{{ $t('pages.redeem.gen.tipSuf') }}</p>
          <t-form label-align="top">
            <t-row :gutter="16">
              <t-col :span="4">
                <t-form-item :label="$t('pages.redeem.gen.serviceType')">
                  <t-select v-model="gen.ServiceType" :options="SERVICE_OPTIONS" style="width: 100%" />
                </t-form-item>
              </t-col>
              <t-col :span="4">
                <t-form-item :label="$t('pages.redeem.gen.count')">
                  <t-input-number v-model="gen.Count" :min="1" :max="1000" style="width: 100%" />
                </t-form-item>
              </t-col>
              <t-col :span="4">
                <t-form-item :label="$t('pages.redeem.gen.valueDays')">
                  <t-input-number v-model="gen.Value" :min="1" :max="3650" style="width: 100%" />
                </t-form-item>
              </t-col>
              <t-col :span="4">
                <t-form-item :label="$t('pages.redeem.gen.limitMode')">
                  <t-checkbox-group v-model="gen.limits">
                    <t-checkbox value="time">{{ $t('pages.redeem.limit.time') }}</t-checkbox>
                    <t-checkbox value="times">{{ $t('pages.redeem.limit.times') }}</t-checkbox>
                  </t-checkbox-group>
                </t-form-item>
              </t-col>
              <t-col v-if="timeLimited" :span="4">
                <t-form-item :label="$t('pages.redeem.gen.expireTime')">
                  <t-date-picker
                    v-model="gen.ExpireTime"
                    enable-time-picker
                    allow-input
                    clearable
                    format="YYYY-MM-DD HH:mm:ss"
                    style="width: 100%"
                  />
                </t-form-item>
              </t-col>
              <t-col v-if="timesLimited" :span="4">
                <t-form-item :label="$t('pages.redeem.gen.times')">
                  <t-input-number v-model="gen.Times" :min="1" :max="100000" style="width: 100%" />
                </t-form-item>
              </t-col>
              <t-col :span="12">
                <t-form-item :label="$t('pages.redeem.gen.description')">
                  <t-input v-model="gen.Description" :placeholder="$t('pages.redeem.gen.descriptionPlaceholder')" clearable />
                </t-form-item>
              </t-col>
              <t-col :span="12">
                <t-form-item>
                  <t-button theme="primary" :loading="genLoading" @click="generate">{{ $t('pages.redeem.gen.submit') }}</t-button>
                </t-form-item>
              </t-col>
            </t-row>
          </t-form>
          <div v-if="codes.length" class="result">
            <t-space style="margin-bottom: 8px">
              <t-tag theme="primary" variant="light">{{ $t('pages.redeem.gen.totalCount', { n: codes.length }) }}</t-tag>
              <t-button size="small" variant="outline" @click="copyCodes">{{ $t('pages.redeem.gen.copyAll') }}</t-button>
            </t-space>
            <t-textarea :value="codesText" readonly :autosize="{ minRows: 3, maxRows: 12 }" />
          </div>
        </t-card>
      </t-col>
      <t-col :span="4">
        <t-card :title="$t('pages.redeem.use.title')" :bordered="false" class="hutao-card">
          <p class="tip">{{ $t('pages.redeem.use.tipPre') }}<code>POST /Redeem/Use</code>{{ $t('pages.redeem.use.tipSuf') }}</p>
          <t-form label-align="top">
            <t-form-item :label="$t('pages.redeem.use.code')">
              <t-input v-model="use.code" :placeholder="$t('pages.redeem.use.codePlaceholder')" clearable />
            </t-form-item>
            <t-form-item :label="$t('pages.redeem.use.username')">
              <t-input v-model="use.username" :placeholder="$t('pages.redeem.use.usernamePlaceholder')" clearable />
            </t-form-item>
            <t-form-item>
              <t-button theme="primary" :loading="useLoading" @click="redeem">{{ $t('pages.redeem.use.submit') }}</t-button>
            </t-form-item>
          </t-form>
          <t-alert v-if="useResult" class="result" theme="success" :message="useResult" />
        </t-card>
      </t-col>
    </t-row>

    <t-card :title="$t('pages.redeem.list.title')" :bordered="false" class="hutao-card">
      <t-space class="search-bar" break-line>
        <t-input v-model="keyword" :placeholder="$t('pages.redeem.list.searchPlaceholder')" clearable style="width: 300px" @enter="onSearch" @clear="onSearch" />
        <t-button theme="primary" :loading="listLoading" @click="onSearch">{{ $t('pages.redeem.list.search') }}</t-button>
        <t-button theme="danger" variant="outline" :disabled="!selectedIds.length" @click="batchDelete">
          {{ $t('pages.redeem.list.batchDelete', { n: selectedIds.length }) }}
        </t-button>
        <t-button variant="outline" @click="exportCsv">{{ $t('pages.redeem.list.exportCsv') }}</t-button>
      </t-space>
      <t-table
        row-key="Id"
        :data="rows"
        :columns="listColumns"
        :loading="listLoading"
        :pagination="pagination"
        :selected-row-keys="selectedIds"
        size="small"
        @select-change="(value: number[]) => (selectedIds = value)"
        @page-change="onPageChange"
      >
        <template #ServiceType="{ row }">{{ SERVICE_TEXT[row.ServiceType] ?? row.ServiceType }}</template>
        <template #Type="{ row }">{{ typeText(row.Type) }}</template>
        <template #CreateTime="{ row }">{{ fmtTime(row.CreateTime) }}</template>
        <template #op="{ row }">
          <t-space size="small">
            <t-link theme="primary" hover="color" @click="openEdit(row)">{{ $t('pages.redeem.actions.edit') }}</t-link>
            <t-link theme="danger" hover="color" @click="removeCode(row)">{{ $t('pages.redeem.actions.delete') }}</t-link>
          </t-space>
        </template>
      </t-table>
    </t-card>

    <t-dialog
      v-model:visible="editDialog.visible"
      :header="$t('pages.redeem.edit.title')"
      width="720px"
      :confirm-btn="{ content: $t('pages.redeem.actions.save'), loading: editDialog.submitting }"
      @confirm="submitEdit"
    >
      <t-form label-align="top">
        <t-row :gutter="16">
          <t-col :span="6">
            <t-form-item :label="$t('pages.redeem.edit.code')">
              <t-input :value="editDialog.Code" readonly />
            </t-form-item>
          </t-col>
          <t-col :span="6">
            <t-form-item :label="$t('pages.redeem.gen.serviceType')">
              <t-select v-model="editDialog.ServiceType" :options="SERVICE_OPTIONS" />
            </t-form-item>
          </t-col>
          <t-col :span="6">
            <t-form-item :label="$t('pages.redeem.gen.valueDays')">
              <t-input-number v-model="editDialog.Value" :min="1" :max="3650" style="width: 100%" />
            </t-form-item>
          </t-col>
          <t-col :span="6">
            <t-form-item :label="$t('pages.redeem.gen.limitMode')">
              <t-checkbox-group v-model="editDialog.limits">
                  <t-checkbox value="time">{{ $t('pages.redeem.limit.time') }}</t-checkbox>
                  <t-checkbox value="times">{{ $t('pages.redeem.limit.times') }}</t-checkbox>
              </t-checkbox-group>
            </t-form-item>
          </t-col>
          <t-col v-if="editTimeLimited" :span="6">
            <t-form-item :label="$t('pages.redeem.gen.expireTime')">
              <t-date-picker
                v-model="editDialog.ExpireTime"
                enable-time-picker
                allow-input
                clearable
                format="YYYY-MM-DD HH:mm:ss"
                style="width: 100%"
              />
            </t-form-item>
          </t-col>
          <t-col v-if="editTimesLimited" :span="6">
            <t-form-item :label="$t('pages.redeem.gen.times')">
              <t-input-number v-model="editDialog.TimesAllowed" :min="1" :max="100000" style="width: 100%" />
            </t-form-item>
          </t-col>
          <t-col :span="12">
            <t-form-item :label="$t('pages.redeem.gen.description')">
              <t-input v-model="editDialog.Description" :placeholder="$t('pages.redeem.edit.descriptionPlaceholder')" clearable />
            </t-form-item>
          </t-col>
        </t-row>
      </t-form>
    </t-dialog>
  </div>
</template>

<style lang="less" scoped>
.tip {
  margin-bottom: 16px;
  color: var(--td-text-color-secondary);
  font-size: 13px;
}
.hutao-card {
  margin-bottom: 16px;
}
.result {
  margin-top: 12px;
}
.search-bar {
  margin-bottom: 16px;
}
</style>
