<script setup lang="ts">
import { MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, reactive, ref } from 'vue';

import {
  distributionDesignation,
  gachaLogDesignation,
  getUserDetail,
  getUserList,
  getUserStatistics,
  resetUserPassword,
  updateUserPrivilege,
} from '@/api/service';
import { t } from '@/locales';

const loadingStats = ref(false);
const stats = ref<any>(null);

const statTiles = computed(() => {
  const s = stats.value;
  if (!s) return [];
  return [
    { label: t('pages.user.stats.totalUsers'), value: s.TotalUsers },
    { label: t('pages.user.stats.maintainers'), value: s.Maintainers },
    { label: t('pages.user.stats.licensedDevelopers'), value: s.LicensedDevelopers },
    { label: t('pages.user.stats.activeGachaLogUsers'), value: s.ActiveGachaLogUsers },
    { label: t('pages.user.stats.activeCdnUsers'), value: s.ActiveCdnUsers },
    { label: t('pages.user.stats.bannedUids'), value: s.BannedUids },
    { label: t('pages.user.stats.gachaItemCount'), value: s.GachaItemCount },
    { label: t('pages.user.stats.abyssRecords'), value: s.SpiralAbyssRecordCount },
    { label: t('pages.user.stats.theaterRecords'), value: s.RoleCombatRecordCount },
  ];
});

const keyword = ref('');
const roleFilter = ref('');
const expiringDays = ref(0);
const loadingList = ref(false);
const users = ref<any[]>([]);
const pagination = reactive({ current: 1, pageSize: 20, total: 0 });

const columns = computed(() => [
  { colKey: 'Id', title: 'ID', width: 80 },
  { colKey: 'UserName', title: t('pages.user.list.colUserName'), ellipsis: true, minWidth: 200 },
  { colKey: 'IsMaintainer', title: t('pages.user.list.colMaintainer'), width: 90 },
  { colKey: 'IsLicensedDeveloper', title: t('pages.user.list.colDeveloper'), width: 90 },
  { colKey: 'GachaLogExpireAt', title: t('pages.user.list.colGachaExpireAt'), width: 190 },
  { colKey: 'CdnExpireAt', title: t('pages.user.list.colCdnExpireAt'), width: 190 },
  { colKey: 'op', title: t('pages.user.list.colOperation'), width: 280, fixed: 'right' },
]);

function fmt(sec: number) {
  if (!sec || sec <= 0) return '—';
  const d = new Date(sec * 1000);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('zh-CN');
}

const durationDialog = reactive({
  visible: false,
  submitting: false,
  userName: '',
  serviceType: 'gachalog',
  days: 30,
});

function openDuration(row: any) {
  durationDialog.userName = row.UserName ?? '';
  durationDialog.serviceType = 'gachalog';
  durationDialog.days = 30;
  durationDialog.visible = true;
}

async function submitDuration() {
  if (!durationDialog.userName) {
    MessagePlugin.warning(t('pages.user.messages.noUserName'));
    return;
  }
  durationDialog.submitting = true;
  try {
    const res: any =
      durationDialog.serviceType === 'gachalog'
        ? await gachaLogDesignation(durationDialog.userName, durationDialog.days)
        : await distributionDesignation(durationDialog.userName, durationDialog.days);
    MessagePlugin.success(res.message || t('pages.user.messages.opSuccess'));
    durationDialog.visible = false;
    await Promise.all([loadStats(), loadUsers()]);
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.user.messages.opFail'));
  } finally {
    durationDialog.submitting = false;
  }
}

const privilegeDialog = reactive({
  visible: false,
  submitting: false,
  userId: 0,
  userName: '',
  isMaintainer: false,
  isLicensedDeveloper: false,
});

function openPrivilege(row: any) {
  privilegeDialog.userId = row.Id;
  privilegeDialog.userName = row.UserName ?? '';
  privilegeDialog.isMaintainer = !!row.IsMaintainer;
  privilegeDialog.isLicensedDeveloper = !!row.IsLicensedDeveloper;
  privilegeDialog.visible = true;
}

