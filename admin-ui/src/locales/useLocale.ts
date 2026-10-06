import type { GlobalConfigProvider } from 'tdesign-vue-next';
import { computed } from 'vue';

import type { SupportedLocale } from '@/locales/index';
import { i18n, localeConfigKey, supportedLocales } from '@/locales/index';
import { useNotificationStore } from '@/store/modules/notification';

export function useLocale() {
  const locale = computed({
    get: () => i18n.global.locale.value,
    set: (val: string) => {
      i18n.global.locale.value = val;
    },
  });

  const changeLocale = (lang: string) => {
    const validLang = supportedLocales.includes(lang as SupportedLocale) ? (lang as SupportedLocale) : 'zh_CN';
    locale.value = validLang;
    // 直接同步写入，保证与 getInitialLocale 的读取格式一致、且不依赖 vueuse 的异步 flush
    try {
      localStorage.setItem(localeConfigKey, validLang);
    } catch {
      // 隐私模式等场景忽略
    }
    // 刷新持久化的翻译数据
    useNotificationStore().refreshMsgData();
  };

  const getComponentsLocale = computed(() => {
    return (i18n.global.getLocaleMessage(locale.value) as Record<string, any>).componentsLocale as GlobalConfigProvider;
  });

  return {
    changeLocale,
    getComponentsLocale,
    locale,
  };
}
