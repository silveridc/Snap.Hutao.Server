<script setup lang="ts">
import { DialogPlugin, MessagePlugin } from 'tdesign-vue-next';
import { computed, ref } from 'vue';

import { invalidateCache, listCacheKeys } from '@/api/service';
import { t } from '@/locales';

interface KeyRow {
  key: string;
}

const presets = computed(() => [
  { label: t('pages.cache.presets.abyssStats'), pattern: '*Statistics*' },
  { label: t('pages.cache.presets.gachaDistribution'), pattern: '*GachaDistribution*' },
  { label: t('pages.cache.presets.gachaEventStats'), pattern: 'GachaEventStatistics' },
  { label: t('pages.cache.presets.theaterStats'), pattern: 'RoleCombatStatistics:*' },
  { label: t('pages.cache.presets.enka'), pattern: 'Enka:*' },
  { label: t('pages.cache.presets.wallpaper'), pattern: '*Wallpaper*' },
  { label: t('pages.cache.presets.announcements'), pattern: 'Announcements:*' },
  { label: t('pages.cache.presets.maintainer'), pattern: 'User:IsMaintainer:*' },
  { label: t('pages.cache.presets.userStats'), pattern: 'Admin:UserStatistics' },
]);

const patterns = ref<string[]>([]);
const keys = ref<string[]>([]);
const invalidating = ref(false);
const lastResult = ref('');

const browsePattern = ref('*');
const loadingKeys = ref(false);
const rows = ref<KeyRow[]>([]);
const selectedKeys = ref<string[]>([]);

const columns = computed(() => [
  { colKey: 'row-select', type: 'multiple', width: 50 },
  { colKey: 'key', title: t('pages.cache.browse.colKey') },
]);
async function runInvalidate(payload: { Keys?: string[]; Patterns?: string[] }) {
  invalidating.value = true;
  lastResult.value = '';
  try {
    const res: any = await invalidateCache({ Keys: payload.Keys ?? [], Patterns: payload.Patterns ?? [] });
    const d = res.data ?? {};
    lastResult.value = t('pages.cache.messages.result', { keys: d.RemovedKeys ?? 0, patterns: d.RemovedByPattern ?? 0 });
    MessagePlugin.success(t('pages.cache.messages.invalidateSuccess'));
    if (rows.value.length) {
      await queryKeys();
    }
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.cache.messages.opFail'));
  } finally {
    invalidating.value = false;
  }
}

function invalidatePreset(pattern: string) {
  runInvalidate({ Patterns: [pattern] });
}

function invalidateAll() {
  const dialog = DialogPlugin.confirm({
    header: t('pages.cache.dialog.clearAllHeader'),
    body: t('pages.cache.dialog.clearAllBody'),
    theme: 'warning',
    confirmBtn: t('pages.cache.dialog.clearAllConfirm'),
    onConfirm: () => {
      dialog.hide();
      runInvalidate({ Patterns: ['*'] });
    },
  });
}

function submitCustom() {
  if (!keys.value.length && !patterns.value.length) {
    MessagePlugin.warning(t('pages.cache.messages.emptyInput'));
    return;
  }
  runInvalidate({ Keys: keys.value, Patterns: patterns.value });
}

async function queryKeys() {
  loadingKeys.value = true;
  try {
    const res: any = await listCacheKeys(browsePattern.value || '*', 500);
    rows.value = (res.data ?? []).map((k: string) => ({ key: k }));
    selectedKeys.value = [];
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.cache.messages.queryFail'));
  } finally {
    loadingKeys.value = false;
  }
}

function expireSelected() {
  if (!selectedKeys.value.length) {
    MessagePlugin.warning(t('pages.cache.messages.selectFirst'));
    return;
  }
  runInvalidate({ Keys: [...selectedKeys.value] });
}
</script>

<template>
  <div class="hutao-page">
    <t-row :gutter="16">
      <t-col :span="7">
        <t-card :title="$t('pages.cache.quick.title')" :bordered="false" class="hutao-card">
          <p class="tip">{{ $t('pages.cache.quick.tipPre') }}<code>POST /Service/Cache/Invalidate</code>{{ $t('pages.cache.quick.tipSuf') }}</p>
          <t-space break-line size="8px">
            <t-button
              v-for="p in presets"
              :key="p.pattern"
              theme="default"
              variant="outline"
              :loading="invalidating"
              @click="invalidatePreset(p.pattern)"
            >
              {{ p.label }}
            </t-button>
            <t-button theme="danger" variant="outline" :loading="invalidating" @click="invalidateAll">{{ $t('pages.cache.quick.invalidateAll') }}</t-button>
          </t-space>
          <t-alert v-if="lastResult" class="result" theme="success" :message="lastResult" />
        </t-card>
      </t-col>
      <t-col :span="5">
        <t-card :title="$t('pages.cache.custom.title')" :bordered="false" class="hutao-card">
          <t-form label-align="top">
            <t-form-item :label="$t('pages.cache.custom.keysLabel')">
              <t-tag-input v-model="keys" :placeholder="$t('pages.cache.custom.keysPlaceholder')" clearable />
            </t-form-item>
            <t-form-item :label="$t('pages.cache.custom.patternsLabel')">
              <t-tag-input v-model="patterns" :placeholder="$t('pages.cache.custom.patternsPlaceholder')" clearable />
            </t-form-item>
            <t-form-item>
              <t-button theme="primary" :loading="invalidating" @click="submitCustom">{{ $t('pages.cache.custom.submit') }}</t-button>
            </t-form-item>
          </t-form>
        </t-card>
      </t-col>
    </t-row>

    <t-card :title="$t('pages.cache.browse.title')" :bordered="false" class="hutao-card">
      <t-space class="browse-bar">
        <t-input v-model="browsePattern" :placeholder="$t('pages.cache.browse.patternPlaceholder')" style="width: 320px" @enter="queryKeys" />
        <t-button theme="primary" :loading="loadingKeys" @click="queryKeys">{{ $t('pages.cache.browse.query') }}</t-button>
        <t-button
          theme="danger"
          variant="outline"
          :disabled="!selectedKeys.length"
          :loading="invalidating"
          @click="expireSelected"
        >
          {{ $t('pages.cache.browse.expireSelected', { count: selectedKeys.length }) }}
        </t-button>
      </t-space>
      <t-table
        row-key="key"
        :data="rows"
        :columns="columns"
        :selected-row-keys="selectedKeys"
        :loading="loadingKeys"
        max-height="480"
        size="small"
        @select-change="(value: string[]) => (selectedKeys = value)"
      />
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
.result {
  margin-top: 12px;
}
.browse-bar {
  margin-bottom: 16px;
}
</style>