async function submitPrivilege() {
  privilegeDialog.submitting = true;
  try {
    const res: any = await updateUserPrivilege({
      UserId: privilegeDialog.userId,
      IsMaintainer: privilegeDialog.isMaintainer,
      IsLicensedDeveloper: privilegeDialog.isLicensedDeveloper,
    });
    MessagePlugin.success(res.message || t('pages.user.messages.updateSuccess'));
    privilegeDialog.visible = false;
    await Promise.all([loadStats(), loadUsers()]);
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.user.messages.updateFail'));
  } finally {
    privilegeDialog.submitting = false;
  }
}

const passwordDialog = reactive({
  visible: false,
  submitting: false,
  userId: 0,
  userName: '',
  newPassword: '',
});

function openPassword(row: any) {
  passwordDialog.userId = row.Id;
  passwordDialog.userName = row.UserName ?? '';
  passwordDialog.newPassword = '';
  passwordDialog.visible = true;
}

async function submitPassword() {
  if (passwordDialog.newPassword.length < 8) {
    MessagePlugin.warning(t('pages.user.messages.passwordTooShort'));
    return;
  }
  passwordDialog.submitting = true;
  try {
    const res: any = await resetUserPassword({
      UserId: passwordDialog.userId,
      NewPassword: passwordDialog.newPassword,
    });
    MessagePlugin.success(res.message || t('pages.user.messages.resetPasswordSuccess'));
    passwordDialog.visible = false;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.user.messages.resetPasswordFail'));
  } finally {
    passwordDialog.submitting = false;
  }
}

const detailDialog = reactive({
  visible: false,
  loading: false,
  data: null as any,
});

const detailColumns = computed(() => [
  { colKey: 'Uid', title: 'UID' },
  { colKey: 'ItemCount', title: t('pages.user.detailDialog.colGachaItemCount'), width: 120 },
]);

async function openDetail(row: any) {
  detailDialog.visible = true;
  detailDialog.loading = true;
  detailDialog.data = null;
  try {
    const res: any = await getUserDetail(row.Id);
    detailDialog.data = res.data ?? null;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.user.messages.detailFail'));
  } finally {
    detailDialog.loading = false;
  }
}
async function loadStats() {
  loadingStats.value = true;
  try {
    const res: any = await getUserStatistics();
    stats.value = res.data ?? null;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.user.messages.statsFail'));
  } finally {
    loadingStats.value = false;
  }
}

async function loadUsers() {
  loadingList.value = true;
  try {
    const filters: any = {};
    if (roleFilter.value === 'maintainer') filters.maintainer = true;
    else if (roleFilter.value === 'developer') filters.developer = true;
    if (expiringDays.value > 0) filters.expiringInDays = expiringDays.value;
    const res: any = await getUserList(pagination.current, pagination.pageSize, keyword.value.trim(), filters);
    const d = res.data ?? {};
    users.value = d.Items ?? [];
    pagination.total = d.Total ?? 0;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.user.messages.userListFail'));
  } finally {
    loadingList.value = false;
  }
}

function onFilterChange() {
  pagination.current = 1;
  loadUsers();
}

function onSearch() {
  pagination.current = 1;
  loadUsers();
}

function onPageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
  loadUsers();
}

onMounted(() => {
  loadStats();
  loadUsers();
});
</script>

