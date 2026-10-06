<template>
  <div class="login-wrapper">
    <login-header />

    <div class="login-container">
      <div class="title-container">
        <h1 class="title margin-no">{{ $t('pages.login.brand') }}</h1>
        <h1 class="title">{{ $t('pages.login.subtitle') }}</h1>
        <div class="sub-title">
          <p class="tip">{{ $t('pages.login.tip') }}</p>
        </div>
      </div>

      <login />
      <tdesign-setting />
    </div>

    <footer class="copyright">Snap Hutao Server Admin</footer>
  </div>
</template>
<script setup lang="ts">
import TdesignSetting from '@/layouts/setting.vue';

import LoginHeader from './components/Header.vue';
import Login from './components/Login.vue';

defineOptions({
  name: 'LoginIndex',
});
</script>
<style lang="less" scoped>
@import './index.less';
</style>
