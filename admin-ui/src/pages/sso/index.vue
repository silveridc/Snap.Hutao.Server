<template>
  <div class="login-wrapper">
    <div class="login-container">
      <div class="title-container">
        <h1 class="title margin-no">{{ $t('pages.login.brand') }}</h1>
        <h1 class="title">{{ $t('pages.sso.title') }}</h1>
      </div>

      <t-card :bordered="false" class="sso-card">
        <t-space v-if="state === 'working'" size="8px">
          <t-loading size="small" />
          <span>{{ $t('pages.sso.working') }}</span>
        </t-space>

        <t-result v-else-if="state === 'done'" theme="success" :title="$t('pages.sso.success')" />

        <template v-else>
          <t-result theme="warning" :title="$t('pages.sso.title')" :description="$t(errorKey)" />
          <div v-if="subject" class="subject-row">
            <span class="label">{{ $t('pages.sso.subject') }}</span>
            <code class="subject">{{ subject }}</code>
            <t-link theme="primary" hover="color" @click="copySubject">{{ $t('pages.sso.copy') }}</t-link>
          </div>
          <div class="action-row">
            <t-button theme="primary" @click="backToLogin">{{ $t('pages.sso.backToLogin') }}</t-button>
          </div>
        </template>
      </t-card>
    </div>

    <footer class="copyright">Snap Hutao Server Admin</footer>
  </div>
</template>
<script setup lang="ts">
import { MessagePlugin } from 'tdesign-vue-next';
import { onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { t } from '@/locales';
import { useUserStore } from '@/store';

defineOptions({
  name: 'SsoIndex',
});

// 身份源回跳携带的错误标记 → 本地文案；细节留在服务端日志里，地址里只有这些固定值
const ERROR_KEYS: Record<string, string> = {
  expired: 'pages.sso.errors.expired',
  upstream: 'pages.sso.errors.upstream',
  invalid_token: 'pages.sso.errors.invalid_token',
  not_provisioned: 'pages.sso.errors.not_provisioned',
  disabled: 'pages.sso.errors.disabled',
};

const route = useRoute();
const router = useRouter();
const userStore = useUserStore();

const state = ref<'working' | 'done' | 'failed'>('working');
const errorKey = ref('pages.sso.errors.unknown');
const subject = ref('');
const returnPath = ref('');

function fail(key: string, shownSubject?: string) {
  errorKey.value = key;
  subject.value = shownSubject ?? '';
  state.value = 'failed';
}

async function exchange(ticket: string) {
  try {
    await userStore.loginWithTicket(ticket);
    state.value = 'done';
    // 让「登录成功」露出一瞬，再回到发起登录时所在的页面
    window.setTimeout(() => router.replace(returnPath.value || '/dashboard'), 500);
  } catch (e: unknown) {
    fail('pages.sso.errors.ticket');
    MessagePlugin.error((e as Error).message);
  }
}

async function copySubject() {
  try {
    await navigator.clipboard.writeText(subject.value);
    MessagePlugin.success(t('pages.sso.copied'));
  } catch {
    MessagePlugin.error(t('pages.server.requestFailed'));
  }
}

function backToLogin() {
  router.replace('/login');
}

onMounted(() => {
  const ticket = route.query.ticket as string;
  const error = route.query.error as string;
  const callbackSubject = (route.query.subject as string) || '';
  returnPath.value = (route.query.return as string) || '';

  // 票据与 state 都是一次性的，先把凭证从地址和历史记录里抹掉
  router.replace({ path: '/sso', query: {} });

  if (ticket) {
    exchange(ticket);
    return;
  }
  if (error) {
    fail(ERROR_KEYS[error] ?? 'pages.sso.errors.unknown', callbackSubject);
    return;
  }
  fail('pages.sso.errors.unknown');
});
</script>
<style lang="less" scoped>
@import '../login/index.less';

.sso-card {
  width: 100%;
  max-width: 460px;
}

.subject-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 16px;

  .label {
    color: var(--td-text-color-secondary);
    flex: none;
  }

  .subject {
    flex: 1;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    padding: 2px 8px;
    background: var(--td-bg-color-secondarycontainer);
    border-radius: var(--td-radius-default);
  }
}

.action-row {
  margin-top: 24px;
  text-align: center;
}
</style>
