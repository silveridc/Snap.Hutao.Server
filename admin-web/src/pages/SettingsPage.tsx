import { useEffect, useMemo, useRef, useState } from 'react';

import { Banner, Button, Input, Spin, Switch, Toast } from '@douyinfe/semi-ui';

import { getSystemConfig, resetSystemConfig, updateSystemConfig } from '@/api/service';
import { t, useLocale } from '@/i18n';
import { useQuery } from '@/hooks/useQuery';
import type { SystemConfigMap } from '@/types/api';

interface ConfigField {
  key: string;
  labelKey: string;
  hintKey: string;
  type: 'switch' | 'text' | 'password';
}

interface ConfigSection {
  titleKey: string;
  descriptionKey: string;
  fields: ConfigField[];
}

const CONFIG_SECTIONS: ConfigSection[] = [
  {
    titleKey: 'settings.sections.global.title',
    descriptionKey: 'settings.sections.global.description',
    fields: [
      { key: 'System.MaintenanceMode', labelKey: 'settings.fields.maintenanceMode.label', hintKey: 'settings.fields.maintenanceMode.hint', type: 'switch' },
      { key: 'System.RegistrationEnabled', labelKey: 'settings.fields.registrationEnabled.label', hintKey: 'settings.fields.registrationEnabled.hint', type: 'switch' },
      { key: 'System.CaptchaEnabled', labelKey: 'settings.fields.captchaEnabled.label', hintKey: 'settings.fields.captchaEnabled.hint', type: 'switch' },
    ],
  },
  {
    titleKey: 'settings.sections.killSwitch.title',
    descriptionKey: 'settings.sections.killSwitch.description',
    fields: [
      { key: 'Patch.DisableFpsUnlock', labelKey: 'settings.fields.disableFpsUnlock.label', hintKey: 'settings.fields.disableFpsUnlock.hint', type: 'switch' },
      { key: 'Patch.DisableFpsUnlockReason', labelKey: 'settings.fields.disableFpsUnlockReason.label', hintKey: 'settings.fields.disableFpsUnlockReason.hint', type: 'text' },
      { key: 'Patch.DisableIslandInjection', labelKey: 'settings.fields.disableIslandInjection.label', hintKey: 'settings.fields.disableIslandInjection.hint', type: 'switch' },
      { key: 'Patch.DisableIslandInjectionReason', labelKey: 'settings.fields.disableIslandInjectionReason.label', hintKey: 'settings.fields.disableIslandInjectionReason.hint', type: 'text' },
    ],
  },
  {
    titleKey: 'settings.sections.s3Sign.title',
    descriptionKey: 'settings.sections.s3Sign.description',
    fields: [
      { key: 'Patch.S3.Host', labelKey: 'settings.fields.s3Host.label', hintKey: 'settings.fields.s3Host.hint', type: 'text' },
      { key: 'Patch.S3.Region', labelKey: 'settings.fields.s3Region.label', hintKey: 'settings.fields.s3Region.hint', type: 'text' },
      { key: 'Patch.S3.AccessKeyId', labelKey: 'settings.fields.s3AccessKeyId.label', hintKey: 'settings.fields.s3AccessKeyId.hint', type: 'text' },
      { key: 'Patch.S3.SecretAccessKey', labelKey: 'settings.fields.s3SecretAccessKey.label', hintKey: 'settings.fields.s3SecretAccessKey.hint', type: 'password' },
      { key: 'Patch.S3.ExpiresHours', labelKey: 'settings.fields.s3ExpiresHours.label', hintKey: 'settings.fields.s3ExpiresHours.hint', type: 'text' },
    ],
  },
  {
    titleKey: 'settings.sections.pizzaHelper.title',
    descriptionKey: 'settings.sections.pizzaHelper.description',
    fields: [
      { key: 'PizzaHelper.UidSalt', labelKey: 'settings.fields.uidSalt.label', hintKey: 'settings.fields.uidSalt.hint', type: 'text' },
      { key: 'PizzaHelper.ApiSalt', labelKey: 'settings.fields.apiSalt.label', hintKey: 'settings.fields.apiSalt.hint', type: 'text' },
      { key: 'PizzaHelper.EndPoints.AvatarHolding', labelKey: 'settings.fields.avatarHolding.label', hintKey: 'settings.fields.avatarHolding.hint', type: 'text' },
      { key: 'PizzaHelper.EndPoints.SpiralAbyss', labelKey: 'settings.fields.spiralAbyss.label', hintKey: 'settings.fields.spiralAbyss.hint', type: 'text' },
    ],
  },
  {
    titleKey: 'settings.sections.afdian.title',
    descriptionKey: 'settings.sections.afdian.description',
    fields: [
      { key: 'Afdian.SkuGachaLogUploadService', labelKey: 'settings.fields.skuGachaLog.label', hintKey: 'settings.fields.skuGachaLog.hint', type: 'text' },
      { key: 'Afdian2.SkuCdnService', labelKey: 'settings.fields.skuCdn.label', hintKey: 'settings.fields.skuCdn.hint', type: 'text' },
    ],
  },
  {
    titleKey: 'settings.sections.mail.title',
    descriptionKey: 'settings.sections.mail.description',
    fields: [
      { key: 'Smtp.DiagnosticEmailAddress', labelKey: 'settings.fields.diagnosticEmail.label', hintKey: 'settings.fields.diagnosticEmail.hint', type: 'text' },
    ],
  },
  {
    titleKey: 'settings.sections.sso.title',
    descriptionKey: 'settings.sections.sso.description',
    fields: [
      { key: 'Oidc.Enabled', labelKey: 'settings.fields.ssoEnabled.label', hintKey: 'settings.fields.ssoEnabled.hint', type: 'switch' },
      { key: 'Oidc.Authority', labelKey: 'settings.fields.ssoAuthority.label', hintKey: 'settings.fields.ssoAuthority.hint', type: 'text' },
      { key: 'Oidc.ClientId', labelKey: 'settings.fields.ssoClientId.label', hintKey: 'settings.fields.ssoClientId.hint', type: 'text' },
      { key: 'Oidc.ClientSecret', labelKey: 'settings.fields.ssoClientSecret.label', hintKey: 'settings.fields.ssoClientSecret.hint', type: 'password' },
      { key: 'Oidc.RedirectUri', labelKey: 'settings.fields.ssoRedirectUri.label', hintKey: 'settings.fields.ssoRedirectUri.hint', type: 'text' },
      { key: 'Oidc.ClientRedirectUri', labelKey: 'settings.fields.ssoClientRedirectUri.label', hintKey: 'settings.fields.ssoClientRedirectUri.hint', type: 'text' },
      { key: 'Oidc.Scope', labelKey: 'settings.fields.ssoScope.label', hintKey: 'settings.fields.ssoScope.hint', type: 'text' },
      { key: 'Oidc.DisplayName', labelKey: 'settings.fields.ssoDisplayName.label', hintKey: 'settings.fields.ssoDisplayName.hint', type: 'text' },
      { key: 'Oidc.BootstrapClaim', labelKey: 'settings.fields.ssoBootstrapClaim.label', hintKey: 'settings.fields.ssoBootstrapClaim.hint', type: 'text' },
      { key: 'Oidc.BootstrapValue', labelKey: 'settings.fields.ssoBootstrapValue.label', hintKey: 'settings.fields.ssoBootstrapValue.hint', type: 'text' },
      { key: 'Oidc.RequireHttpsMetadata', labelKey: 'settings.fields.ssoRequireHttps.label', hintKey: 'settings.fields.ssoRequireHttps.hint', type: 'switch' },
    ],
  },
];

