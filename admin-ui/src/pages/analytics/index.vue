<script setup lang="ts">
import { MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, ref } from 'vue';

import { getAbyssAnalytics, getDamageAnalytics, getGachaAnalytics } from '@/api/service';
import { t } from '@/locales';

const loading = ref(false);
const gacha = ref<any>(null);
const abyss = ref<any>(null);
const damage = ref<any>(null);
const days = ref(30);

const dayOptions = computed(() => [
  { label: t('pages.analytics.gacha.days7'), value: 7 },
  { label: t('pages.analytics.gacha.days30'), value: 30 },
  { label: t('pages.analytics.gacha.days90'), value: 90 },
  { label: t('pages.analytics.gacha.daysAll'), value: 0 },
]);

const poolName = (type: number) => t(`pages.analytics.gacha.pool.${type}`);

const gachaPools = computed<any[]>(() => gacha.value?.Pools ?? []);
const gachaTop = computed<any[]>(() => gacha.value?.TopStar5Items ?? []);
const maxItemCount = computed(() => Math.max(1, ...gachaTop.value.map((item: any) => item.Count)));

const spiral = computed(() => abyss.value?.SpiralAbyss ?? null);
const theater = computed(() => abyss.value?.Theater ?? null);

const damageTop = computed<any[]>(() => damage.value?.TopAvatars ?? []);
const maxDamage = computed(() => Math.max(1, ...damageTop.value.map((item: any) => item.TotalDamage)));

const gachaColumns = computed(() => [
  { colKey: 'pool', title: t('pages.analytics.gacha.colPool'), width: 160 },
  { colKey: 'Pulls', title: t('pages.analytics.gacha.colPulls'), width: 120, align: 'right' },
  { colKey: 'Star5', title: t('pages.analytics.gacha.colStar5'), width: 110, align: 'right' },
  { colKey: 'rate', title: t('pages.analytics.gacha.colRate'), width: 120, align: 'right' },
  { colKey: 'average', title: t('pages.analytics.gacha.colAverage'), minWidth: 140, align: 'right' },
]);

const damageColumns = computed(() => [
  { colKey: 'rank', title: t('pages.analytics.damage.colRank'), width: 70 },
  { colKey: 'AvatarId', title: t('pages.analytics.damage.colAvatar'), width: 120 },
  { colKey: 'TotalDamage', title: t('pages.analytics.damage.colDamage'), minWidth: 160, align: 'right' },
  { colKey: 'share', title: t('pages.analytics.damage.colShare'), width: 100, align: 'right' },
  { colKey: 'bar', title: '', minWidth: 260 },
]);

const floorColumns = computed(() => [
  { colKey: 'Index', title: t('pages.analytics.abyss.colFloor'), width: 100 },
  { colKey: 'Records', title: t('pages.analytics.abyss.colRecords'), width: 120, align: 'right' },
  { colKey: 'AverageStar', title: t('pages.analytics.abyss.colAverageStar'), minWidth: 140, align: 'right' },
]);

async function load() {
  loading.value = true;
  try {
    const [gachaRes, abyssRes, damageRes] = await Promise.all([
      getGachaAnalytics(days.value),
      getAbyssAnalytics(),
      getDamageAnalytics(10),
    ]);
    gacha.value = (gachaRes as any).data ?? null;
    abyss.value = (abyssRes as any).data ?? null;
    damage.value = (damageRes as any).data ?? null;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.analytics.messages.loadFail'));
  } finally {
    loading.value = false;
  }
}

onMounted(load);

function fmtInt(value?: number) {
  return value === null || value === undefined ? '—' : Number(value).toLocaleString('zh-CN');
}

function fmtPercent(value?: number) {
  return value === null || value === undefined ? '—' : `${(value * 100).toFixed(2)}%`;
}

function fmtNumber(value?: number, digits = 2) {
  return value === null || value === undefined ? '—' : Number(value).toFixed(digits);
}

function avatarName(id: number) {
  return String(id);
}
</script>

