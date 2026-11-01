import { useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router';

import { Button, Empty, Spin, Toast, Typography } from '@douyinfe/semi-ui';

import { useAuth } from '@/auth/AuthProvider';
import { t, useLocale } from '@/i18n';

// 身份源回跳携带的错误标记 → 本地文案；细节留在服务端日志里，地址里只有这些固定值
const ERROR_KEYS: Record<string, string> = {
  expired: 'sso.errors.expired',
  upstream: 'sso.errors.upstream',
  invalid_token: 'sso.errors.invalidToken',
  not_provisioned: 'sso.errors.notProvisioned',
  disabled: 'sso.errors.disabled',
};

export default function SsoCallbackPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { loginWithTicket } = useAuth();
  useLocale();

  const [error, setError] = useState('');
  const [subject, setSubject] = useState('');
  const used = useRef(false);

  useEffect(() => {
    const ticket = params.get('ticket');
    // 服务端用 return 带回发起登录时所在的页面，缺省回首页
    const redirect = params.get('return') || '/';
    if (!ticket) {
      const slug = params.get('error') ?? '';
      setSubject(params.get('subject') ?? '');
      setError(t(ERROR_KEYS[slug] ?? 'sso.errors.unknown'));
      return;
    }
    // 票据一次性，StrictMode 的双调用会让第二次必然失败
    if (used.current) {
      return;
    }
    used.current = true;

    loginWithTicket(ticket)
      .then(() => navigate(redirect, { replace: true }))
      .catch((err: unknown) => setError(err instanceof Error ? err.message : t('sso.errors.ticket')));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function copySubject() {
    try {
      await navigator.clipboard.writeText(subject);
      Toast.success(t('sso.copied'));
    } catch {
      Toast.error(t('common.operationFail'));
    }
  }

  if (error) {
    return (
      <div className="flex min-h-screen items-center justify-center px-4">
        <div className="w-full max-w-[460px]">
          <Empty title={t('sso.fail')} description={error} />
          {subject && (
            <div className="mt-3 flex items-baseline justify-center gap-2 text-[13px]">
              <span className="text-semi-color-text-2">{t('sso.subject')}</span>
              <code className="font-mono text-semi-color-text-0">{subject}</code>
              <Button theme="borderless" type="primary" size="small" onClick={copySubject}>
                {t('common.copy')}
              </Button>
            </div>
          )}
          <div className="mt-2 text-center text-xs text-semi-color-text-2">{t('sso.retryHint')}</div>
          <div className="mt-4 text-center">
            <Button type="primary" onClick={() => navigate('/login', { replace: true })}>
              {t('common.backHome')}
            </Button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-3 px-4">
      <Spin size="large" />
      <Typography.Text type="tertiary">{t('sso.exchanging')}</Typography.Text>
    </div>
  );
}
