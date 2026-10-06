<script setup lang="ts">
import { DialogPlugin, MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, reactive, ref } from 'vue';

import {
  deleteAnnouncement,
  getAnnouncementList,
  setAnnouncementEnabled,
  updateAnnouncement,
  uploadAnnouncement,
} from '@/api/service';
import { t } from '@/locales';

const form = reactive({
  Title: '',
  Content: '',
  Severity: 0,
  Link: '',
  MaxPresentVersion: '',
});
const loading = ref(false);

const severityOptions = computed(() => [
  { label: t('pages.announcement.severity.informational'), value: 0 },
  { label: t('pages.announcement.severity.success'), value: 1 },
  { label: t('pages.announcement.severity.warning'), value: 2 },
  { label: t('pages.announcement.severity.error'), value: 3 },
]);

async function submit() {
  if (!form.Title.trim() || !form.Content.trim()) {
    MessagePlugin.warning(t('pages.announcement.messages.titleContentRequired'));
    return;
  }
  loading.value = true;
  try {
    const payload = {
      Title: form.Title.trim(),
      Content: form.Content,
      Severity: form.Severity,
      Link: form.Link.trim(),
      MaxPresentVersion: form.MaxPresentVersion.trim() || null,
    };
    const res: any = await uploadAnnouncement(payload);
    MessagePlugin.success(res.message || t('pages.announcement.messages.uploadSuccess'));
    pagination.current = 1;
    await loadList();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.announcement.messages.uploadFail'));
  } finally {
    loading.value = false;
  }
}

const severityTheme = ['default', 'success', 'warning', 'danger'];
const severityText = computed(() => [
  t('pages.announcement.severity.informationalShort'),
  t('pages.announcement.severity.successShort'),
  t('pages.announcement.severity.warningShort'),
  t('pages.announcement.severity.errorShort'),
]);

const listLoading = ref(false);
const rows = ref<any[]>([]);
const pagination = reactive({ current: 1, pageSize: 10, total: 0 });

const columns = computed(() => [
  { colKey: 'Id', title: 'ID', width: 80 },
  { colKey: 'Title', title: t('pages.announcement.list.colTitle'), ellipsis: true, minWidth: 180 },
  { colKey: 'Severity', title: t('pages.announcement.list.colSeverity'), width: 90 },
  { colKey: 'MaxPresentVersion', title: t('pages.announcement.list.colMaxVersion'), width: 140 },
  { colKey: 'Enabled', title: t('pages.announcement.list.colEnabled'), width: 90 },
  { colKey: 'LastUpdateTime', title: t('pages.announcement.list.colUpdateTime'), width: 190 },
  { colKey: 'op', title: t('pages.announcement.list.colOp'), width: 120, fixed: 'right' },
]);

function fmt(sec?: number) {
  if (!sec || sec <= 0) return '—';
  const d = new Date(sec * 1000);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('zh-CN');
}

async function loadList() {
  listLoading.value = true;
  try {
    const res: any = await getAnnouncementList(pagination.current, pagination.pageSize);
    const d = res.data ?? {};
    rows.value = d.Items ?? [];
    pagination.total = d.Total ?? 0;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.announcement.messages.listFail'));
  } finally {
    listLoading.value = false;
  }
}

function onPageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
  loadList();
}

function removeItem(row: any) {
  const dialog = DialogPlugin.confirm({
    header: t('pages.announcement.dialog.removeHeader'),
    body: t('pages.announcement.dialog.removeConfirmBody', { title: row.Title, id: row.Id }),
    theme: 'warning',
    confirmBtn: t('pages.announcement.actions.delete'),
    onConfirm: async () => {
      dialog.hide();
      try {
        const res: any = await deleteAnnouncement(row.Id, row.Locale ?? 'ALL');
        MessagePlugin.success(res.message || t('pages.announcement.messages.deleteSuccess'));
        await loadList();
      } catch (err) {
        MessagePlugin.error((err as Error).message || t('pages.announcement.messages.deleteFail'));
      }
    },
  });
}

async function toggleEnabled(row: any, value: boolean) {
  try {
    const res: any = await setAnnouncementEnabled(row.Id, value, row.Locale ?? 'ALL');
    MessagePlugin.success(res.message || t('pages.announcement.messages.opSuccess'));
    row.Enabled = value;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.announcement.messages.opFail'));
  }
}

onMounted(loadList);

const editDialog = reactive({
  visible: false,
  submitting: false,
  Id: 0,
  Locale: 'ALL',
  Title: '',
  Content: '',
  Severity: 0,
  Link: '',
  MaxPresentVersion: '',
});

function openEdit(row: any) {
  editDialog.Id = row.Id;
  editDialog.Locale = row.Locale ?? 'ALL';
  editDialog.Title = row.Title ?? '';
  editDialog.Content = row.Content ?? '';
  editDialog.Severity = row.Severity ?? 0;
  editDialog.Link = row.Link ?? '';
  editDialog.MaxPresentVersion = row.MaxPresentVersion ?? '';
  editDialog.visible = true;
}

async function submitEdit() {
  if (!editDialog.Title.trim() || !editDialog.Content.trim()) {
    MessagePlugin.warning(t('pages.announcement.messages.titleContentRequired'));
    return;
  }
  editDialog.submitting = true;
  try {
    const payload = {
      Id: editDialog.Id,
      Locale: editDialog.Locale,
      Title: editDialog.Title.trim(),
      Content: editDialog.Content,
      Severity: editDialog.Severity,
      Link: editDialog.Link.trim(),
      MaxPresentVersion: editDialog.MaxPresentVersion.trim() || null,
    };
    const res: any = await updateAnnouncement(payload);
    MessagePlugin.success(res.message || t('pages.announcement.messages.editSuccess'));
    editDialog.visible = false;
    await loadList();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.announcement.messages.editFail'));
  } finally {
    editDialog.submitting = false;
  }
}
</script>

