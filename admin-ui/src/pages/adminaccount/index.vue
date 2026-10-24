<template>
  <div class="hutao-page">
    <t-card :title="$t('pages.adminaccount.title')" :bordered="false" class="hutao-card">
      <p class="tip">{{ $t('pages.adminaccount.tip') }}</p>
      <t-alert v-if="!canWrite" theme="warning" :message="$t('pages.adminaccount.readonly')" class="readonly-alert" />

      <t-space break-line size="8px" class="toolbar">
        <t-button theme="primary" :disabled="!canWrite" @click="openAdd">{{ $t('pages.adminaccount.add.btn') }}</t-button>
        <t-button variant="outline" :loading="loading" @click="load">{{ $t('pages.adminaccount.refresh') }}</t-button>
      </t-space>

      <t-table
        row-key="Id"
        :data="accounts"
        :columns="columns"
        :loading="loading"
        :pagination="false"
        size="small"
        max-height="520"
      >
        <template #state="{ row }">
          <t-space size="4px">
            <t-tag :theme="row.IsEnabled ? 'success' : 'danger'" variant="light">
              {{ row.IsEnabled ? $t('pages.adminaccount.enabled') : $t('pages.adminaccount.disabled') }}
            </t-tag>
            <t-tag v-if="row.IsCurrent" theme="primary" variant="light">{{ $t('pages.adminaccount.current') }}</t-tag>
            <t-tag v-if="row.IsBootstrap" theme="warning" variant="light">{{ $t('pages.adminaccount.bootstrap') }}</t-tag>
          </t-space>
        </template>

        <template #lastLogin="{ row }">
          {{ row.LastLoginAt ? formatTime(row.LastLoginAt) : $t('pages.adminaccount.never') }}
        </template>

        <template #bound="{ row }">
          <span v-if="row.BoundUserName">{{ row.BoundUserName }}</span>
          <span v-else class="unbound">{{ $t('pages.adminaccount.unbound') }}</span>
        </template>

        <template #op="{ row }">
          <t-link
            theme="primary"
            hover="color"
            :disabled="!canWrite || row.IsCurrent"
            @click="toggleEnabled(row)"
          >
            {{ row.IsEnabled ? $t('pages.adminaccount.disable') : $t('pages.adminaccount.enable') }}
          </t-link>
          <t-link
            theme="primary"
            hover="color"
            :disabled="!canWrite"
            class="op-link"
            @click="openBind(row)"
          >
            {{ row.BoundUserId ? $t('pages.adminaccount.bind.rebind') : $t('pages.adminaccount.bind.btn') }}
          </t-link>
          <t-link
            v-if="row.BoundUserId"
            theme="warning"
            hover="color"
            :disabled="!canWrite"
            class="op-link"
            @click="confirmUnbind(row)"
          >
            {{ $t('pages.adminaccount.unbind.btn') }}
          </t-link>
          <t-link
            theme="danger"
            hover="color"
            :disabled="!canWrite || row.IsCurrent"
            class="op-link"
            @click="confirmRemove(row)"
          >
            {{ $t('pages.adminaccount.remove.btn') }}
          </t-link>
        </template>
      </t-table>
    </t-card>

    <t-dialog
      v-model:visible="addDialog.visible"
      :header="$t('pages.adminaccount.add.header')"
      :confirm-btn="{ content: $t('pages.adminaccount.add.confirm'), loading: addDialog.submitting }"
      @confirm="submitAdd"
    >
      <t-form label-align="left" :label-width="110">
        <t-form-item :label="$t('pages.adminaccount.add.subject')" name="subject">
          <t-input v-model="addDialog.subject" :placeholder="$t('pages.adminaccount.add.subjectPlaceholder')" />
        </t-form-item>
        <t-form-item :label="$t('pages.adminaccount.add.userName')" name="userName">
          <t-input v-model="addDialog.userName" :placeholder="$t('pages.adminaccount.add.userNamePlaceholder')" />
        </t-form-item>
        <t-form-item :label="$t('pages.adminaccount.add.email')" name="email">
          <t-input v-model="addDialog.email" :placeholder="$t('pages.adminaccount.add.emailPlaceholder')" />
        </t-form-item>
      </t-form>
    </t-dialog>

    <t-dialog
      v-model:visible="bindDialog.visible"
      :header="$t('pages.adminaccount.bind.header')"
      :confirm-btn="{ content: $t('pages.adminaccount.bind.confirm'), loading: bindDialog.submitting }"
      @confirm="submitBind"
    >
      <p class="tip">{{ $t('pages.adminaccount.bind.body', { subject: bindDialog.subject }) }}</p>
      <t-form label-align="left" :label-width="110">
        <t-form-item :label="$t('pages.adminaccount.bind.userName')" name="userName">
          <t-input v-model="bindDialog.userName" :placeholder="$t('pages.adminaccount.bind.userNamePlaceholder')" />
        </t-form-item>
      </t-form>
    </t-dialog>
  </div>
</template>
<script setup lang="ts">
import { DialogPlugin, MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, reactive, ref } from 'vue';

import {
  addAdminAccount,
  bindAdminAccount,
  getAdminAccounts,
  removeAdminAccount,
  setAdminAccountEnabled,
  unbindAdminAccount,
} from '@/api/service';
import { t } from '@/locales';
import { useUserStore } from '@/store';

defineOptions({
  name: 'AdminAccountIndex',
});

