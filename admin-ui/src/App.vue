<template>
  <t-config-provider :global-config="getComponentsLocale">
    <router-view :key="locale" :class="[mode]" />
  </t-config-provider>
</template>
<script setup lang="ts">
import { MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, onUnmounted } from 'vue';
import { useRouter } from 'vue-router';

import { t } from '@/locales';
import { useLocale } from '@/locales/useLocale';
import { useSettingStore, useUserStore } from '@/store';
import { AUTH_EXPIRED_EVENT } from '@/utils/token';

const store = useSettingStore();
const userStore = useUserStore();
const router = useRouter();

const mode = computed(() => {
  return store.displayMode;
});

const { getComponentsLocale, locale } = useLocale();

function onAuthExpired() {
  userStore.logout();
  if (router.currentRoute.value.path !== '/login') {
    MessagePlugin.warning(t('common.sessionExpired'));
    router.replace({ path: '/login' });
  }
}

onMounted(() => window.addEventListener(AUTH_EXPIRED_EVENT, onAuthExpired));
onUnmounted(() => window.removeEventListener(AUTH_EXPIRED_EVENT, onAuthExpired));
</script>
<style lang="less" scoped>
#nprogress .bar {
  background: var(--td-brand-color) !important;
}
</style>
