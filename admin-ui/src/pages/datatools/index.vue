<script setup lang="ts">
import { DialogPlugin, MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, reactive, ref } from 'vue';

import {
  addAllowedVersion,
  getAllowedVersions,
  getInvalidGachaUids,
  removeAllowedVersion,
  removeInvalidGachaUid,
} from '@/api/service';
import { t } from '@/locales';

const newVersion = ref('');
const addingVersion = ref(false);
const versionsLoading = ref(false);
const versions = ref<string[]>([]);

async function loadVersions() {
  versionsLoading.value = true;
  try {
    const res: any = await getAllowedVersions();
    versions.value = res.data ?? [];
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.datatools.messages.loadVersionsFail'));
  } finally {
    versionsLoading.value = false;
  }
}

async function addVersion() {
  const header = newVersion.value.trim();
  if (!header) {
    MessagePlugin.warning(t('pages.datatools.messages.headerRequired'));
    return;
  }
  addingVersion.value = true;
  try {
    const res: any = await addAllowedVersion(header);
    MessagePlugin.success(res.message || t('pages.datatools.messages.addSuccess'));
    newVersion.value = '';
    await loadVersions();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.datatools.messages.addFail'));
  } finally {
    addingVersion.value = false;
  }
}

function removeVersion(header: string) {
  const dialog = DialogPlugin.confirm({
    header: t('pages.datatools.dialog.removeVersionHeader'),
    body: t('pages.datatools.dialog.removeVersionBody', { header }),
    theme: 'warning',
    confirmBtn: t('pages.datatools.dialog.removeVersionConfirm'),
    onConfirm: async () => {
      dialog.hide();
      try {
        const res: any = await removeAllowedVersion(header);
        MessagePlugin.success(res.message || t('pages.datatools.messages.removeSuccess'));
        await loadVersions();
      } catch (err) {
        MessagePlugin.error((err as Error).message || t('pages.datatools.messages.removeFail'));
      }
    },
  });
}
const keyword = ref('');
const uidLoading = ref(false);
const uidRows = ref<{ uid: string }[]>([]);
const pagination = reactive({ current: 1, pageSize: 20, total: 0 });

const uidColumns = computed(() => [
  { colKey: 'uid', title: 'UID' },
  { colKey: 'op', title: t('pages.datatools.uids.colOp'), width: 120, fixed: 'right' },
]);

async function loadUids() {
  uidLoading.value = true;
  try {
    const res: any = await getInvalidGachaUids(pagination.current, pagination.pageSize, keyword.value.trim());
    const d = res.data ?? {};
    uidRows.value = (d.Items ?? []).map((u: string) => ({ uid: u }));
    pagination.total = d.Total ?? 0;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.datatools.messages.loadUidsFail'));
  } finally {
    uidLoading.value = false;
  }
}

function onSearch() {
  pagination.current = 1;
  loadUids();
}

function onPageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
  loadUids();
}

function removeUid(uid: string) {
  const dialog = DialogPlugin.confirm({
    header: t('pages.datatools.dialog.removeUidHeader'),
    body: t('pages.datatools.dialog.removeUidBody', { uid }),
    confirmBtn: t('pages.datatools.dialog.removeUidConfirm'),
    onConfirm: async () => {
      dialog.hide();
      try {
        const res: any = await removeInvalidGachaUid(uid);
        MessagePlugin.success(res.message || t('pages.datatools.messages.removeSuccess'));
        await loadUids();
      } catch (err) {
        MessagePlugin.error((err as Error).message || t('pages.datatools.messages.removeFail'));
      }
    },
  });
}

onMounted(() => {
  loadVersions();
  loadUids();
});
</script>

<template>
  <div class="hutao-page">
    <t-row :gutter="16">
      <t-col :span="5">
        <t-card :title="$t('pages.datatools.versions.title')" :bordered="false" class="hutao-card">
          <p class="tip">{{ $t('pages.datatools.versions.tipPre') }}<code>allowed_versions</code>{{ $t('pages.datatools.versions.tipSuf') }}</p>
          <t-space class="bar" break-line>
            <t-input v-model="newVersion" :placeholder="$t('pages.datatools.versions.inputPlaceholder')" clearable @enter="addVersion" />
            <t-button theme="primary" :loading="addingVersion" @click="addVersion">{{ $t('pages.datatools.versions.add') }}</t-button>
            <t-button variant="outline" :loading="versionsLoading" @click="loadVersions">{{ $t('pages.datatools.versions.refresh') }}</t-button>
          </t-space>
          <div class="version-list">
            <t-tag
              v-for="v in versions"
              :key="v"
              theme="primary"
              variant="light"
              closable
              @close="removeVersion(v)"
            >
              {{ v }}
            </t-tag>
          </div>
          <t-empty v-if="!versions.length && !versionsLoading" :description="$t('pages.datatools.versions.empty')" />
        </t-card>
      </t-col>
      <t-col :span="7">
        <t-card :title="$t('pages.datatools.uids.title')" :bordered="false" class="hutao-card">
          <p class="tip">{{ $t('pages.datatools.uids.tipPre') }}<code>invalid_gacha_uids</code>{{ $t('pages.datatools.uids.tipSuf') }}</p>
          <t-space class="bar">
            <t-input v-model="keyword" :placeholder="$t('pages.datatools.uids.searchPlaceholder')" clearable style="width: 260px" @enter="onSearch" @clear="onSearch" />
            <t-button theme="primary" :loading="uidLoading" @click="onSearch">{{ $t('pages.datatools.uids.search') }}</t-button>
          </t-space>
          <t-table
            row-key="uid"
            :data="uidRows"
            :columns="uidColumns"
            :loading="uidLoading"
            :pagination="pagination"
            size="small"
            @page-change="onPageChange"
          >
            <template #op="{ row }">
              <t-link theme="primary" hover="color" @click="removeUid(row.uid)">{{ $t('pages.datatools.uids.remove') }}</t-link>
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
.bar {
  margin-bottom: 16px;
}
.version-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
}
</style>

