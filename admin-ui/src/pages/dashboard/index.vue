<script setup lang="ts">
import { MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { getUserStatistics } from '@/api/service';
import { t } from '@/locales';
import { useUserStore } from '@/store';

const userStore = useUserStore();
const router = useRouter();
const refreshing = ref(false);

const statsLoading = ref(false);
const stats = ref<any>(null);

const statTiles = computed(() => {
  const s = stats.value;
  if (!s) return [];
  return [
    { label: t('pages.dashboard.stats.totalUsers'), value: s.TotalUsers },
    { label: t('pages.dashboard.stats.maintainers'), value: s.Maintainers },
    { label: t('pages.dashboard.stats.licensedDevelopers'), value: s.LicensedDevelopers },
    { label: t('pages.dashboard.stats.activeGachaLogUsers'), value: s.ActiveGachaLogUsers },
    { label: t('pages.dashboard.stats.activeCdnUsers'), value: s.ActiveCdnUsers },
    { label: t('pages.dashboard.stats.bannedUids'), value: s.BannedUids },
    { label: t('pages.dashboard.stats.gachaItemCount'), value: s.GachaItemCount },
    { label: t('pages.dashboard.stats.abyssRecords'), value: s.SpiralAbyssRecordCount },
    { label: t('pages.dashboard.stats.theaterRecords'), value: s.RoleCombatRecordCount },
  ];
});

async function loadStats() {
  statsLoading.value = true;
  try {
    const res: any = await getUserStatistics();
    stats.value = res.data ?? null;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.dashboard.messages.statsFail'));
  } finally {
    statsLoading.value = false;
  }
}

onMounted(loadStats);

function fmt(value?: string) {
  if (!value) return '-';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? String(value) : d.toLocaleString('zh-CN');
}

const items = computed(() => {
  const u = userStore.userInfo;
  if (!u?.name) return [];
  return [
    { label: t('pages.dashboard.account.email'), value: u.name },
    { label: t('pages.dashboard.account.maintainer'), value: u.isMaintainer ? t('pages.dashboard.account.yes') : t('pages.dashboard.account.no') },
    { label: t('pages.dashboard.account.licensedDeveloper'), value: u.isLicensedDeveloper ? t('pages.dashboard.account.yes') : t('pages.dashboard.account.no') },
    { label: t('pages.dashboard.account.gachaLogExpireAt'), value: fmt(u.gachaLogExpireAt) },
    { label: t('pages.dashboard.account.cdnExpireAt'), value: fmt(u.cdnExpireAt) },
  ];
});

const shortcuts = computed(() => [
  { title: t('pages.dashboard.shortcuts.abyssStats'), path: '/statistics' },
  { title: t('pages.dashboard.shortcuts.compensation'), path: '/compensation' },
  { title: t('pages.dashboard.shortcuts.announcement'), path: '/announcement' },
  { title: t('pages.dashboard.shortcuts.redeem'), path: '/redeem' },
]);

async function refresh() {
  refreshing.value = true;
  try {
    await userStore.getUserInfo();
    MessagePlugin.success(t('pages.dashboard.messages.refreshed'));
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.dashboard.messages.refreshFail'));
  } finally {
    refreshing.value = false;
  }
}
</script>

<template>
  <div class="hutao-page">
    <t-card :title="$t('pages.dashboard.overview.title')" class="hutao-card" :bordered="false">
      <template #actions>
        <t-button variant="outline" size="small" :loading="statsLoading" @click="loadStats">{{ $t('pages.dashboard.overview.refresh') }}</t-button>
      </template>
      <t-loading :loading="statsLoading" show-overlay>
        <div class="stat-grid">
          <div v-for="tile in statTiles" :key="tile.label" class="stat-tile">
            <div class="stat-value">{{ tile.value ?? '—' }}</div>
            <div class="stat-label">{{ tile.label }}</div>
          </div>
          <t-empty v-if="!statTiles.length && !statsLoading" :description="$t('pages.dashboard.overview.noData')" />
        </div>
      </t-loading>
    </t-card>

    <t-row :gutter="16">
      <t-col :span="6">
        <t-card :title="$t('pages.dashboard.account.title')" class="hutao-card" :bordered="false">
          <template #actions>
            <t-button variant="outline" size="small" :loading="refreshing" @click="refresh">{{ $t('pages.dashboard.account.refresh') }}</t-button>
          </template>
          <div v-for="it in items" :key="it.label" class="info-row">
            <span class="info-label">{{ it.label }}</span>
            <span class="info-value">{{ it.value }}</span>
          </div>
        </t-card>
      </t-col>
      <t-col :span="6">
        <t-card :title="$t('pages.dashboard.shortcuts.title')" :bordered="false" class="hutao-card">
          <t-space break-line>
            <t-button v-for="s in shortcuts" :key="s.path" theme="default" variant="outline" @click="router.push(s.path)">
              {{ s.title }}
            </t-button>
          </t-space>
        </t-card>
      </t-col>
    </t-row>
  </div>
</template>

<style lang="less" scoped>
.hutao-card {
  margin-bottom: 16px;
}
.info-row {
  display: flex;
  padding: 10px 0;
  border-bottom: 1px solid var(--td-component-stroke);
}
.info-row:last-child {
  border-bottom: none;
}
.info-label {
  width: 160px;
  color: var(--td-text-color-secondary);
}
.info-value {
  font-weight: 500;
}
.stat-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
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
</style>
