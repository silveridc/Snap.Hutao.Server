import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router';

import { IconLockStroked, IconUser } from '@douyinfe/semi-icons';
import { Button, Card, Divider, Input, Toast, Typography } from '@douyinfe/semi-ui';

import { getCaptchaSettings } from '@/api/passport';
import { getSsoChallenge, getSsoStatus } from '@/api/sso';
import { useAuth } from '@/auth/AuthProvider';
import { t, useLocale } from '@/i18n';
import { get } from '@/lib/http';
import { getReCaptchaToken } from '@/lib/recaptcha';
import type { TodayWallpaper } from '@/types/api';

export default function LoginPage() {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const { user, login } = useAuth();
  useLocale();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [sso, setSso] = useState({ enabled: false, displayName: '' });
  const [siteKey, setSiteKey] = useState('');
  const [ssoLoading, setSsoLoading] = useState(false);
  const [wallpaper, setWallpaper] = useState('');

  const redirect = params.get('redirect') || '/';

  // 已登录直接进首页，避免二次输入
  useEffect(() => {
    if (user) {
      navigate(redirect, { replace: true });
    }
  }, [user, navigate, redirect]);

  // 身份源或人机验证没配置时后端返回未启用，静默隐藏入口即可
  useEffect(() => {
    getSsoStatus()
      .then((status) => setSso({ enabled: status.Enabled, displayName: status.DisplayName ?? '' }))
      .catch(() => setSso({ enabled: false, displayName: '' }));
    getCaptchaSettings()
      .then((settings) => setSiteKey(settings?.Enabled ? (settings.SiteKey ?? '') : ''))
      .catch(() => setSiteKey(''));
    // 登录页是匿名页，壁纸接口也是公开的，拿不到就退回纯色背景
    get<TodayWallpaper>('/api/wallpapers/today')
      .then((envelope) => setWallpaper(envelope.data?.url ?? ''))
      .catch(() => setWallpaper(''));
  }, []);

  async function onSubmit(event: FormEvent) {
    event.preventDefault();
    if (!email.trim() || !password) {
      Toast.warning(!email.trim() ? t('login.rules.email') : t('login.rules.password'));
      return;
    }

    setSubmitting(true);
    try {
      let captchaToken: string | undefined;
      if (siteKey) {
        try {
          captchaToken = await getReCaptchaToken(siteKey, 'login');
        } catch {
          Toast.error(t('login.messages.captchaFail'));
          return;
        }
      }
      await login(email.trim(), password, captchaToken);
      Toast.success(t('login.messages.loginSuccess'));
      navigate(redirect, { replace: true });
    } catch (error) {
      Toast.error((error as Error).message || t('login.messages.loginFail'));
    } finally {
      setSubmitting(false);
    }
  }

  async function onSsoLogin() {
    setSsoLoading(true);
    try {
      // 授权地址必须整页跳转，跨站的 XHR 拿不到重定向后的结果
      window.location.assign(await getSsoChallenge(redirect === '/' ? '' : redirect));
    } catch (error) {
      Toast.error((error as Error).message || t('login.sso.fail'));
      setSsoLoading(false);
    }
  }

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden px-4 bg-semi-color-bg-0">
      {wallpaper && (
        <>
          <div className="absolute inset-0 bg-cover bg-center" style={{ backgroundImage: `url(${wallpaper})` }} />
          <div className="absolute inset-0 bg-gradient-to-t from-black/60 via-black/25 to-black/45" />
        </>
      )}

      <div className="relative w-full max-w-[380px]">
        <div className="mb-6 text-center">
          <Typography.Title heading={3} className="!mb-1" style={wallpaper ? { color: '#fff' } : undefined}>
            {t('common.brand')}
          </Typography.Title>
          <Typography.Text type="tertiary" style={wallpaper ? { color: 'rgba(255,255,255,.85)' } : undefined}>
            {t('common.subtitle')}
          </Typography.Text>
          <div className="mt-2 text-xs text-semi-color-text-2" style={wallpaper ? { color: 'rgba(255,255,255,.65)' } : undefined}>
            {t('login.tip')}
          </div>
        </div>

        <Card
          bodyStyle={{ padding: 20 }}
          className={wallpaper ? '!bg-white/85 !shadow-lg backdrop-blur' : '!shadow-none'}
        >
          <form onSubmit={onSubmit} className="space-y-3">
            <Input
              size="large"
              prefix={<IconUser />}
              placeholder={t('login.form.emailPlaceholder')}
              value={email}
              onChange={setEmail}
              autoComplete="username"
            />
            <Input
              size="large"
              type="password"
              prefix={<IconLockStroked />}
              placeholder={t('login.form.passwordPlaceholder')}
              value={password}
              onChange={setPassword}
              autoComplete="current-password"
            />
            <Button size="large" block type="primary" theme="solid" htmlType="submit" loading={submitting}>
              {t('login.form.loginBtn')}
            </Button>
          </form>

          {sso.enabled && (
            <>
              <Divider>{t('login.sso.divider')}</Divider>
              <Button size="large" block theme="light" loading={ssoLoading} onClick={onSsoLogin}>
                {t('login.sso.btn', { name: sso.displayName || t('login.sso.defaultName') })}
              </Button>
            </>
          )}
        </Card>

        <div
          className="mt-6 text-center text-xs text-semi-color-text-3"
          style={wallpaper ? { color: 'rgba(255,255,255,.55)' } : undefined}
        >
          {t('common.copyright')}
          {/* 右下角角标被 CSS 隐藏了，Google 条款要求改成在页面上标注出处；后端未启用人机验证时整行不出现 */}
          {siteKey && (
            <div className="mt-1">
              {t('login.captcha.notice')}{' '}
              <a className="underline" href="https://policies.google.com/privacy" target="_blank" rel="noreferrer">
                {t('login.captcha.privacy')}
              </a>
              {' · '}
              <a className="underline" href="https://policies.google.com/terms" target="_blank" rel="noreferrer">
                {t('login.captcha.terms')}
              </a>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
