<script setup lang="ts">
import { DialogPlugin, MessagePlugin } from 'tdesign-vue-next';
import { reactive, ref } from 'vue';

import {
  distributionCompensation,
  distributionDesignation,
  gachaLogCompensation,
  gachaLogDesignation,
} from '@/api/service';
import { t } from '@/locales';

const form = reactive({
  serviceType: 'gachalog',
  target: 'all',
  userName: '',
  days: 7,
});
const loading = ref(false);
const lastResult = ref('');

function serviceLabel() {
  return form.serviceType === 'gachalog' ? t('pages.compensation.form.serviceGachalog') : 'CDN';
}

async function doRequest() {
  if (form.serviceType === 'gachalog') {
    return form.target === 'all'
      ? gachaLogCompensation(form.days)
      : gachaLogDesignation(form.userName.trim(), form.days);
  }
  return form.target === 'all'
    ? distributionCompensation(form.days)
    : distributionDesignation(form.userName.trim(), form.days);
}

async function submit() {
  if (!form.days) {
    MessagePlugin.warning(t('pages.compensation.messages.daysRequired'));
    return;
  }
  if (form.target === 'user' && !form.userName.trim()) {
    MessagePlugin.warning(t('pages.compensation.messages.emailRequired'));
    return;
  }
  loading.value = true;
  lastResult.value = '';
  try {
    const res: any = await doRequest();
    lastResult.value = res.message || t('pages.compensation.messages.opSuccess');
    MessagePlugin.success(t('pages.compensation.messages.compensateSuccess'));
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.compensation.messages.opFail'));
  } finally {
    loading.value = false;
  }
}

function onSubmit() {
  if (form.target === 'all') {
    const dialog = DialogPlugin.confirm({
      header: t('pages.compensation.dialog.allHeader'),
      body: t('pages.compensation.dialog.allBody', { service: serviceLabel(), days: form.days }),
      confirmBtn: t('pages.compensation.dialog.allConfirm'),
      theme: 'warning',
      onConfirm: () => {
        dialog.hide();
        submit();
      },
    });
    return;
  }
  submit();
}
</script>

<template>
  <div class="hutao-page">
    <t-card :title="$t('pages.compensation.form.title')" :bordered="false">
      <p class="tip">{{ $t('pages.compensation.form.tip') }}</p>
      <t-form label-align="top">
        <t-row :gutter="16">
          <t-col :span="4">
            <t-form-item :label="$t('pages.compensation.form.serviceType')">
              <t-radio-group v-model="form.serviceType">
                <t-radio-button value="gachalog">{{ $t('pages.compensation.form.serviceGachalog') }}</t-radio-button>
                <t-radio-button value="cdn">{{ $t('pages.compensation.form.serviceCdn') }}</t-radio-button>
              </t-radio-group>
            </t-form-item>
          </t-col>
          <t-col :span="4">
            <t-form-item :label="$t('pages.compensation.form.target')">
              <t-radio-group v-model="form.target">
                <t-radio-button value="all">{{ $t('pages.compensation.form.targetAll') }}</t-radio-button>
                <t-radio-button value="user">{{ $t('pages.compensation.form.targetUser') }}</t-radio-button>
              </t-radio-group>
            </t-form-item>
          </t-col>
          <t-col :span="4">
            <t-form-item :label="$t('pages.compensation.form.days')">
              <t-input-number v-model="form.days" :min="-3650" :max="3650" theme="normal" style="width: 100%" />
            </t-form-item>
          </t-col>
          <t-col v-if="form.target === 'user'" :span="12">
            <t-form-item :label="$t('pages.compensation.form.userEmail')">
              <t-input v-model="form.userName" :placeholder="$t('pages.compensation.form.userEmailPlaceholder')" clearable style="max-width: 480px" />
            </t-form-item>
          </t-col>
          <t-col :span="12">
            <t-form-item>
              <t-button theme="primary" :loading="loading" @click="onSubmit">{{ $t('pages.compensation.form.submit') }}</t-button>
            </t-form-item>
          </t-col>
        </t-row>
      </t-form>
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
