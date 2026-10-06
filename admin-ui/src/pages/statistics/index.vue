<script setup lang="ts">
import { DialogPlugin, MessagePlugin } from 'tdesign-vue-next';
import { ref } from 'vue';

import { runStatistics } from '@/api/service';
import { t } from '@/locales';

const loading = ref(false);
const lastResult = ref('');

async function run() {
  loading.value = true;
  lastResult.value = '';
  try {
    const res: any = await runStatistics();
    lastResult.value = res.message || t('pages.statistics.messages.opSuccess');
    MessagePlugin.success(t('pages.statistics.run.successMsg'));
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.statistics.messages.opFail'));
  } finally {
    loading.value = false;
  }
}

function confirmRun() {
  const dialog = DialogPlugin.confirm({
    header: t('pages.statistics.run.dialogHeader'),
    body: t('pages.statistics.run.dialogBody'),
    confirmBtn: t('pages.statistics.run.dialogConfirm'),
    onConfirm: async () => {
      dialog.hide();
      await run();
    },
  });
}
</script>

<template>
  <div class="hutao-page">
    <t-card :title="$t('pages.statistics.run.title')" :bordered="false">
      <p class="tip">{{ $t('pages.statistics.run.tipPre') }} <code>GET /Service/Statistics/Run</code>{{ $t('pages.statistics.run.tipSuf') }}</p>
      <t-button theme="primary" :loading="loading" @click="confirmRun">{{ $t('pages.statistics.run.triggerBtn') }}</t-button>
      <t-alert v-if="lastResult" class="result" theme="success" :message="lastResult" />
    </t-card>
  </div>
</template>

<style lang="less" scoped>
.tip {
  margin-bottom: 16px;
  color: var(--td-text-color-secondary);
  font-size: 13px;
}
.result {
  margin-top: 12px;
}
</style>
