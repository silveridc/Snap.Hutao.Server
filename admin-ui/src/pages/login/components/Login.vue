<template>
  <t-form
    ref="form"
    class="item-container login-password"
    :data="formData"
    :rules="FORM_RULES"
    label-width="0"
    @submit="onSubmit"
  >
    <t-form-item name="email">
      <t-input v-model="formData.email" size="large" :placeholder="$t('pages.login.form.emailPlaceholder')" clearable>
        <template #prefix-icon>
          <t-icon name="user" />
        </template>
      </t-input>
    </t-form-item>

    <t-form-item name="password">
      <t-input
        v-model="formData.password"
        size="large"
        :type="showPsw ? 'text' : 'password'"
        clearable
        :placeholder="$t('pages.login.form.passwordPlaceholder')"
      >
        <template #prefix-icon>
          <t-icon name="lock-on" />
        </template>
        <template #suffix-icon>
          <t-icon :name="showPsw ? 'browse' : 'browse-off'" @click="showPsw = !showPsw" />
        </template>
      </t-input>
    </t-form-item>

    <t-form-item class="btn-container">
      <t-button block size="large" type="submit" :loading="loading">{{ $t('pages.login.form.loginBtn') }}</t-button>
    </t-form-item>
  </t-form>
</template>
<script setup lang="ts">
import type { FormInstanceFunctions, FormRule, SubmitContext } from 'tdesign-vue-next';
import { MessagePlugin } from 'tdesign-vue-next';
import { computed, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { t } from '@/locales';
import { useUserStore } from '@/store';

const userStore = useUserStore();

const INITIAL_DATA = {
  email: '',
  password: '',
};

const FORM_RULES = computed<Record<string, FormRule[]>>(() => ({
  email: [{ required: true, message: t('pages.login.rules.email'), type: 'error' }],
  password: [{ required: true, message: t('pages.login.rules.password'), type: 'error' }],
}));

const form = ref<FormInstanceFunctions>();
const formData = ref({ ...INITIAL_DATA });
const showPsw = ref(false);
const loading = ref(false);

const router = useRouter();
const route = useRoute();

const onSubmit = async (ctx: SubmitContext) => {
  if (ctx.validateResult !== true) {
    return;
  }
  loading.value = true;
  try {
    await userStore.login({ email: formData.value.email, password: formData.value.password });
    MessagePlugin.success(t('pages.login.messages.loginSuccess'));
    const redirect = route.query.redirect as string;
    router.push(redirect || '/dashboard');
  } catch (e: unknown) {
    MessagePlugin.error((e as Error).message || t('pages.login.messages.loginFail'));
  } finally {
    loading.value = false;
  }
};
</script>
<style lang="less" scoped>
@import '../index.less';
</style>