<template>
  <div class="hutao-page analytics">
    <t-loading :loading="loading" show-overlay>
      <t-card :title="$t('pages.analytics.gacha.title')" :bordered="false" class="hutao-card">
        <template #actions>
          <t-radio-group v-model="days" variant="default-filled" size="small" @change="load">
            <t-radio-button v-for="opt in dayOptions" :key="opt.value" :value="opt.value">{{ opt.label }}</t-radio-button>
          </t-radio-group>
        </template>

        <div class="facts facts-4">
          <div class="fact">
            <div class="fact-value">{{ fmtInt(gacha?.TotalPulls) }}</div>
            <div class="fact-label">{{ $t('pages.analytics.gacha.totalPulls') }}</div>
          </div>
          <div class="fact">
            <div class="fact-value">{{ fmtInt(gacha?.TotalStar5) }}</div>
            <div class="fact-label">{{ $t('pages.analytics.gacha.totalStar5') }}</div>
          </div>
          <div class="fact">
            <div class="fact-value">{{ gacha?.TotalPulls ? fmtPercent(gacha.TotalStar5 / gacha.TotalPulls) : '—' }}</div>
            <div class="fact-label">{{ $t('pages.analytics.gacha.star5Rate') }}</div>
          </div>
          <div class="fact">
            <div class="fact-value">{{ fmtInt(gacha?.UntrustedPulls) }}</div>
            <div class="fact-label">{{ $t('pages.analytics.gacha.untrusted') }}</div>
          </div>
        </div>

        <t-table row-key="pool" :data="gachaPools" :columns="gachaColumns" size="small" cell-empty-content="—" class="sub-table">
          <template #empty>{{ $t('pages.analytics.messages.noData') }}</template>
          <template #pool="{ row }">{{ poolName(row?.GachaType) }}</template>
          <template #Pulls="{ row }">{{ fmtInt(row?.Pulls) }}</template>
          <template #Star5="{ row }">{{ fmtInt(row?.Star5) }}</template>
          <template #rate="{ row }">{{ fmtPercent(row?.Star5Rate) }}</template>
          <template #average="{ row }">{{ fmtNumber(row?.AveragePullsPerStar5) }}</template>
        </t-table>

        <div v-if="gachaTop.length" class="bar-list">
          <div class="bar-title">{{ $t('pages.analytics.gacha.topItems') }}</div>
          <div v-for="item in gachaTop" :key="item.ItemId" class="bar-row">
            <span class="bar-name mono">{{ item.ItemId }}</span>
            <div class="bar-track">
              <div class="bar-fill" :style="{ width: `${Math.max(4, (item.Count / maxItemCount) * 100)}%` }" />
            </div>
            <span class="bar-value">{{ fmtInt(item.Count) }}</span>
          </div>
        </div>
      </t-card>

      <t-card :title="$t('pages.analytics.abyss.title')" :bordered="false" class="hutao-card">
        <t-row :gutter="16" class="abyss-row">
          <t-col :span="6" class="abyss-col">
            <t-card :title="$t('pages.analytics.abyss.spiral')" :bordered="false" class="inner-card">
              <div class="facts facts-3">
                <div class="fact">
                  <div class="fact-value">{{ fmtInt(spiral?.Records) }}</div>
                  <div class="fact-label">{{ $t('pages.analytics.abyss.records') }}</div>
                </div>
                <div class="fact">
                  <div class="fact-value">{{ fmtInt(spiral?.UniqueUids) }}</div>
                  <div class="fact-label">{{ $t('pages.analytics.abyss.uniqueUids') }}</div>
                </div>
                <div class="fact">
                  <div class="fact-value">{{ fmtInt(spiral?.FullStarRecords) }}</div>
                  <div class="fact-label">{{ $t('pages.analytics.abyss.fullStar') }}</div>
                </div>
              </div>
              <t-table row-key="Index" :data="spiral?.Floors ?? []" :columns="floorColumns" size="small" cell-empty-content="—">
                <template #empty>{{ $t('pages.analytics.messages.noData') }}</template>
                <template #AverageStar="{ row }">{{ fmtNumber(row?.AverageStar) }}</template>
              </t-table>
            </t-card>
          </t-col>
          <t-col :span="6" class="abyss-col">
            <t-card :title="$t('pages.analytics.abyss.theater')" :bordered="false" class="inner-card">
              <div class="facts facts-2">
                <div class="fact">
                  <div class="fact-value">{{ fmtInt(theater?.Records) }}</div>
                  <div class="fact-label">{{ $t('pages.analytics.abyss.records') }}</div>
                </div>
                <div class="fact">
                  <div class="fact-value">{{ fmtInt(theater?.UniqueUids) }}</div>
                  <div class="fact-label">{{ $t('pages.analytics.abyss.uniqueUids') }}</div>
                </div>
              </div>
              <div class="bar-title">{{ $t('pages.analytics.abyss.topAvatars') }}</div>
              <div v-for="avatar in theater?.TopAvatars ?? []" :key="avatar.AvatarId" class="chip-row">
                <span class="mono">{{ avatarName(avatar.AvatarId) }}</span>
                <span class="chip-count">{{ fmtInt(avatar.Count) }}</span>
              </div>
              <t-empty v-if="!(theater?.TopAvatars ?? []).length" :description="$t('pages.analytics.messages.noData')" />
            </t-card>
          </t-col>
        </t-row>

        <div class="bar-title">{{ $t('pages.analytics.abyss.spiralTop') }}</div>
        <div class="chip-wrap">
          <t-tag v-for="avatar in spiral?.TopAvatars ?? []" :key="avatar.AvatarId" variant="light" class="chip">
            {{ avatarName(avatar.AvatarId) }} · {{ fmtInt(avatar.Count) }}
          </t-tag>
          <span v-if="!(spiral?.TopAvatars ?? []).length" class="muted">{{ $t('pages.analytics.messages.noData') }}</span>
        </div>
      </t-card>

      <t-card :title="$t('pages.analytics.damage.title')" :bordered="false" class="hutao-card">
        <p class="tip">{{ $t('pages.analytics.damage.tip') }}</p>
        <div class="facts facts-2">
          <div class="fact">
            <div class="fact-value">{{ fmtInt(damage?.TotalDamage) }}</div>
            <div class="fact-label">{{ $t('pages.analytics.damage.totalDamage') }}</div>
          </div>
          <div class="fact">
            <div class="fact-value">{{ fmtInt(damage?.SampleRecords) }}</div>
            <div class="fact-label">{{ $t('pages.analytics.damage.samples') }}</div>
          </div>
        </div>
        <t-table row-key="AvatarId" :data="damageTop" :columns="damageColumns" size="small" cell-empty-content="—">
          <template #empty>{{ $t('pages.analytics.messages.noData') }}</template>
          <template #rank="{ row, rowIndex }">{{ (rowIndex ?? 0) + 1 }}</template>
          <template #TotalDamage="{ row }">{{ fmtInt(row?.TotalDamage) }}</template>
          <template #share="{ row }">{{ fmtPercent(row?.Share) }}</template>
          <template #bar="{ row }">
            <div class="bar-track">
              <div class="bar-fill danger" :style="{ width: `${Math.max(4, (row.TotalDamage / maxDamage) * 100)}%` }" />
            </div>
          </template>
        </t-table>
      </t-card>
    </t-loading>
  </div>