<template>
  <div class="hutao-page">
    <t-card :title="$t('pages.user.overview.title')" :bordered="false" class="hutao-card">
      <template #actions>
        <t-button variant="outline" size="small" :loading="loadingStats" @click="loadStats">{{ $t('pages.user.overview.refresh') }}</t-button>
      </template>
      <t-loading :loading="loadingStats" show-overlay>
        <div class="stat-grid">
          <div v-for="tile in statTiles" :key="tile.label" class="stat-tile">
            <div class="stat-value">{{ tile.value ?? '—' }}</div>
            <div class="stat-label">{{ tile.label }}</div>
          </div>
          <t-empty v-if="!statTiles.length" :description="$t('pages.user.overview.noData')" />
        </div>
      </t-loading>
    </t-card>

    <t-card :title="$t('pages.user.list.title')" :bordered="false" class="hutao-card">
      <t-space class="search-bar" break-line>
        <t-input
          v-model="keyword"
          :placeholder="$t('pages.user.list.searchPlaceholder')"
          clearable
          style="width: 280px"
          @enter="onSearch"
          @clear="onSearch"
        />
        <t-select
          v-model="roleFilter"
          :options="[
            { label: $t('pages.user.list.allRoles'), value: '' },
            { label: $t('pages.user.list.maintainersOnly'), value: 'maintainer' },
            { label: $t('pages.user.list.developersOnly'), value: 'developer' },
          ]"
          style="width: 140px"
          @change="onFilterChange"
        />
        <t-select
          v-model="expiringDays"
          :options="[
            { label: $t('pages.user.list.expiryAny'), value: 0 },
            { label: $t('pages.user.list.expiry7Days'), value: 7 },
            { label: $t('pages.user.list.expiry30Days'), value: 30 },
          ]"
          style="width: 150px"
          @change="onFilterChange"
        />
        <t-button theme="primary" :loading="loadingList" @click="onSearch">{{ $t('pages.user.list.search') }}</t-button>
      </t-space>
      <t-table
        row-key="Id"
        :data="users"
        :columns="columns"
        :loading="loadingList"
        :pagination="pagination"
        size="small"
        @page-change="onPageChange"
      >
        <template #IsMaintainer="{ row }">
          <t-tag v-if="row.IsMaintainer" theme="success" variant="light">{{ $t('pages.user.list.yes') }}</t-tag>
          <span v-else>—</span>
        </template>
        <template #IsLicensedDeveloper="{ row }">
          <t-tag v-if="row.IsLicensedDeveloper" theme="primary" variant="light">{{ $t('pages.user.list.yes') }}</t-tag>
          <span v-else>—</span>
        </template>
        <template #GachaLogExpireAt="{ row }">{{ fmt(row.GachaLogExpireAt) }}</template>
        <template #CdnExpireAt="{ row }">{{ fmt(row.CdnExpireAt) }}</template>
        <template #op="{ row }">
          <t-space size="small">
            <t-link theme="primary" hover="color" @click="openDetail(row)">{{ $t('pages.user.list.detail') }}</t-link>
            <t-link theme="primary" hover="color" @click="openDuration(row)">{{ $t('pages.user.list.adjustDuration') }}</t-link>
            <t-link theme="primary" hover="color" @click="openPrivilege(row)">{{ $t('pages.user.list.privilege') }}</t-link>
            <t-link theme="warning" hover="color" @click="openPassword(row)">{{ $t('pages.user.list.resetPassword') }}</t-link>
          </t-space>
        </template>
      </t-table>
    </t-card>

    <t-dialog
      v-model:visible="durationDialog.visible"
      :header="$t('pages.user.durationDialog.header')"
      :confirm-btn="{ content: $t('pages.user.durationDialog.submit'), loading: durationDialog.submitting }"
      @confirm="submitDuration"
    >
      <t-form label-align="top">
        <t-form-item :label="$t('pages.user.durationDialog.targetUser')">
          <t-input :value="durationDialog.userName" readonly />
        </t-form-item>
        <t-form-item :label="$t('pages.user.durationDialog.serviceType')">
          <t-radio-group v-model="durationDialog.serviceType">
            <t-radio-button value="gachalog">{{ $t('pages.user.durationDialog.gachalog') }}</t-radio-button>
            <t-radio-button value="cdn">{{ $t('pages.user.durationDialog.cdn') }}</t-radio-button>
          </t-radio-group>
        </t-form-item>
        <t-form-item :label="$t('pages.user.durationDialog.daysLabel')">
          <t-input-number v-model="durationDialog.days" :min="-3650" :max="3650" theme="normal" style="width: 200px" />
        </t-form-item>
      </t-form>
    </t-dialog>

    <t-dialog
      v-model:visible="privilegeDialog.visible"
      :header="$t('pages.user.privilegeDialog.header')"
      :confirm-btn="{ content: $t('pages.user.privilegeDialog.save'), loading: privilegeDialog.submitting }"
      @confirm="submitPrivilege"
    >
      <t-form label-align="left" :label-width="120">
        <t-form-item :label="$t('pages.user.privilegeDialog.targetUser')">
          <t-input :value="privilegeDialog.userName" readonly />
        </t-form-item>
        <t-form-item :label="$t('pages.user.privilegeDialog.maintainer')">
          <t-switch v-model="privilegeDialog.isMaintainer" />
        </t-form-item>
        <t-form-item :label="$t('pages.user.privilegeDialog.licensedDeveloper')">
          <t-switch v-model="privilegeDialog.isLicensedDeveloper" />
        </t-form-item>
      </t-form>
    </t-dialog>

    <t-dialog
      v-model:visible="passwordDialog.visible"
      :header="$t('pages.user.passwordDialog.header')"
      :confirm-btn="{ content: $t('pages.user.passwordDialog.reset'), theme: 'warning', loading: passwordDialog.submitting }"
      @confirm="submitPassword"
    >
      <t-form label-align="top">
        <t-form-item :label="$t('pages.user.passwordDialog.targetUser')">
          <t-input :value="passwordDialog.userName" readonly />
        </t-form-item>
        <t-form-item :label="$t('pages.user.passwordDialog.newPasswordLabel')">
          <t-input
            v-model="passwordDialog.newPassword"
            type="password"
            :placeholder="$t('pages.user.passwordDialog.newPasswordPlaceholder')"
            clearable
            @enter="submitPassword"
          />
        </t-form-item>
      </t-form>
    </t-dialog>

    <t-dialog
      v-model:visible="detailDialog.visible"
      :header="$t('pages.user.detailDialog.header')"
      width="640px"
      :footer="false"
    >
      <t-loading :loading="detailDialog.loading" show-overlay>
        <div v-if="detailDialog.data" class="detail-body">
          <t-descriptions :column="2" size="small">
            <t-descriptions-item :label="$t('pages.user.detailDialog.colUserName')">{{ detailDialog.data.UserName || '—' }}{{ $t('pages.user.detailDialog.idSuffix', { id: detailDialog.data.Id }) }}</t-descriptions-item>
            <t-descriptions-item :label="$t('pages.user.detailDialog.redeemUsedCount')">{{ detailDialog.data.RedeemUsedCount }}</t-descriptions-item>
            <t-descriptions-item :label="$t('pages.user.detailDialog.maintainer')">{{ detailDialog.data.IsMaintainer ? $t('pages.user.detailDialog.yes') : $t('pages.user.detailDialog.no') }}</t-descriptions-item>
            <t-descriptions-item :label="$t('pages.user.detailDialog.licensedDeveloper')">{{ detailDialog.data.IsLicensedDeveloper ? $t('pages.user.detailDialog.yes') : $t('pages.user.detailDialog.no') }}</t-descriptions-item>
            <t-descriptions-item :label="$t('pages.user.detailDialog.gachaExpireAt')">{{ fmt(detailDialog.data.GachaLogExpireAt) }}</t-descriptions-item>
            <t-descriptions-item :label="$t('pages.user.detailDialog.cdnExpireAt')">{{ fmt(detailDialog.data.CdnExpireAt) }}</t-descriptions-item>
          </t-descriptions>
          <div class="detail-subtitle">{{ $t('pages.user.detailDialog.gachaUidTitle', { n: detailDialog.data.GachaUids?.length || 0 }) }}</div>
          <t-table
            row-key="Uid"
            :data="detailDialog.data.GachaUids || []"
            :columns="detailColumns"
            size="small"
            max-height="240"
          />
        </div>
        <t-empty v-else-if="!detailDialog.loading" :description="$t('pages.user.detailDialog.noData')" />
      </t-loading>
    </t-dialog>
  </div>
</template>

<style lang="less" scoped>
.hutao-card {
  margin-bottom: 16px;
}
.stat-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
  gap: 12px;
  min-height: 40px;
}
.stat-tile {
  padding: 16px;
  border: 1px solid var(--td-component-stroke);
  border-radius: var(--td-radius-medium);
  background: var(--td-bg-color-container);
}
.stat-value {
  font-size: 24px;
  font-weight: 600;
  color: var(--td-text-color-primary);
}
.stat-label {
  margin-top: 4px;
  color: var(--td-text-color-secondary);
  font-size: 13px;
}
.search-bar {
  margin-bottom: 16px;
}
.detail-subtitle {
  margin: 16px 0 8px;
  font-weight: 600;
}
</style>