interface AdminAccountRow {
  Id: number;
  Provider: string;
  Subject: string;
  UserName: string;
  Email?: string;
  IsEnabled: boolean;
  IsBootstrap: boolean;
  CreatedAt: string;
  LastLoginAt?: string;
  BoundUserId?: number;
  BoundUserName?: string;
  IsCurrent: boolean;
}

const userStore = useUserStore();
// 服务端按返回码兜底，这里只为体验把写操作按钮置灰
const canWrite = computed(() => userStore.userInfo.canWrite);

const accounts = ref<AdminAccountRow[]>([]);
const loading = ref(false);
const addDialog = reactive({ visible: false, submitting: false, subject: '', userName: '', email: '' });
const bindDialog = reactive({ visible: false, submitting: false, accountId: 0, subject: '', userName: '' });

const columns = computed(() => [
  { colKey: 'Subject', title: t('pages.adminaccount.colSubject'), minWidth: 200, ellipsis: true },
  { colKey: 'UserName', title: t('pages.adminaccount.colUserName'), minWidth: 140, ellipsis: true },
  { colKey: 'Email', title: t('pages.adminaccount.colEmail'), minWidth: 180, ellipsis: true },
  { colKey: 'bound', title: t('pages.adminaccount.colBound'), minWidth: 200, ellipsis: true },
  { colKey: 'state', title: t('pages.adminaccount.colState'), width: 220 },
  { colKey: 'CreatedAt', title: t('pages.adminaccount.colCreated'), width: 190 },
  { colKey: 'lastLogin', title: t('pages.adminaccount.colLastLogin'), width: 190 },
  { colKey: 'op', title: t('pages.adminaccount.colOp'), width: 250, fixed: 'right' as const },
]);

function formatTime(value: string) {
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('zh-CN');
}

async function load() {
  loading.value = true;
  try {
    const res: any = await getAdminAccounts();
    accounts.value = res.data ?? [];
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.adminaccount.messages.loadFail'));
  } finally {
    loading.value = false;
  }
}

function openAdd() {
  addDialog.subject = '';
  addDialog.userName = '';
  addDialog.email = '';
  addDialog.visible = true;
}

async function submitAdd() {
  const subject = addDialog.subject.trim();
  if (!subject) {
    MessagePlugin.warning(t('pages.adminaccount.add.subjectRequired'));
    return;
  }
  addDialog.submitting = true;
  try {
    const res: any = await addAdminAccount(subject, addDialog.userName.trim() || null, addDialog.email.trim() || null);
    MessagePlugin.success(res.message || t('pages.adminaccount.messages.addSucceed'));
    addDialog.visible = false;
    await load();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.adminaccount.messages.addFail'));
  } finally {
    addDialog.submitting = false;
  }
}

async function toggleEnabled(row: AdminAccountRow) {
  try {
    const res: any = await setAdminAccountEnabled(row.Id, !row.IsEnabled);
    MessagePlugin.success(res.message || t('pages.adminaccount.messages.toggleSucceed'));
    await load();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.adminaccount.messages.toggleFail'));
  }
}

function openBind(row: AdminAccountRow) {
  bindDialog.accountId = row.Id;
  bindDialog.subject = row.Subject;
  bindDialog.userName = row.BoundUserName || '';
  bindDialog.visible = true;
}

async function submitBind() {
  const userName = bindDialog.userName.trim();
  if (!userName) {
    MessagePlugin.warning(t('pages.adminaccount.bind.userNameRequired'));
    return;
  }
  bindDialog.submitting = true;
  try {
    const res: any = await bindAdminAccount(bindDialog.accountId, userName);
    MessagePlugin.success(res.message || t('pages.adminaccount.messages.bindSucceed'));
    bindDialog.visible = false;
    await load();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.adminaccount.messages.bindFail'));
  } finally {
    bindDialog.submitting = false;
  }
}

function confirmUnbind(row: AdminAccountRow) {
  const dialog = DialogPlugin.confirm({
    header: t('pages.adminaccount.unbind.header'),
    body: t('pages.adminaccount.unbind.body', { subject: row.Subject }),
    theme: 'warning',
    confirmBtn: t('pages.adminaccount.unbind.confirm'),
    onConfirm: async () => {
      dialog.hide();
      try {
        const res: any = await unbindAdminAccount(row.Id);
        MessagePlugin.success(res.message || t('pages.adminaccount.messages.unbindSucceed'));
        await load();
      } catch (err) {
        MessagePlugin.error((err as Error).message || t('pages.adminaccount.messages.unbindFail'));
      }
    },
  });
}

function confirmRemove(row: AdminAccountRow) {
  const dialog = DialogPlugin.confirm({
    header: t('pages.adminaccount.remove.header'),
    body: t('pages.adminaccount.remove.body', { subject: row.Subject }),
    theme: 'warning',
    confirmBtn: t('pages.adminaccount.remove.confirm'),
    onConfirm: async () => {
      dialog.hide();
      try {
        const res: any = await removeAdminAccount(row.Id);
        MessagePlugin.success(res.message || t('pages.adminaccount.messages.removeSucceed'));
        await load();
      } catch (err) {
        MessagePlugin.error((err as Error).message || t('pages.adminaccount.messages.removeFail'));
      }
    },
  });
}

onMounted(load);
</script>
<style lang="less" scoped>
.hutao-card {
  margin-bottom: 16px;
}

.tip {
  margin: 0 0 12px;
  color: var(--td-text-color-secondary);
}

.unbound {
  color: var(--td-text-color-placeholder);
}

.readonly-alert {
  margin-bottom: 12px;
}

.toolbar {
  margin-bottom: 16px;
}

.op-link {
  margin-left: 12px;
}
</style>
