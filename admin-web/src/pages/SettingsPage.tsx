import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';

import { useSearchParams } from 'react-router';

import {
  IconCheckCircleStroked,
  IconClockStroked,
  IconGiftStroked,
  IconKeyStroked,
  IconLockStroked,
  IconMailStroked,
  IconServerStroked,
  IconSettingStroked,
  IconShieldStroked,
} from '@douyinfe/semi-icons';
import { Banner, Button, Input, Spin, Switch, TabPane, Tabs, Toast } from '@douyinfe/semi-ui';

import { getSystemConfig, resetSystemConfig, updateSystemConfig } from '@/api/service';
import { t, useLocale } from '@/i18n';
import { useQuery } from '@/hooks/useQuery';
import { readOidcDiscovery } from '@/lib/oidcDiscovery';
import type { SystemConfigMap } from '@/types/api';

interface ConfigField {
  key: string;
  labelKey: string;
  hintKey: string;
  type: 'switch' | 'text' | 'password';
  discover?: boolean;
}

interface ConfigSection {
  id: string;
  icon: ReactNode;
  titleKey: string;
  descriptionKey: string;
  fields: ConfigField[];
}

const CONFIG_SECTIONS: ConfigSection[] = [
  {
    id: 'global',
    icon: <IconSettingStroked size="small" />,
    titleKey: 'settings.sections.global.title',
    descriptionKey: 'settings.sections.global.description',
    fields: [
      { key: 'System.MaintenanceMode', labelKey: 'settings.fields.maintenanceMode.label', hintKey: 'settings.fields.maintenanceMode.hint', type: 'switch' },
      { key: 'System.RegistrationEnabled', labelKey: 'settings.fields.registrationEnabled.label', hintKey: 'settings.fields.registrationEnabled.hint', type: 'switch' },
    ],
  },
  {
    id: 'captcha',
    icon: <IconCheckCircleStroked size="small" />,
    titleKey: 'settings.sections.captcha.title',
    descriptionKey: 'settings.sections.captcha.description',
    fields: [
      { key: 'System.CaptchaEnabled', labelKey: 'settings.fields.captchaEnabled.label', hintKey: 'settings.fields.captchaEnabled.hint', type: 'switch' },
      { key: 'Recaptcha.SiteKey', labelKey: 'settings.fields.recaptchaSiteKey.label', hintKey: 'settings.fields.recaptchaSiteKey.hint', type: 'text' },
      { key: 'Recaptcha.SecretKey', labelKey: 'settings.fields.recaptchaSecretKey.label', hintKey: 'settings.fields.recaptchaSecretKey.hint', type: 'password' },
      { key: 'Recaptcha.MinScore', labelKey: 'settings.fields.recaptchaMinScore.label', hintKey: 'settings.fields.recaptchaMinScore.hint', type: 'text' },
    ],
  },
  {
    id: 'killSwitch',
    icon: <IconShieldStroked size="small" />,
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
    id: 's3Sign',
    icon: <IconServerStroked size="small" />,
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
    id: 'pizzaHelper',
    icon: <IconKeyStroked size="small" />,
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
    id: 'afdian',
    icon: <IconGiftStroked size="small" />,
    titleKey: 'settings.sections.afdian.title',
    descriptionKey: 'settings.sections.afdian.description',
    fields: [
      { key: 'Afdian.SkuGachaLogUploadService', labelKey: 'settings.fields.skuGachaLog.label', hintKey: 'settings.fields.skuGachaLog.hint', type: 'text' },
      { key: 'Afdian.UserId', labelKey: 'settings.fields.afdianUserId.label', hintKey: 'settings.fields.afdianUserId.hint', type: 'text' },
      { key: 'Afdian.UserToken', labelKey: 'settings.fields.afdianUserToken.label', hintKey: 'settings.fields.afdianUserToken.hint', type: 'password' },
      { key: 'Afdian2.SkuCdnService', labelKey: 'settings.fields.skuCdn.label', hintKey: 'settings.fields.skuCdn.hint', type: 'text' },
      { key: 'Afdian2.UserId', labelKey: 'settings.fields.afdian2UserId.label', hintKey: 'settings.fields.afdian2UserId.hint', type: 'text' },
      { key: 'Afdian2.UserToken', labelKey: 'settings.fields.afdian2UserToken.label', hintKey: 'settings.fields.afdian2UserToken.hint', type: 'password' },
    ],
  },
  {
    id: 'duration',
    icon: <IconClockStroked size="small" />,
    titleKey: 'settings.sections.duration.title',
    descriptionKey: 'settings.sections.duration.description',
    fields: [
      { key: 'Passport.RegistrationGiftDays', labelKey: 'settings.fields.registrationGiftDays.label', hintKey: 'settings.fields.registrationGiftDays.hint', type: 'text' },
      { key: 'GachaLog.ExtendDays', labelKey: 'settings.fields.gachaLogExtendDays.label', hintKey: 'settings.fields.gachaLogExtendDays.hint', type: 'text' },
      { key: 'Redeem.CodeExpireDays', labelKey: 'settings.fields.redeemCodeExpireDays.label', hintKey: 'settings.fields.redeemCodeExpireDays.hint', type: 'text' },
      { key: 'JobLog.RetentionDays', labelKey: 'settings.fields.jobLogRetentionDays.label', hintKey: 'settings.fields.jobLogRetentionDays.hint', type: 'text' },
      { key: 'SpiralAbyss.RecordRetentionDays', labelKey: 'settings.fields.spiralAbyssRecordRetentionDays.label', hintKey: 'settings.fields.spiralAbyssRecordRetentionDays.hint', type: 'text' },
    ],
  },
  {
    id: 'mail',
    icon: <IconMailStroked size="small" />,
    titleKey: 'settings.sections.mail.title',
    descriptionKey: 'settings.sections.mail.description',
    fields: [
      { key: 'Smtp.DiagnosticEmailAddress', labelKey: 'settings.fields.diagnosticEmail.label', hintKey: 'settings.fields.diagnosticEmail.hint', type: 'text' },
    ],
  },
  {
    id: 'yae',
    icon: <IconServerStroked size="small" />,
    titleKey: 'settings.sections.yae.title',
    descriptionKey: 'settings.sections.yae.description',
    fields: [
      { key: 'Yae.BucketUrls', labelKey: 'settings.fields.yaeBucketUrls.label', hintKey: 'settings.fields.yaeBucketUrls.hint', type: 'text' },
      { key: 'Yae.ChineseGameHashes', labelKey: 'settings.fields.yaeChineseGameHashes.label', hintKey: 'settings.fields.yaeChineseGameHashes.hint', type: 'text' },
    ],
  },
  {
    id: 'sso',
    icon: <IconLockStroked size="small" />,
    titleKey: 'settings.sections.sso.title',
    descriptionKey: 'settings.sections.sso.description',
    fields: [
      { key: 'Oidc.Enabled', labelKey: 'settings.fields.ssoEnabled.label', hintKey: 'settings.fields.ssoEnabled.hint', type: 'switch' },
      { key: 'Oidc.Authority', labelKey: 'settings.fields.ssoAuthority.label', hintKey: 'settings.fields.ssoAuthority.hint', type: 'text', discover: true },
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

const SWITCH_KEYS = new Set(
  CONFIG_SECTIONS.flatMap((section) => section.fields.filter((field) => field.type === 'switch').map((field) => field.key)),
);

// 「一键填入」的成功提示要报出字段名，而不是配置键
const LABEL_KEYS = new Map(
  CONFIG_SECTIONS.flatMap((section) => section.fields.map((field) => [field.key, field.labelKey] as [string, string])),
);

type FormState = Record<string, string | boolean>;

function initForm(data: SystemConfigMap): { form: FormState; original: FormState } {
  const form: FormState = {};
  const original: FormState = {};
  for (const key of ALL_KEYS) {
    const raw = data[key];
    const value = SWITCH_KEYS.has(key) ? raw === 'true' || raw === 'True' : raw ?? '';
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
  const [discovering, setDiscovering] = useState(false);
  const config = useQuery(getSystemConfig, []);
  const [params, setParams] = useSearchParams();

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

  // 分 Tab 后未保存的字段会被藏起来，靠页签上的小圆点提示哪个分组还有改动
  const dirtySections = useMemo(
    () => new Set(CONFIG_SECTIONS.filter((section) => section.fields.some((field) => form[field.key] !== original[field.key])).map((section) => section.id)),
    [form, original],
  );

  const activeTab = CONFIG_SECTIONS.find((section) => section.id === params.get('tab'))?.id ?? CONFIG_SECTIONS[0]?.id ?? '';

  function selectTab(key: string) {
    const next = new URLSearchParams(params);
    next.set('tab', key);
    setParams(next, { replace: true });
  }

  async function reload() {
    initialized.current = false;
    await config.reload();
  }

  function updateField(key: string, value: string | boolean) {
    setForm((prev) => ({ ...prev, [key]: value }));
  }

  async function discover() {
    const authority = String(form['Oidc.Authority'] ?? '');
    if (!authority.trim()) {
      Toast.error(t('settings.messages.discoverEmpty'));
      return;
    }
    setDiscovering(true);
    try {
      const patch = await readOidcDiscovery(authority);
      const changed = Object.entries(patch).filter(([key, value]) => form[key] !== value);
      if (!changed.length) {
        Toast.info(t('settings.messages.discoverNoChange'));
        return;
      }
      setForm((prev) => ({ ...prev, ...patch }));
      const names = changed.map(([key]) => t(LABEL_KEYS.get(key) ?? key)).join(' / ');
      Toast.success(t('settings.messages.discoverSuccess', { fields: names }));
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setDiscovering(false);
    }
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

  // 全开关的分组三列铺开；带开关的保持两列，让开关和它的熔断原因成对落在同一行；
  // 纯输入项的分组在超宽屏上给到三列
  function gridClass(fields: ConfigField[]) {
    const switches = fields.filter((field) => field.type === 'switch').length;
    if (switches === fields.length) {
      return 'grid grid-cols-1 gap-3 md:grid-cols-3';
    }
    if (switches > 0) {
      return 'grid grid-cols-1 gap-x-8 gap-y-5 md:grid-cols-2';
    }
    return 'grid grid-cols-1 gap-x-8 gap-y-5 md:grid-cols-2 2xl:grid-cols-3';
  }

  function renderField(field: ConfigField) {
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
          {field.discover && (
            <Button size="small" type="tertiary" loading={discovering} onClick={discover}>
              {t('settings.form.discover')}
            </Button>
          )}
          <Button size="small" type="tertiary" onClick={() => resetField(field.key)}>
            {t('settings.form.reset')}
          </Button>
        </div>
        <div className="text-xs leading-[18px] text-[var(--semi-color-text-2)]">{t(field.hintKey)}</div>
      </div>
    );
  }

  return (
    <div className="w-full space-y-4 pb-24">
      {config.error && <Banner type="danger" closeIcon={null} description={config.error.message} />}

      <Spin spinning={config.loading}>
        {/* newapi classic 的设置页：一个分组一个卡片式页签，页签写进 ?tab= 方便直接分享定位 */}
        <Tabs type="card" collapsible activeKey={activeTab} onChange={selectTab}>
          {CONFIG_SECTIONS.map((section) => (
            <TabPane
              key={section.id}
              itemKey={section.id}
              tab={
                <span className="flex items-center gap-1.5">
                  {section.icon}
                  {t(section.titleKey)}
                  {dirtySections.has(section.id) && <span className="h-1.5 w-1.5 rounded-full bg-[var(--semi-color-warning)]" />}
                </span>
              }
            >
              {activeTab === section.id && (
                <div className="px-1 pt-5">
                  <p className="mb-4 text-xs text-[var(--semi-color-text-2)]">{t(section.descriptionKey)}</p>
                  <div className={gridClass(section.fields)}>
                    {section.fields.map(renderField)}
                  </div>
                </div>
              )}
            </TabPane>
          ))}
        </Tabs>
      </Spin>

      <div className="sticky bottom-4 z-10 flex items-center justify-end gap-6 rounded-[var(--semi-border-radius-medium)] bg-[var(--semi-color-bg-0)] px-6 py-3 shadow-[0_2px_12px_rgba(0,0,0,0.1)] md:justify-between">
        {/* 窄屏放不下标题列，硬挤会变成一行一个字；页面标题在顶栏已经有了，这里直接收起 */}
        <div className="hidden min-w-0 flex-col gap-0.5 md:flex">
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