<template>
  <div class="hutao-page">
    <t-card :title="$t('pages.announcement.form.title')" :bordered="false" class="hutao-card">
      <p class="tip">{{ $t('pages.announcement.form.tipPre') }}<code>POST /Service/Announcement/Upload</code>{{ $t('pages.announcement.form.tipSuf') }}</p>
      <t-form label-align="top">
        <t-row :gutter="16">
          <t-col :span="6">
            <t-form-item :label="$t('pages.announcement.form.titleLabel')">
              <t-input v-model="form.Title" :placeholder="$t('pages.announcement.form.titlePlaceholder')" clearable />
            </t-form-item>
          </t-col>
          <t-col :span="6">
            <t-form-item :label="$t('pages.announcement.form.severity')">
              <t-select v-model="form.Severity" :options="severityOptions" style="width: 100%" />
            </t-form-item>
          </t-col>
          <t-col :span="6">
            <t-form-item :label="$t('pages.announcement.form.link')">
              <t-input v-model="form.Link" :placeholder="$t('pages.announcement.form.linkPlaceholder')" clearable />
            </t-form-item>
          </t-col>
          <t-col :span="6">
            <t-form-item :label="$t('pages.announcement.form.maxVersion')">
              <t-input v-model="form.MaxPresentVersion" :placeholder="$t('pages.announcement.form.maxVersionPlaceholder')" clearable />
            </t-form-item>
          </t-col>
          <t-col :span="12">
            <t-form-item :label="$t('pages.announcement.form.content')">
              <t-textarea v-model="form.Content" :placeholder="$t('pages.announcement.form.contentPlaceholder')" :autosize="{ minRows: 4, maxRows: 12 }" />
            </t-form-item>
          </t-col>
          <t-col :span="12">
            <t-form-item>
              <t-button theme="primary" :loading="loading" @click="submit">{{ $t('pages.announcement.form.submit') }}</t-button>
            </t-form-item>
          </t-col>
        </t-row>
      </t-form>
    </t-card>

    <t-card :title="$t('pages.announcement.list.title')" :bordered="false" class="hutao-card">
      <template #actions>
        <t-button variant="outline" size="small" :loading="listLoading" @click="loadList">{{ $t('pages.announcement.list.refresh') }}</t-button>
      </template>
      <t-table
        row-key="Id"
        :data="rows"
        :columns="columns"
        :loading="listLoading"
        :pagination="pagination"
        size="small"
        @page-change="onPageChange"
      >
        <template #Severity="{ row }">
          <t-tag :theme="severityTheme[row.Severity] || 'default'" variant="light">
            {{ severityText[row.Severity] ?? row.Severity }}
          </t-tag>
        </template>
        <template #MaxPresentVersion="{ row }">{{ row.MaxPresentVersion || '—' }}</template>
        <template #Enabled="{ row }">
          <t-switch :value="row.Enabled" @change="(v: boolean) => toggleEnabled(row, v)" />
        </template>
        <template #LastUpdateTime="{ row }">{{ fmt(row.LastUpdateTime) }}</template>
        <template #op="{ row }">
          <t-space size="small">
            <t-link theme="primary" hover="color" @click="openEdit(row)">{{ $t('pages.announcement.actions.edit') }}</t-link>
            <t-link theme="danger" hover="color" @click="removeItem(row)">{{ $t('pages.announcement.actions.delete') }}</t-link>
          </t-space>
        </template>
      </t-table>
    </t-card>

    <t-dialog
      v-model:visible="editDialog.visible"
      :header="$t('pages.announcement.edit.title')"
      width="720px"
      :confirm-btn="{ content: $t('pages.announcement.actions.save'), loading: editDialog.submitting }"
      @confirm="submitEdit"
    >
      <t-form label-align="top">
        <t-row :gutter="16">
          <t-col :span="6">
            <t-form-item :label="$t('pages.announcement.form.titleLabel')">
              <t-input v-model="editDialog.Title" :placeholder="$t('pages.announcement.form.titlePlaceholder')" clearable />
            </t-form-item>
          </t-col>
          <t-col :span="6">
            <t-form-item :label="$t('pages.announcement.form.severity')">
              <t-select v-model="editDialog.Severity" :options="severityOptions" style="width: 100%" />
            </t-form-item>
          </t-col>
          <t-col :span="6">
            <t-form-item :label="$t('pages.announcement.form.link')">
              <t-input v-model="editDialog.Link" :placeholder="$t('pages.announcement.edit.linkPlaceholder')" clearable />
            </t-form-item>
          </t-col>
          <t-col :span="6">
            <t-form-item :label="$t('pages.announcement.form.maxVersion')">
              <t-input v-model="editDialog.MaxPresentVersion" :placeholder="$t('pages.announcement.edit.maxVersionPlaceholder')" clearable />
            </t-form-item>
          </t-col>
          <t-col :span="12">
            <t-form-item :label="$t('pages.announcement.form.content')">
              <t-textarea v-model="editDialog.Content" :placeholder="$t('pages.announcement.form.contentPlaceholder')" :autosize="{ minRows: 4, maxRows: 12 }" />
            </t-form-item>
          </t-col>
        </t-row>
      </t-form>
    </t-dialog>
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
</style>