</template>

<style lang="less" scoped>
.facts {
  display: grid;
  gap: 12px;
  padding: 4px 0 16px;
}

.facts-4 {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.facts-3 {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.facts-2 {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.fact-value {
  font-size: 22px;
  font-weight: 600;
  line-height: 1.2;
  color: var(--td-text-color-primary);
  font-variant-numeric: tabular-nums;
}

.fact-label {
  margin-top: 4px;
  font-size: 12px;
  color: var(--td-text-color-secondary);
}

.sub-table {
  margin-bottom: 8px;
}

.abyss-row {
  margin-bottom: 8px;
}

.abyss-col {
  display: flex;
  flex-direction: column;
}

.inner-card {
  flex: 1;
  background: var(--td-bg-color-secondarycontainer);
}

.bar-list {
  padding-top: 8px;
}

.bar-title {
  margin: 16px 0 8px;
  font-size: 13px;
  font-weight: 600;
  color: var(--td-text-color-primary);
}

.bar-row {
  display: grid;
  grid-template-columns: 96px 1fr 64px;
  align-items: center;
  gap: 12px;
  padding: 4px 0;
  font-size: 12px;
}

.bar-name {
  color: var(--td-text-color-secondary);
}

.bar-track {
  height: 6px;
  border-radius: 3px;
  background: var(--td-bg-color-component);
  overflow: hidden;
}

.bar-fill {
  height: 100%;
  border-radius: 3px;
  background: var(--td-brand-color);
}

.bar-fill.danger {
  background: var(--td-error-color);
}

.bar-value {
  text-align: right;
  color: var(--td-text-color-primary);
  font-variant-numeric: tabular-nums;
}

.chip-wrap {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.chip-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 4px 0;
  font-size: 12px;
  border-bottom: 1px dashed var(--td-component-stroke);
}

.chip-row:last-child {
  border-bottom: none;
}

.chip-count {
  color: var(--td-text-color-primary);
  font-variant-numeric: tabular-nums;
}

.tip {
  margin-bottom: 12px;
  color: var(--td-text-color-secondary);
  font-size: 13px;
}

.muted {
  color: var(--td-text-color-placeholder);
  font-size: 12px;
}
</style>
