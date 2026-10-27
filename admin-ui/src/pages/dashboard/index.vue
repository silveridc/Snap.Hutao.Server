<script setup lang="ts">
import { MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { getSystemOverview, getUserStatistics } from '@/api/service';
import { t } from '@/locales';
import { useUserStore } from '@/store';

const userStore = useUserStore();
const router = useRouter();
const refreshing = ref(false);

const loading = ref(false);
const overview = ref<any>(null);
const userStats = ref<any>(null);

const growthItems = computed(() => {
  const stats = userStats.value;
  if (!stats) return [];
  return [
    { label: t('pages.dashboard.growth.total'), value: stats.TotalUsers ?? '—' },
    { label: t('pages.dashboard.growth.today'), value: stats.NewUsersToday ?? 0 },
    { label: t('pages.dashboard.growth.last7'), value: stats.NewUsersLast7Days ?? 0 },
    { label: t('pages.dashboard.growth.last30'), value: stats.NewUsersLast30Days ?? 0 },
  ];
});

const runtime = computed(() => overview.value?.Runtime ?? null);
const resource = computed(() => overview.value?.Resource ?? null);
const redis = computed(() => overview.value?.Redis ?? null);
const scheduler = computed(() => overview.value?.Scheduler ?? null);
const databases = computed<any[]>(() => overview.value?.Databases ?? []);
const jobs = computed<any[]>(() => scheduler.value?.Jobs ?? []);

const uptime = computed(() => (runtime.value ? fmtDuration(runtime.value.UptimeSeconds) : '—'));

const runtimeItems = computed(() => {
  const r = runtime.value;
  if (!r) return [];
  return [
    { label: t('pages.dashboard.system.appVersion'), value: r.ApplicationVersion, mono: true },
    { label: t('pages.dashboard.system.machine'), value: r.MachineName, mono: true },
    { label: t('pages.dashboard.system.os'), value: r.OperatingSystem },
    { label: t('pages.dashboard.system.framework'), value: r.Framework, mono: true },
    { label: t('pages.dashboard.system.processId'), value: r.ProcessId, mono: true },
    { label: t('pages.dashboard.system.startedAt'), value: fmt(r.StartedAt) },
    { label: t('pages.dashboard.system.localTime'), value: fmt(r.LocalTime) },
    { label: t('pages.dashboard.system.timeZone'), value: r.TimeZoneId, mono: true },
  ];
});

const cpuPercent = computed(() => {
  const value = Number(resource.value?.ProcessCpuPercent ?? 0);
  return Math.min(100, Math.max(0, Number.isFinite(value) ? value : 0));
});

const cpuColor = computed(() => {
  if (cpuPercent.value >= 85) return '#e34d59';
  if (cpuPercent.value >= 60) return '#ed7b2f';
  return '#0052d9';
});

const workingSet = computed(() => bytesParts(resource.value?.WorkingSetBytes));
const privateMemory = computed(() => bytesParts(resource.value?.PrivateMemoryBytes));
const managedHeap = computed(() => bytesParts(resource.value?.ManagedHeapBytes));

// 托管堆占私有内存的比例：比例偏高通常意味着 GC 压力大，比单看绝对值有信息量
const heapShare = computed(() => {
  const heap = Number(resource.value?.ManagedHeapBytes ?? 0);
  const total = Number(resource.value?.PrivateMemoryBytes ?? 0);
  if (!total) return 0;
  return Math.min(100, Math.round((heap / total) * 100));
});

const resourceFacts = computed(() => {
  const r = resource.value;
  if (!r) return [];
  return [
    { label: t('pages.dashboard.resource.cores'), value: r.ProcessorCount },
    { label: t('pages.dashboard.resource.threads'), value: r.ThreadCount },
    { label: t('pages.dashboard.resource.cpuSeconds'), value: `${Number(r.ProcessCpuSeconds).toFixed(2)}s` },
  ];
});

// INFO 被服务端禁用时，除接入地址与 Ping 外的指标都拿不到，直接留白比铺一排「—」干净
const redisItems = computed(() => {
  const r = redis.value;
  if (!r) return [];
  const rows: Array<{ label: string; value: any }> = [
    { label: t('pages.dashboard.redis.endpoint'), value: r.Endpoint },
    { label: t('pages.dashboard.redis.ping'), value: r.PingMs === null || r.PingMs === undefined ? null : `${r.PingMs} ms` },
    { label: t('pages.dashboard.redis.version'), value: r.Version },
    { label: t('pages.dashboard.redis.usedMemory'), value: r.UsedMemory },
    { label: t('pages.dashboard.redis.policy'), value: r.MaxMemoryPolicy },
    { label: t('pages.dashboard.redis.clients'), value: r.ConnectedClients },
    { label: t('pages.dashboard.redis.keys'), value: r.KeyCount },
    { label: t('pages.dashboard.redis.uptimeDays'), value: r.UptimeDays },
    { label: t('pages.dashboard.redis.instanceName'), value: r.InstanceName },
  ];

  return rows.filter((row) => row.value !== null && row.value !== undefined && row.value !== '');
});

const databaseColumns = computed(() => [
  { colKey: 'Name', title: t('pages.dashboard.database.colName'), width: 120 },
  { colKey: 'status', title: t('pages.dashboard.database.colStatus'), width: 90 },
  { colKey: 'ServerVersion', title: t('pages.dashboard.database.colVersion'), width: 140 },
  { colKey: 'address', title: t('pages.dashboard.database.colAddress'), minWidth: 220 },
  { colKey: 'Database', title: t('pages.dashboard.database.colDatabase'), minWidth: 180 },
  { colKey: 'MaxPoolSize', title: t('pages.dashboard.database.colPool'), width: 120, align: 'right' },
  { colKey: 'RoundtripMs', title: t('pages.dashboard.database.colRoundtrip'), width: 120, align: 'right' },
]);

const jobColumns = computed(() => [
  { colKey: 'Name', title: t('pages.dashboard.scheduler.colName'), width: 220, ellipsis: true },
  { colKey: 'JobType', title: t('pages.dashboard.scheduler.colType'), minWidth: 240, ellipsis: true },
  { colKey: 'Cron', title: t('pages.dashboard.scheduler.colCron'), width: 150 },
  { colKey: 'State', title: t('pages.dashboard.scheduler.colState'), width: 120 },
  { colKey: 'NextFireAt', title: t('pages.dashboard.scheduler.colNext'), width: 180 },
  { colKey: 'PreviousFireAt', title: t('pages.dashboard.scheduler.colPrevious'), width: 180 },
]);

// 任务实现类型是全限定名，列表里只留类名，完整名放到 tooltip
function shortType(value?: string) {
  if (!value) return '—';
  const index = value.lastIndexOf('.');
  return index >= 0 ? value.slice(index + 1) : value;
}

const schedulerFacts = computed(() => {
  const s = scheduler.value;
  if (!s) return [];
  return [
    { label: t('pages.dashboard.scheduler.state'), value: s.State || '—' },
    { label: t('pages.dashboard.scheduler.name'), value: s.Name || '—' },
    { label: t('pages.dashboard.scheduler.jobCount'), value: s.JobCount ?? '—' },
    { label: t('pages.dashboard.scheduler.triggerCount'), value: s.TriggerCount ?? '—' },
    { label: t('pages.dashboard.scheduler.instanceId'), value: s.InstanceId || '—', mono: true },
  ];
});

async function load() {
  loading.value = true;
  try {
    const [overviewRes, statsRes]: any[] = await Promise.all([getSystemOverview(), getUserStatistics()]);
    overview.value = overviewRes.data ?? null;
    userStats.value = statsRes.data ?? null;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.dashboard.messages.loadFail'));
  } finally {
    loading.value = false;
  }
}

onMounted(load);

// 服务端返回的时间有两类：ISO 字符串（运行时/调度器）与 unix 秒（账号到期时间）
function fmt(value?: string | number) {
  if (value === null || value === undefined || value === '') return '—';
  if (typeof value === 'number' && value <= 0) return '—';

  const d = typeof value === 'number' ? new Date(value * 1000) : new Date(value);
  return Number.isNaN(d.getTime()) ? String(value) : d.toLocaleString('zh-CN');
}

function bytesParts(value?: number) {
  if (value === null || value === undefined) return { value: '—', unit: '' };
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let size = value;
  let index = 0;
  while (size >= 1024 && index < units.length - 1) {
    size /= 1024;
    index += 1;
  }

  return { value: index === 0 ? String(size) : size.toFixed(1), unit: units[index] };
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
  <div class="hutao-page dashboard">
    <t-row :gutter="16" class="top-row">
      <t-col :span="4" class="top-col">
        <t-card :bordered="false" class="hutao-card panel">
          <template #title>
            <div class="panel-title">
              <span>{{ $t('pages.dashboard.system.title') }}</span>
              <t-tag v-if="runtime" size="small" variant="light" theme="default" class="mono">{{ runtime.ApplicationVersion }}</t-tag>
            </div>
          </template>
          <template #actions>
            <t-button variant="text" shape="square" size="small" :loading="loading" @click="load">
              <template #icon>
                <t-icon name="refresh" />
              </template>
            </t-button>
          </template>
          <t-loading :loading="loading" show-overlay>
            <div class="hero">
              <div class="hero-value">{{ uptime }}</div>
              <div class="hero-label">{{ $t('pages.dashboard.system.uptime') }}</div>
            </div>
            <div class="info-list">
              <div v-for="it in runtimeItems" :key="it.label" class="info-row">
                <span class="info-label">{{ it.label }}</span>
                <span class="info-value" :class="{ mono: it.mono }">{{ it.value }}</span>
              </div>
            </div>
          </t-loading>
        </t-card>
      </t-col>

      <t-col :span="4" class="top-col">
        <t-card :bordered="false" class="hutao-card panel">
          <template #title>{{ $t('pages.dashboard.resource.title') }}</template>
          <t-loading :loading="loading" show-overlay>
            <div class="metric">
              <div class="metric-head">
                <span class="metric-label">{{ $t('pages.dashboard.resource.cpu') }}</span>
                <span class="metric-value">
                  {{ cpuPercent.toFixed(1) }}<span class="metric-unit">%</span>
                </span>
              </div>
              <t-progress theme="line" :percentage="cpuPercent" :label="false" :color="cpuColor" :stroke-width="6" />
            </div>

            <div class="metric">
              <div class="metric-head">
                <span class="metric-label">{{ $t('pages.dashboard.resource.workingSet') }}</span>
                <span class="metric-value">
                  {{ workingSet.value }}<span class="metric-unit">{{ workingSet.unit }}</span>
                </span>
              </div>
              <div class="metric-sub">
                {{ $t('pages.dashboard.resource.privateMemory') }} {{ privateMemory.value }} {{ privateMemory.unit }}
              </div>
            </div>

            <div class="metric">
              <div class="metric-head">
                <span class="metric-label">{{ $t('pages.dashboard.resource.managedHeap') }}</span>
                <span class="metric-value">
                  {{ managedHeap.value }}<span class="metric-unit">{{ managedHeap.unit }}</span>
                </span>
              </div>
              <div class="metric-sub">
                <t-progress theme="line" :percentage="heapShare" :label="false" :color="'#00a870'" :stroke-width="4" />
                <span class="metric-sub-text">{{ $t('pages.dashboard.resource.heapShare') }} {{ heapShare }}%</span>
              </div>
            </div>

            <div class="facts facts-3">
              <div v-for="f in resourceFacts" :key="f.label" class="fact">
                <div class="fact-value">{{ f.value }}</div>
                <div class="fact-label">{{ f.label }}</div>
              </div>
            </div>
          </t-loading>
        </t-card>
      </t-col>

      <t-col :span="4" class="top-col">
        <t-card :bordered="false" class="hutao-card panel">
          <template #title>
            <div class="panel-title">
              <span>{{ $t('pages.dashboard.redis.title') }}</span>
              <t-tag v-if="redis" size="small" variant="light" :theme="redis.Connected ? 'success' : 'danger'">
                {{ redis.Connected ? $t('pages.dashboard.redis.online') : $t('pages.dashboard.redis.offline') }}
              </t-tag>
            </div>
          </template>
          <t-loading :loading="loading" show-overlay>
            <div class="info-list">
              <div v-for="it in redisItems" :key="it.label" class="info-row">
                <span class="info-label">{{ it.label }}</span>
                <span class="info-value mono">{{ it.value }}</span>
              </div>
            </div>
            <div v-if="redis && redis.Error" class="hint">
              <t-icon name="info-circle" />
              <span>{{ $t('pages.dashboard.redis.infoUnavailable') }}</span>
              <t-tooltip :content="redis.Error" placement="top">
                <t-icon name="help-circle" class="hint-help" />
              </t-tooltip>
            </div>
          </t-loading>
        </t-card>
      </t-col>
    </t-row>

    <t-card :title="$t('pages.dashboard.database.title')" :bordered="false" class="hutao-card">
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
        <template #RoundtripMs="{ row }">
          <span class="mono">{{ row?.RoundtripMs === null || row?.RoundtripMs === undefined ? '—' : `${row.RoundtripMs} ms` }}</span>
        </template>
      </t-table>
      <t-alert v-for="db in databases.filter((item) => item.Error)" :key="db.Name" theme="warning" :message="`${db.Name}: ${db.Error}`" class="probe-error" />
    </t-card>

    <t-card :title="$t('pages.dashboard.scheduler.title')" :bordered="false" class="hutao-card">
      <div class="facts facts-5">
        <div v-for="f in schedulerFacts" :key="f.label" class="fact">
          <div class="fact-value" :class="{ mono: f.mono }">{{ f.value }}</div>
          <div class="fact-label">{{ f.label }}</div>
        </div>
      </div>
      <t-table row-key="Name" :data="jobs" :columns="jobColumns" :loading="loading" size="small" cell-empty-content="—" table-layout="fixed">
        <template #empty>{{ $t('pages.dashboard.overview.noData') }}</template>
        <template #Cron="{ row }">
          <span class="mono">{{ row?.Cron || '—' }}</span>
        </template>
        <template #JobType="{ row }">
          <t-tooltip :content="row?.JobType || '—'" placement="top-left">
            <span class="mono">{{ shortType(row?.JobType) }}</span>
          </t-tooltip>
        </template>
        <template #NextFireAt="{ row }">{{ fmt(row?.NextFireAt) }}</template>
        <template #PreviousFireAt="{ row }">{{ fmt(row?.PreviousFireAt) }}</template>
      </t-table>
      <t-alert v-if="scheduler && scheduler.Error" theme="warning" :message="scheduler.Error" class="probe-error" />
    </t-card>

    <t-card :title="$t('pages.dashboard.growth.title')" :bordered="false" class="hutao-card">
      <div class="facts">
        <div v-for="it in growthItems" :key="it.label" class="fact">
          <div class="fact-value">{{ it.value }}</div>
          <div class="fact-label">{{ it.label }}</div>
        </div>
      </div>
    </t-card>
    <t-row :gutter="16">
      <t-col :span="6">
        <t-card :title="$t('pages.dashboard.account.title')" :bordered="false" class="hutao-card">
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

// 顶部三张卡等高，避免左侧内容多、右侧短一截的参差感
.top-row {
  display: flex;
  align-items: stretch;
}

.top-col {
  display: flex;
  flex-direction: column;
}

.top-col :deep(.panel) {
  flex: 1;
}

.panel :deep(.t-card__header) {
  padding-bottom: 8px;
}

.panel :deep(.t-card__body) {
  padding-top: 4px;
}

.panel-title {
  display: flex;
  align-items: center;
  gap: 8px;
}

// 运行时长做主视觉，避免「一行只有一个值」的空旷感
.hero {
  padding: 6px 0 18px;
  border-bottom: 1px solid var(--td-component-stroke);
  margin-bottom: 4px;
}

.hero-value {
  font-size: 30px;
  font-weight: 600;
  line-height: 1.2;
  color: var(--td-text-color-primary);
  letter-spacing: -0.5px;
}

.hero-label {
  margin-top: 4px;
  font-size: 13px;
  color: var(--td-text-color-secondary);
}
// 资源卡：CPU / 工作集 / 托管堆各占一档，纵向铺满卡片高度
.metric {
  padding: 12px 0;
  border-bottom: 1px dashed var(--td-component-stroke);
}

.metric:first-child {
  padding-top: 4px;
}

.metric-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 8px;
}

.metric-label {
  font-size: 13px;
  color: var(--td-text-color-secondary);
}

.metric-value {
  font-size: 22px;
  font-weight: 600;
  line-height: 1.2;
  color: var(--td-text-color-primary);
  font-variant-numeric: tabular-nums;
}

.metric-unit {
  font-size: 12px;
  font-weight: 500;
  color: var(--td-text-color-secondary);
  margin-left: 2px;
}

.metric-sub {
  font-size: 12px;
  color: var(--td-text-color-secondary);
}

.metric-sub-text {
  display: block;
  margin-top: 4px;
}

.facts {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 8px;
  padding-top: 14px;
}

.facts-3 {
  grid-template-columns: repeat(3, 1fr);
}

// 调度器多一个实例 ID，单独给五列并允许长 ID 断行
.facts-5 {
  grid-template-columns: 1.2fr 1.2fr 0.7fr 0.7fr 1.6fr;
  padding: 4px 0 16px;
  border-bottom: 1px solid var(--td-component-stroke);
  margin-bottom: 4px;
}

.facts-5 .fact-value {
  word-break: break-all;
  font-size: 14px;
}

.fact {
  min-width: 0;
}

.fact-value {
  font-size: 15px;
  font-weight: 600;
  color: var(--td-text-color-primary);
  font-variant-numeric: tabular-nums;
}

.fact-label {
  margin-top: 2px;
  font-size: 12px;
  color: var(--td-text-color-secondary);
}

// Redis 的 INFO 报错收成一行提示，原始英文只放在 tooltip 里
.hint {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 12px;
  padding: 8px 10px;
  border-radius: var(--td-radius-medium);
  background: var(--td-warning-color-light);
  color: var(--td-warning-color);
  font-size: 12px;
}

.hint-help {
  cursor: help;
}
.summary {
  margin-bottom: 12px;
}

.probe-error {
  margin-top: 12px;
}
</style>