const ALL_KEYS = CONFIG_SECTIONS.flatMap((section) => section.fields.map((field) => field.key));

type FormState = Record<string, string | boolean>;

function initForm(data: SystemConfigMap): { form: FormState; original: FormState } {
  const form: FormState = {};
  const original: FormState = {};
  for (const key of ALL_KEYS) {
    const raw = data[key];
    const isSwitch = CONFIG_SECTIONS.some((section) => section.fields.some((field) => field.key === key && field.type === 'switch'));
    const value = isSwitch ? raw === 'true' || raw === 'True' : raw ?? '';
    form[key] = value;
    original[key] = value;
  }
  return { form, original };
}

export default function SettingsPage() {
  useLocale();

  const [form, setForm] = useState<FormState>({});
  const [original, setOriginal] = useState<FormState>({});
  const [saving, setSaving] = useState(false);
  const config = useQuery(getSystemConfig, []);

  const initialized = useRef(false);

  useEffect(() => {
    if (!config.data || initialized.current) {
      return;
    }
    initialized.current = true;
    const { form: nextForm, original: nextOriginal } = initForm(config.data);
    setForm(nextForm);
    setOriginal(nextOriginal);
  }, [config.data]);

  const dirtyKeys = useMemo(
    () => ALL_KEYS.filter((key) => form[key] !== original[key]),
    [form, original],
  );

  async function reload() {
    initialized.current = false;
    await config.reload();
  }

  function updateField(key: string, value: string | boolean) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  async function save() {
    if (!dirtyKeys.length) {
      Toast.info(t('settings.messages.noChanges'));
      return;
    }
    setSaving(true);
    try {
      const payload: Record<string, string | null> = {};
      for (const key of dirtyKeys) {
        const value = form[key];
        payload[key] = typeof value === 'boolean' ? (value ? 'true' : 'false') : String(value) || null;
      }
      const message = await updateSystemConfig(payload);
      Toast.success(message || t('settings.messages.saveSuccess'));
      setOriginal({ ...form });
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setSaving(false);
    }
  }

  async function resetField(key: string) {
    try {
      const message = await resetSystemConfig(key);
      Toast.success(message || t('settings.messages.resetSuccess'));
      await reload();
    } catch (error) {
      Toast.error((error as Error).message);
    }
  }

  return (
    <div className="space-y-4 pb-24">
      {config.error && <Banner type="danger" closeIcon={null} description={config.error.message} />}

      <Spin spinning={config.loading}>
        <div className="space-y-4">
          {CONFIG_SECTIONS.map((section) => {
            const allSwitches = section.fields.every((field) => field.type === 'switch');
            return (
              <div key={section.titleKey} className="rounded-[var(--semi-border-radius-medium)] bg-[var(--semi-color-bg-0)] p-6">
                <h3 className="text-base font-semibold">{t(section.titleKey)}</h3>
                <p className="mb-4 mt-1 text-xs text-[var(--semi-color-text-2)]">{t(section.descriptionKey)}</p>

                <div className={allSwitches ? 'grid grid-cols-1 gap-3 md:grid-cols-3' : 'grid grid-cols-1 gap-x-8 gap-y-5 lg:grid-cols-2'}>
                  {section.fields.map((field) => {
                    const value = form[field.key];
                    if (field.type === 'switch') {
                      return (
                        <div key={field.key} className="rounded-md border border-[var(--semi-color-border)] px-4 py-2.5">
                          <div className="flex items-center justify-between gap-3">
                            <span className="text-sm font-medium">{t(field.labelKey)}</span>
                            <Switch checked={Boolean(value)} onChange={(checked) => updateField(field.key, checked)} />
                          </div>
                          <div className="mt-1 text-xs leading-[18px] text-[var(--semi-color-text-2)]">{t(field.hintKey)}</div>
                        </div>
                      );
                    }
                    return (
                      <div key={field.key} className="flex min-w-0 flex-col gap-1.5">
                        <span className="text-sm font-medium">{t(field.labelKey)}</span>
                        <div className="flex items-center gap-2">
                          <Input
                            className="min-w-0 flex-1"
                            mode={field.type === 'password' ? 'password' : undefined}
                            value={String(value ?? '')}
                            placeholder={t('settings.form.emptyPlaceholder')}
                            onChange={(next) => updateField(field.key, next)}
                          />
                          <Button size="small" type="tertiary" onClick={() => resetField(field.key)}>
                            {t('settings.form.reset')}
                          </Button>
                        </div>
                        <div className="text-xs leading-[18px] text-[var(--semi-color-text-2)]">{t(field.hintKey)}</div>
                      </div>
                    );
                  })}
                </div>
              </div>
            );
          })}
        </div>
      </Spin>

      <div className="sticky bottom-4 z-10 flex items-center justify-between gap-6 rounded-[var(--semi-border-radius-medium)] bg-[var(--semi-color-bg-0)] px-6 py-3 shadow-[0_2px_12px_rgba(0,0,0,0.1)]">
        <div className="flex min-w-0 flex-col gap-0.5">
          <span className="text-[15px] font-semibold">{t('settings.header.title')}</span>
          <span className="text-xs text-[var(--semi-color-text-2)]">{t('settings.header.tip')}</span>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <Button type="tertiary" loading={config.loading} onClick={reload}>
            {t('settings.header.reload')}
          </Button>
          <Button theme="solid" loading={saving} disabled={!dirtyKeys.length} onClick={save}>
            {t('settings.header.saveBtn', { n: dirtyKeys.length })}
          </Button>
        </div>
      </div>
    </div>
  );
}
