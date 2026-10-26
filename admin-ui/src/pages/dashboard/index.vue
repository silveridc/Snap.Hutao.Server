<script setup lang="ts">
import { MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { getSystemOverview } from '@/api/service';
import { t } from '@/locales';
import { useUserStore } from '@/store';

const userStore = useUserStore();
const router = useRouter();
const refreshing = ref(false);

const loading = ref(false);
const overview = ref<any>(null);

const runtime = computed(() => overview.value?.Runtime ?? null);
const resource = computed(() => overview.value?.Resource ?? null);
const redis = computed(() => overview.value?.Redis ?? null);
const scheduler = computed(() => overview.value?.Scheduler ?? null);
const databases = computed<any[]>(() => overview.value?.Databases ?? []);
const jobs = computed<any[]>(() => scheduler.value?.Jobs ?? []);

const runtimeItems = computed(() => {
  const r = runtime.value;
  if (!r) return [];
  return [
    { label: t('pages.dashboard.system.appVersion'), value: r.ApplicationVersion },
    { label: t('pages.dashboard.system.machine'), value: r.MachineName },
    { label: t('pages.dashboard.system.os'), value: r.OperatingSystem },
    { label: t('pages.dashboard.system.framework'), value: r.Framework },
    { label: t('pages.dashboard.system.processId'), value: r.ProcessId },
    { label: t('pages.dashboard.system.startedAt'), value: fmt(r.StartedAt) },
    { label: t('pages.dashboard.system.uptime'), value: fmtDuration(r.UptimeSeconds) },
    { label: t('pages.dashboard.system.localTime'), value: fmt(r.LocalTime) },
    { label: t('pages.dashboard.system.timeZone'), value: r.TimeZoneId },
  ];
});

const resourceItems = computed(() => {
  const r = resource.value;
  if (!r) return [];
  return [
    { label: t('pages.dashboard.resource.cpu'), value: `${r.ProcessCpuPercent}%` },
    { label: t('pages.dashboard.resource.cores'), value: r.ProcessorCount },
    { label: t('pages.dashboard.resource.cpuSeconds'), value: `${r.ProcessCpuSeconds}s` },
    { label: t('pages.dashboard.resource.workingSet'), value: fmtBytes(r.WorkingSetBytes) },
    { label: t('pages.dashboard.resource.privateMemory'), value: fmtBytes(r.PrivateMemoryBytes) },
    { label: t('pages.dashboard.resource.managedHeap'), value: fmtBytes(r.ManagedHeapBytes) },
    { label: t('pages.dashboard.resource.threads'), value: r.ThreadCount },
  ];
});

const redisItems = computed(() => {
  const r = redis.value;
  if (!r) return [];
  return [
    { label: t('pages.dashboard.redis.status'), value: r.Connected ? t('pages.dashboard.redis.online') : t('pages.dashboard.redis.offline'), tag: r.Connected ? 'success' : 'danger' },
    { label: t('pages.dashboard.redis.endpoint'), value: r.Endpoint || '—' },
    { label: t('pages.dashboard.redis.version'), value: r.Version || '—' },
    { label: t('pages.dashboard.redis.usedMemory'), value: r.UsedMemory || '—' },
    { label: t('pages.dashboard.redis.policy'), value: r.MaxMemoryPolicy || '—' },
    { label: t('pages.dashboard.redis.clients'), value: r.ConnectedClients ?? '—' },
    { label: t('pages.dashboard.redis.keys'), value: r.KeyCount ?? '—' },
    { label: t('pages.dashboard.redis.uptimeDays'), value: r.UptimeDays ?? '—' },
    { label: t('pages.dashboard.redis.ping'), value: r.PingMs === null || r.PingMs === undefined ? '—' : `${r.PingMs} ms` },
    { label: t('pages.dashboard.redis.instanceName'), value: r.InstanceName },
  ];
});

const databaseColumns = computed(() => [
  { colKey: 'Name', title: t('pages.dashboard.database.colName'), width: 110 },
  { colKey: 'status', title: t('pages.dashboard.database.colStatus'), width: 90 },
  { colKey: 'ServerVersion', title: t('pages.dashboard.database.colVersion'), width: 150 },
  { colKey: 'address', title: t('pages.dashboard.database.colAddress'), width: 200 },
  { colKey: 'Database', title: t('pages.dashboard.database.colDatabase'), minWidth: 200 },
  { colKey: 'MaxPoolSize', title: t('pages.dashboard.database.colPool'), width: 90 },
  { colKey: 'RoundtripMs', title: t('pages.dashboard.database.colRoundtrip'), width: 110 },
]);

const jobColumns = computed(() => [
  { colKey: 'Group', title: t('pages.dashboard.scheduler.colGroup'), width: 100 },
  { colKey: 'Name', title: t('pages.dashboard.scheduler.colName'), width: 150 },
  { colKey: 'JobType', title: t('pages.dashboard.scheduler.colType'), minWidth: 260 },
  { colKey: 'Cron', title: t('pages.dashboard.scheduler.colCron'), width: 150 },
  { colKey: 'State', title: t('pages.dashboard.scheduler.colState'), width: 110 },
  { colKey: 'NextFireAt', title: t('pages.dashboard.scheduler.colNext'), width: 190 },
  { colKey: 'PreviousFireAt', title: t('pages.dashboard.scheduler.colPrevious'), width: 190 },
]);

async function load() {
  loading.value = true;
  try {
    const res: any = await getSystemOverview();
    overview.value = res.data ?? null;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.dashboard.messages.loadFail'));
  } finally {
    loading.value = false;
  }
}

onMounted(load);

function fmt(value?: string) {
  if (!value) return '—';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? String(value) : d.toLocaleString('zh-CN');
}

function fmtBytes(value?: number) {
  if (value === null || value === undefined) return '—';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let size = value;
  let index = 0;
  while (size >= 1024 && index < units.length - 1) {
    size /= 1024;
    index += 1;
  }

  return `${index === 0 ? size : size.toFixed(1)} ${units[index]}`;
}

function fmtDuration(seconds?: number) {
  if (seconds === null || seconds === undefined) return '—';
  const units: Array<[number, string]> = [
    [Math.floor(seconds / 86400), t('pages.dashboard.duration.days')],
    [Math.floor((seconds % 86400) / 3600), t('pages.dashboard.duration.hours')],
    [Math.floor((seconds % 3600) / 60), t('pages.dashboard.duration.minutes')],
    [seconds % 60, t('pages.dashboard.duration.seconds')],
  ];

  const kept = units.filter(([value]) => value > 0).slice(0, 2);
  if (!kept.length) return `0 ${t('pages.dashboard.duration.seconds')}`;
  return kept.map(([value, label]) => `${value} ${label}`).join(' ');
}

const shortcuts = computed(() => [
  { title: t('pages.dashboard.shortcuts.abyssStats'), path: '/statistics' },
  { title: t('pages.dashboard.shortcuts.compensation'), path: '/compensation' },
  { title: t('pages.dashboard.shortcuts.announcement'), path: '/announcement' },
  { title: t('pages.dashboard.shortcuts.redeem'), path: '/redeem' },
]);

const items = computed(() => {
  const u = userStore.userInfo;
  if (!u?.name) return [];
  return [
    { label: t('pages.dashboard.account.email'), value: u.name },
    { label: t('pages.dashboard.account.authKind'), value: u.authKind === 'admin' ? t('pages.dashboard.account.kindAdmin') : t('pages.dashboard.account.kindUser') },
    { label: t('pages.dashboard.account.maintainer'), value: u.isMaintainer ? t('pages.dashboard.account.yes') : t('pages.dashboard.account.no') },
    { label: t('pages.dashboard.account.canWrite'), value: u.canWrite ? t('pages.dashboard.account.yes') : t('pages.dashboard.account.no') },
    { label: t('pages.dashboard.account.licensedDeveloper'), value: u.isLicensedDeveloper ? t('pages.dashboard.account.yes') : t('pages.dashboard.account.no') },
    { label: t('pages.dashboard.account.gachaLogExpireAt'), value: fmt(u.gachaLogExpireAt) },
    { label: t('pages.dashboard.account.cdnExpireAt'), value: fmt(u.cdnExpireAt) },
  ];
});

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
    <t-row :gutter="16">
      <t-col :span="4">
        <t-card :title="$t('pages.dashboard.system.title')" class="hutao-card" :bordered="false">
          <template #actions>
            <t-button variant="outline" size="small" :loading="loading" @click="load">{{ $t('pages.dashboard.overview.refresh') }}</t-button>
          </template>
          <t-loading :loading="loading" show-overlay>
            <div v-for="it in runtimeItems" :key="it.label" class="info-row">
              <span class="info-label">{{ it.label }}</span>
              <span class="info-value mono">{{ it.value }}</span>
            </div>
          </t-loading>
        </t-card>
      </t-col>
      <t-col :span="4">
        <t-card :title="$t('pages.dashboard.resource.title')" class="hutao-card" :bordered="false">
          <div class="stat-grid">
            <div v-for="it in resourceItems" :key="it.label" class="stat-tile">
              <div class="stat-value">{{ it.value }}</div>
              <div class="stat-label">{{ it.label }}</div>
            </div>
          </div>
        </t-card>
      </t-col>
      <t-col :span="4">
        <t-card :title="$t('pages.dashboard.redis.title')" class="hutao-card" :bordered="false">
          <div v-for="it in redisItems" :key="it.label" class="info-row">
            <span class="info-label">{{ it.label }}</span>
            <t-tag v-if="it.tag" :theme="it.tag" variant="light-outline">{{ it.value }}</t-tag>
            <span v-else class="info-value mono">{{ it.value }}</span>
          </div>
          <t-alert v-if="redis && redis.Error" theme="warning" :message="redis.Error" class="probe-error" />
        </t-card>
      </t-col>
    </t-row>

    <t-card :title="$t('pages.dashboard.database.title')" class="hutao-card" :bordered="false">
      <t-table row-key="Name" :data="databases" :columns="databaseColumns" :loading="loading" size="small" cell-empty-content="—">
        <template #empty>{{ $t('pages.dashboard.overview.noData') }}</template>
        <template #status="{ row }">
          <t-tag :theme="row?.Connected ? 'success' : 'danger'" variant="light-outline">
            {{ row?.Connected ? $t('pages.dashboard.redis.online') : $t('pages.dashboard.redis.offline') }}
          </t-tag>
        </template>
        <template #address="{ row }">
          <span class="mono">{{ row?.Host ? `${row.Host}:${row.Port ?? '—'}` : '—' }}</span>
        </template>
      </t-table>
      <t-alert v-for="db in databases.filter((item) => item.Error)" :key="db.Name" theme="warning" :message="`${db.Name}: ${db.Error}`" class="probe-error" />
    </t-card>

    <t-card :title="$t('pages.dashboard.scheduler.title')" class="hutao-card" :bordered="false">
      <t-descriptions :column="5" layout="horizontal" size="small" class="summary">
        <t-descriptions-item :label="$t('pages.dashboard.scheduler.state')">{{ scheduler?.State || '—' }}</t-descriptions-item>
        <t-descriptions-item :label="$t('pages.dashboard.scheduler.name')">{{ scheduler?.Name || '—' }}</t-descriptions-item>
        <t-descriptions-item :label="$t('pages.dashboard.scheduler.instanceId')">{{ scheduler?.InstanceId || '—' }}</t-descriptions-item>
        <t-descriptions-item :label="$t('pages.dashboard.scheduler.jobCount')">{{ scheduler?.JobCount ?? '—' }}</t-descriptions-item>
        <t-descriptions-item :label="$t('pages.dashboard.scheduler.triggerCount')">{{ scheduler?.TriggerCount ?? '—' }}</t-descriptions-item>
      </t-descriptions>
      <t-table row-key="Name" :data="jobs" :columns="jobColumns" :loading="loading" size="small" cell-empty-content="—">
        <template #empty>{{ $t('pages.dashboard.overview.noData') }}</template>
        <template #Cron="{ row }">
          <span class="mono">{{ row?.Cron || '—' }}</span>
        </template>
        <template #JobType="{ row }">
          <span class="mono">{{ row?.JobType }}</span>
        </template>
        <template #NextFireAt="{ row }">{{ fmt(row?.NextFireAt) }}</template>
        <template #PreviousFireAt="{ row }">{{ fmt(row?.PreviousFireAt) }}</template>
      </t-table>
      <t-alert v-if="scheduler && scheduler.Error" theme="warning" :message="scheduler.Error" class="probe-error" />
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
  width: 130px;
  color: var(--td-text-color-secondary);
}
.info-value {
  font-weight: 500;
}
.stat-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
  gap: 12px;
}
.stat-tile {
  padding: 14px;
  border: 1px solid var(--td-component-stroke);
  border-radius: var(--td-radius-medium);
  background: var(--td-bg-color-container);
}
.stat-value {
  font-size: 20px;
  font-weight: 600;
  color: var(--td-text-color-primary);
}
.stat-label {
  margin-top: 4px;
  color: var(--td-text-color-secondary);
  font-size: 13px;
}
.mono {
  font-family: 'Cascadia Code', 'Fira Code', Consolas, monospace;
}
.summary {
  margin-bottom: 12px;
}
.probe-error {
  margin-top: 12px;
}
</style>
