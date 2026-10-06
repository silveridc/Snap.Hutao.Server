<script setup lang="ts">
import { DialogPlugin, MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, reactive, ref } from 'vue';

import { banUid, batchBanUids, getBanList, unbanUid } from '@/api/service';
import { t } from '@/locales';

const newUid = ref('');
const newReason = ref('');
const banning = ref(false);

const batchText = ref('');
const batchReason = ref('');
const batchLoading = ref(false);

const keyword = ref('');
const loading = ref(false);
const rows = ref<any[]>([]);
const pagination = reactive({ current: 1, pageSize: 20, total: 0 });

const columns = computed(() => [
  { colKey: 'Uid', title: 'UID', width: 140 },
  { colKey: 'Reason', title: t('pages.ban.list.colReason'), ellipsis: true, minWidth: 200 },
  { colKey: 'BanTime', title: t('pages.ban.list.colBanTime'), width: 190 },
  { colKey: 'op', title: t('pages.ban.list.colOp'), width: 100, fixed: 'right' },
]);

function fmt(sec?: number) {
  if (!sec || sec <= 0) return '—';
  const d = new Date(sec * 1000);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('zh-CN');
}

async function loadList() {
  loading.value = true;
  try {
    const res: any = await getBanList(pagination.current, pagination.pageSize, keyword.value.trim());
    const d = res.data ?? {};
    rows.value = d.Items ?? [];
    pagination.total = d.Total ?? 0;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.ban.messages.listFail'));
  } finally {
    loading.value = false;
  }
}
async function addBan() {
  const uid = newUid.value.trim();
  if (!/^\d{9,10}$/.test(uid)) {
    MessagePlugin.warning(t('pages.ban.messages.uidFormat'));
    return;
  }
  banning.value = true;
  try {
    const res: any = await banUid(uid, newReason.value.trim());
    MessagePlugin.success(res.message || t('pages.ban.messages.banSuccess'));
    newUid.value = '';
    newReason.value = '';
    pagination.current = 1;
    await loadList();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.ban.messages.banFail'));
  } finally {
    banning.value = false;
  }
}

async function batchBan() {
  const uids = batchText.value
    .split(/[\s,，]+/)
    .map((u) => u.trim())
    .filter(Boolean);
  const invalid = uids.filter((u) => !/^\d{9,10}$/.test(u));
  if (!uids.length) {
    MessagePlugin.warning(t('pages.ban.messages.emptyUids'));
    return;
  }
  if (invalid.length) {
    MessagePlugin.warning(t('pages.ban.messages.invalidUids', { count: invalid.length }));
    return;
  }
  batchLoading.value = true;
  try {
    const res: any = await batchBanUids(uids, batchReason.value.trim());
    MessagePlugin.success(res.message || t('pages.ban.messages.batchSuccess'));
    batchText.value = '';
    batchReason.value = '';
    pagination.current = 1;
    await loadList();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.ban.messages.batchFail'));
  } finally {
    batchLoading.value = false;
  }
}

function removeBan(uid: string) {
  const dialog = DialogPlugin.confirm({
    header: t('pages.ban.dialog.unbanHeader'),
    body: t('pages.ban.dialog.unbanBody', { uid }),
    confirmBtn: t('pages.ban.dialog.unbanConfirm'),
    onConfirm: async () => {
      dialog.hide();
      try {
        const res: any = await unbanUid(uid);
        MessagePlugin.success(res.message || t('pages.ban.messages.unbanSuccess'));
        await loadList();
      } catch (err) {
        MessagePlugin.error((err as Error).message || t('pages.ban.messages.unbanFail'));
      }
    },
  });
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
</script>

<template>
  <div class="hutao-page">
    <t-row :gutter="16">
      <t-col :span="5">
        <t-card :title="$t('pages.ban.add.title')" :bordered="false" class="hutao-card">
          <p class="tip">{{ $t('pages.ban.add.tipPre') }}<code>POST /Service/Ban/Add</code>{{ $t('pages.ban.add.tipSuf') }}</p>
          <t-form label-align="top">
            <t-form-item label="UID">
              <t-input v-model="newUid" :placeholder="$t('pages.ban.add.uidPlaceholder')" clearable @enter="addBan" />
            </t-form-item>
            <t-form-item :label="$t('pages.ban.add.reasonLabel')">
              <t-input v-model="newReason" :placeholder="$t('pages.ban.add.reasonPlaceholder')" clearable @enter="addBan" />
            </t-form-item>
            <t-form-item>
              <t-button theme="danger" :loading="banning" @click="addBan">{{ $t('pages.ban.add.submit') }}</t-button>
            </t-form-item>
          </t-form>
        </t-card>
      </t-col>
      <t-col :span="7">
        <t-card :title="$t('pages.ban.batch.title')" :bordered="false" class="hutao-card">
          <p class="tip">{{ $t('pages.ban.batch.tip') }}</p>
          <t-form label-align="top">
            <t-form-item :label="$t('pages.ban.batch.uidListLabel')">
              <t-textarea v-model="batchText" :placeholder="$t('pages.ban.batch.uidListPlaceholder')" :autosize="{ minRows: 3, maxRows: 10 }" />
            </t-form-item>
            <t-form-item :label="$t('pages.ban.batch.reasonLabel')">
              <t-input v-model="batchReason" :placeholder="$t('pages.ban.batch.reasonPlaceholder')" clearable @enter="batchBan" />
            </t-form-item>
            <t-form-item>
              <t-button theme="danger" :loading="batchLoading" @click="batchBan">{{ $t('pages.ban.batch.submit') }}</t-button>
            </t-form-item>
          </t-form>
        </t-card>
      </t-col>
    </t-row>

    <t-card :title="$t('pages.ban.list.title')" :bordered="false" class="hutao-card">
      <t-space class="search-bar">
        <t-input v-model="keyword" :placeholder="$t('pages.ban.list.searchPlaceholder')" clearable style="width: 260px" @enter="onSearch" @clear="onSearch" />
        <t-button theme="primary" :loading="loading" @click="onSearch">{{ $t('pages.ban.list.search') }}</t-button>
      </t-space>
      <t-table
        row-key="Uid"
        :data="rows"
        :columns="columns"
        :loading="loading"
        :pagination="pagination"
        size="small"
        @page-change="onPageChange"
      >
        <template #Reason="{ row }">{{ row.Reason || '—' }}</template>
        <template #BanTime="{ row }">{{ fmt(row.BanTime) }}</template>
        <template #op="{ row }">
          <t-link theme="danger" hover="color" @click="removeBan(row.Uid)">{{ $t('pages.ban.list.unban') }}</t-link>
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
.search-bar {
  margin-bottom: 16px;
}
</style>

