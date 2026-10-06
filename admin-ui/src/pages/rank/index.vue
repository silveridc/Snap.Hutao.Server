<script setup lang="ts">
import { MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, ref } from 'vue';

import { getRankKeys, getRankLeaderboard } from '@/api/service';
import { t } from '@/locales';

const keys = ref<string[]>([]);
const selectedKey = ref('');
const count = ref(50);
const loading = ref(false);
const keysLoading = ref(false);
const rows = ref<any[]>([]);

const keyOptions = computed(() => keys.value.map((k) => ({ label: k, value: k })));

const columns = computed(() => [
  { colKey: 'index', title: '#', width: 70 },
  { colKey: 'Uid', title: 'UID', width: 180 },
  { colKey: 'Value', title: t('pages.rank.view.colValue') },
]);

async function loadKeys() {
  keysLoading.value = true;
  try {
    const res: any = await getRankKeys();
    keys.value = res.data ?? [];
    if (!selectedKey.value && keys.value.length) {
      selectedKey.value = keys.value.find((k) => k === 'Hutao.Damage.Avatar.All') ?? keys.value[0];
      await loadBoard();
    }
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.rank.messages.loadKeysFail'));
  } finally {
    keysLoading.value = false;
  }
}

async function loadBoard() {
  if (!selectedKey.value) {
    MessagePlugin.warning(t('pages.rank.messages.selectFirst'));
    return;
  }
  loading.value = true;
  try {
    const res: any = await getRankLeaderboard(selectedKey.value, count.value);
    rows.value = res.data ?? [];
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.rank.messages.loadBoardFail'));
  } finally {
    loading.value = false;
  }
}

onMounted(loadKeys);
</script>

<template>
  <div class="hutao-page">
    <t-card :title="$t('pages.rank.view.title')" :bordered="false" class="hutao-card">
      <p class="tip">{{ $t('pages.rank.view.tipPre') }}<code>Hutao.*.Avatar.*</code>{{ $t('pages.rank.view.tipSuf') }}</p>
      <t-space class="bar" break-line>
        <t-select
          v-model="selectedKey"
          :options="keyOptions"
          :loading="keysLoading"
          :placeholder="$t('pages.rank.view.selectPlaceholder')"
          filterable
          style="width: 320px"
        />
        <t-input-number v-model="count" :min="1" :max="500" style="width: 160px" />
        <t-button theme="primary" :loading="loading" @click="loadBoard">{{ $t('pages.rank.view.query') }}</t-button>
        <t-button variant="outline" :loading="keysLoading" @click="loadKeys">{{ $t('pages.rank.view.refreshKeys') }}</t-button>
      </t-space>
      <t-table row-key="Uid" :data="rows" :columns="columns" :loading="loading" max-height="560" size="small">
        <template #index="{ rowIndex }">{{ rowIndex + 1 }}</template>
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
.bar {
  margin-bottom: 16px;
}
</style>
