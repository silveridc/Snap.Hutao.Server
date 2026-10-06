<script setup lang="ts">
import { MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, reactive, ref } from 'vue';

import { getSystemConfig, resetSystemConfig, updateSystemConfig } from '@/api/service';
import { t } from '@/locales';

const loading = ref(false);
const saving = ref(false);

const CONFIG_SECTIONS = computed(() => [
  {
    title: t('pages.settings.sections.global.title'),
    description: t('pages.settings.sections.global.description'),
    fields: [
      { key: 'System.MaintenanceMode', label: t('pages.settings.fields.maintenanceMode.label'), type: 'switch', hint: t('pages.settings.fields.maintenanceMode.hint') },
      { key: 'System.RegistrationEnabled', label: t('pages.settings.fields.registrationEnabled.label'), type: 'switch', hint: t('pages.settings.fields.registrationEnabled.hint') },
      { key: 'System.CaptchaEnabled', label: t('pages.settings.fields.captchaEnabled.label'), type: 'switch', hint: t('pages.settings.fields.captchaEnabled.hint') },
    ],
  },
  {
    title: t('pages.settings.sections.pizzaHelper.title'),
    description: t('pages.settings.sections.pizzaHelper.description'),
    fields: [
      { key: 'PizzaHelper.UidSalt', label: t('pages.settings.fields.uidSalt.label'), type: 'text', hint: t('pages.settings.fields.uidSalt.hint') },
      { key: 'PizzaHelper.ApiSalt', label: t('pages.settings.fields.apiSalt.label'), type: 'text', hint: t('pages.settings.fields.apiSalt.hint') },
      { key: 'PizzaHelper.EndPoints.AvatarHolding', label: t('pages.settings.fields.avatarHolding.label'), type: 'text', hint: t('pages.settings.fields.avatarHolding.hint') },
      { key: 'PizzaHelper.EndPoints.SpiralAbyss', label: t('pages.settings.fields.spiralAbyss.label'), type: 'text', hint: t('pages.settings.fields.spiralAbyss.hint') },
    ],
  },
  {
    title: t('pages.settings.sections.afdian.title'),
    description: t('pages.settings.sections.afdian.description'),
    fields: [
      { key: 'Afdian.SkuGachaLogUploadService', label: t('pages.settings.fields.skuGachaLog.label'), type: 'text', hint: t('pages.settings.fields.skuGachaLog.hint') },
      { key: 'Afdian2.SkuCdnService', label: t('pages.settings.fields.skuCdn.label'), type: 'text', hint: t('pages.settings.fields.skuCdn.hint') },
    ],
  },
  {
    title: t('pages.settings.sections.mail.title'),
    description: t('pages.settings.sections.mail.description'),
    fields: [
      { key: 'Smtp.DiagnosticEmailAddress', label: t('pages.settings.fields.diagnosticEmail.label'), type: 'text', hint: t('pages.settings.fields.diagnosticEmail.hint') },
    ],
  },
]);

const form = reactive<Record<string, string | boolean>>({});
const original = reactive<Record<string, string | boolean>>({});

function initForm(data: Record<string, string | null>) {
  for (const section of CONFIG_SECTIONS.value) {
    for (const field of section.fields) {
      const raw = data[field.key];
      if (field.type === 'switch') {
        const val = raw === 'true' || raw === 'True';
        form[field.key] = val;
        original[field.key] = val;
      } else {
        form[field.key] = raw ?? '';
        original[field.key] = raw ?? '';
      }
    }
  }
}

async function load() {
  loading.value = true;
  try {
    const res: any = await getSystemConfig();
    initForm(res.data ?? {});
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.settings.messages.loadFail'));
  } finally {
    loading.value = false;
  }
}

function getDirtyKeys(): string[] {
  const keys: string[] = [];
  for (const section of CONFIG_SECTIONS.value) {
    for (const field of section.fields) {
      if (form[field.key] !== original[field.key]) {
        keys.push(field.key);
      }
    }
  }
  return keys;
}

async function save() {
  const dirtyKeys = getDirtyKeys();
  if (!dirtyKeys.length) {
    MessagePlugin.info(t('pages.settings.messages.noChanges'));
    return;
  }
  saving.value = true;
  try {
    const payload: Record<string, string | null> = {};
    for (const key of dirtyKeys) {
      const val = form[key];
      if (typeof val === 'boolean') {
        payload[key] = val ? 'true' : 'false';
      } else {
        payload[key] = val || null;
      }
    }
    const res: any = await updateSystemConfig(payload);
    MessagePlugin.success(res.message || t('pages.settings.messages.saveSuccess'));
    for (const key of dirtyKeys) {
      original[key] = form[key];
    }
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.settings.messages.saveFail'));
  } finally {
    saving.value = false;
  }
}

async function resetField(key: string) {
  try {
    const res: any = await resetSystemConfig(key);
    MessagePlugin.success(res.message || t('pages.settings.messages.resetSuccess'));
    await load();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.settings.messages.resetFail'));
  }
}

onMounted(load);
</script>

<template>
  <div class="hutao-page">
    <t-card :bordered="false" class="hutao-card header-card">
      <t-space align="center" style="width: 100%; justify-content: space-between">
        <div>
          <h3 style="margin: 0">{{ $t('pages.settings.header.title') }}</h3>
          <p class="tip" style="margin: 4px 0 0">
            {{ $t('pages.settings.header.tip') }}
          </p>
        </div>
        <t-space>
          <t-button variant="outline" :loading="loading" @click="load">{{ $t('pages.settings.header.reload') }}</t-button>
          <t-button theme="primary" :loading="saving" :disabled="!getDirtyKeys().length" @click="save">
            {{ $t('pages.settings.header.saveBtn', { n: getDirtyKeys().length }) }}
          </t-button>
        </t-space>
      </t-space>
    </t-card>

    <t-loading :loading="loading" show-overlay>
      <t-card
        v-for="section in CONFIG_SECTIONS"
        :key="section.title"
        :title="section.title"
        :bordered="false"
        class="hutao-card"
      >
        <template #subtitle>
          <span class="section-desc">{{ section.description }}</span>
        </template>
        <t-form label-align="left" label-width="140px">
          <t-row :gutter="16">
            <t-col v-for="field in section.fields" :key="field.key" :span="field.type === 'switch' ? 4 : 6">
              <t-form-item :label="field.label" :help="field.hint">
                <template v-if="field.type === 'switch'">
                  <t-switch v-model="form[field.key]" />
                </template>
                <template v-else>
                  <t-space align="center" style="width: 100%">
                    <t-input
                      v-model="form[field.key]"
                      :placeholder="$t('pages.settings.form.emptyPlaceholder')"
                      clearable
                      style="max-width: 360px"
                    />
                    <t-tooltip :content="$t('pages.settings.form.resetTooltip')">
                      <t-button
                        variant="text"
                        size="small"
                        theme="default"
                        @click="resetField(field.key)"
                      >
                        {{ $t('pages.settings.form.reset') }}
                      </t-button>
                    </t-tooltip>
                  </t-space>
                </template>
              </t-form-item>
            </t-col>
          </t-row>
        </t-form>
      </t-card>
    </t-loading>
  </div>
</template>

<style lang="less" scoped>
.hutao-card {
  margin-bottom: 16px;
}
.header-card {
  position: sticky;
  top: 0;
  z-index: 10;
}
.tip {
  color: var(--td-text-color-secondary);
  font-size: 13px;
}
.section-desc {
  color: var(--td-text-color-placeholder);
  font-size: 12px;
}
</style>
