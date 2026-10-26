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
    title: t('pages.settings.sections.killSwitch.title'),
    description: t('pages.settings.sections.killSwitch.description'),
    fields: [
      { key: 'Patch.DisableFpsUnlock', label: t('pages.settings.fields.disableFpsUnlock.label'), type: 'switch', hint: t('pages.settings.fields.disableFpsUnlock.hint') },
      { key: 'Patch.DisableIslandInjection', label: t('pages.settings.fields.disableIslandInjection.label'), type: 'switch', hint: t('pages.settings.fields.disableIslandInjection.hint') },
    ],
  },
  {
    title: t('pages.settings.sections.s3Sign.title'),
    description: t('pages.settings.sections.s3Sign.description'),
    fields: [
      { key: 'Patch.S3.Host', label: t('pages.settings.fields.s3Host.label'), type: 'text', hint: t('pages.settings.fields.s3Host.hint') },
      { key: 'Patch.S3.Region', label: t('pages.settings.fields.s3Region.label'), type: 'text', hint: t('pages.settings.fields.s3Region.hint') },
      { key: 'Patch.S3.AccessKeyId', label: t('pages.settings.fields.s3AccessKeyId.label'), type: 'text', hint: t('pages.settings.fields.s3AccessKeyId.hint') },
      { key: 'Patch.S3.SecretAccessKey', label: t('pages.settings.fields.s3SecretAccessKey.label'), type: 'password', hint: t('pages.settings.fields.s3SecretAccessKey.hint') },
      { key: 'Patch.S3.ExpiresHours', label: t('pages.settings.fields.s3ExpiresHours.label'), type: 'text', hint: t('pages.settings.fields.s3ExpiresHours.hint') },
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
  {
    title: t('pages.settings.sections.sso.title'),
    description: t('pages.settings.sections.sso.description'),
    fields: [
      { key: 'Oidc.Enabled', label: t('pages.settings.fields.ssoEnabled.label'), type: 'switch', hint: t('pages.settings.fields.ssoEnabled.hint') },
      { key: 'Oidc.Authority', label: t('pages.settings.fields.ssoAuthority.label'), type: 'text', hint: t('pages.settings.fields.ssoAuthority.hint') },
      { key: 'Oidc.ClientId', label: t('pages.settings.fields.ssoClientId.label'), type: 'text', hint: t('pages.settings.fields.ssoClientId.hint') },
      { key: 'Oidc.ClientSecret', label: t('pages.settings.fields.ssoClientSecret.label'), type: 'password', hint: t('pages.settings.fields.ssoClientSecret.hint') },
      { key: 'Oidc.RedirectUri', label: t('pages.settings.fields.ssoRedirectUri.label'), type: 'text', hint: t('pages.settings.fields.ssoRedirectUri.hint') },
      { key: 'Oidc.ClientRedirectUri', label: t('pages.settings.fields.ssoClientRedirectUri.label'), type: 'text', hint: t('pages.settings.fields.ssoClientRedirectUri.hint') },
      { key: 'Oidc.Scope', label: t('pages.settings.fields.ssoScope.label'), type: 'text', hint: t('pages.settings.fields.ssoScope.hint') },
      { key: 'Oidc.DisplayName', label: t('pages.settings.fields.ssoDisplayName.label'), type: 'text', hint: t('pages.settings.fields.ssoDisplayName.hint') },
      { key: 'Oidc.BootstrapClaim', label: t('pages.settings.fields.ssoBootstrapClaim.label'), type: 'text', hint: t('pages.settings.fields.ssoBootstrapClaim.hint') },
      { key: 'Oidc.BootstrapValue', label: t('pages.settings.fields.ssoBootstrapValue.label'), type: 'text', hint: t('pages.settings.fields.ssoBootstrapValue.hint') },
      { key: 'Oidc.RequireHttpsMetadata', label: t('pages.settings.fields.ssoRequireHttps.label'), type: 'switch', hint: t('pages.settings.fields.ssoRequireHttps.hint') },
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
        <div class="field-grid">
          <div v-for="field in section.fields" :key="field.key" class="field">
            <div class="field-label">{{ field.label }}</div>
            <div class="field-control">
              <t-switch v-if="field.type === 'switch'" v-model="form[field.key]" />
              <template v-else>
                <t-input
                  v-model="form[field.key]"
                  class="field-input"
                  :type="field.type === 'password' ? 'password' : 'text'"
                  :placeholder="$t('pages.settings.form.emptyPlaceholder')"
                  clearable
                />
                <t-tooltip :content="$t('pages.settings.form.resetTooltip')">
                  <t-button variant="text" size="small" theme="default" @click="resetField(field.key)">
                    {{ $t('pages.settings.form.reset') }}
                  </t-button>
                </t-tooltip>
              </template>
            </div>
            <div v-if="field.hint" class="field-hint">{{ field.hint }}</div>
          </div>
        </div>
      </t-card>
    </t-loading>

    <div class="action-bar">
      <div class="action-text">
        <span class="action-title">{{ $t('pages.settings.header.title') }}</span>
        <span class="tip">{{ $t('pages.settings.header.tip') }}</span>
      </div>
      <t-space>
        <t-button variant="outline" :loading="loading" @click="load">{{ $t('pages.settings.header.reload') }}</t-button>
        <t-button theme="primary" :loading="saving" :disabled="!getDirtyKeys().length" @click="save">
          {{ $t('pages.settings.header.saveBtn', { n: getDirtyKeys().length }) }}
        </t-button>
      </t-space>
    </div>
  </div>
</template>

<style lang="less" scoped>
.hutao-card {
  margin-bottom: 16px;
}
.field-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 20px 32px;
}
.field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
}
.field-label {
  color: var(--td-text-color-primary);
  font-size: 14px;
  font-weight: 500;
  line-height: 22px;
}
.field-control {
  display: flex;
  align-items: center;
  gap: 8px;
  min-height: 32px;
}
.field-input {
  flex: 1;
  min-width: 0;
}
.field-hint {
  color: var(--td-text-color-placeholder);
  font-size: 12px;
  line-height: 18px;
}
.action-bar {
  position: sticky;
  bottom: 0;
  z-index: 10;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 24px;
  padding: 12px 24px;
  background: var(--td-bg-color-container);
  border-radius: var(--td-radius-medium);
  box-shadow: 0 2px 12px rgb(0 0 0 / 10%);
}
.action-text {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}
.action-title {
  color: var(--td-text-color-primary);
  font-size: 15px;
  font-weight: 600;
}
.tip {
  color: var(--td-text-color-secondary);
  font-size: 12px;
}
.section-desc {
  color: var(--td-text-color-placeholder);
  font-size: 12px;
}
@media (width <= 1100px) {
  .field-grid {
    grid-template-columns: 1fr;
  }
}
</style>
