import { useState } from 'react';

import {
  Banner,
  Button,
  Card,
  Input,
  InputNumber,
  Modal,
  Radio,
  RadioGroup,
  Toast,
  Typography,
} from '@douyinfe/semi-ui';

import {
  compensateCdnDuration,
  compensateGachaLogDuration,
  grantCdnDuration,
  grantGachaLogDuration,
} from '@/api/service';
import { Field } from '@/components/Field';
import { t, useLocale } from '@/i18n';

type ServiceType = 'gachalog' | 'cdn';
type Target = 'all' | 'user';

export default function CompensationPage() {
  useLocale();

  const [service, setService] = useState<ServiceType>('gachalog');
  const [target, setTarget] = useState<Target>('all');
  const [days, setDays] = useState(7);
  const [userName, setUserName] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState('');

  function compensate(): Promise<string> {
    if (target === 'all') {
      return service === 'gachalog' ? compensateGachaLogDuration(days) : compensateCdnDuration(days);
    }
    return service === 'gachalog'
      ? grantGachaLogDuration(userName.trim(), days)
      : grantCdnDuration(userName.trim(), days);
  }

  async function submit() {
    setSubmitting(true);
    setResult('');
    try {
      const message = await compensate();
      setResult(message || t('common.success'));
      Toast.success(message || t('common.success'));
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setSubmitting(false);
    }
  }

  function onSubmit() {
    if (!days) {
      Toast.warning(t('compensation.messages.daysRequired'));
      return;
    }
    if (target === 'user') {
      if (!userName.trim()) {
        Toast.warning(t('compensation.messages.emailRequired'));
        return;
      }
      submit();
      return;
    }
    Modal.confirm({
      title: t('compensation.dialog.allTitle'),
      content: t('compensation.dialog.allBody', {
        service: service === 'gachalog' ? t('compensation.form.serviceGachalog') : t('compensation.form.serviceCdn'),
        days,
      }),
      okText: t('compensation.dialog.allConfirm'),
      cancelText: t('common.cancel'),
      onOk: submit,
    });
  }

  return (
    <Card title={t('compensation.form.title')}>
      <Typography.Text type="tertiary" className="mb-4 block">
        {t('compensation.form.tip')}
      </Typography.Text>

      <div className="grid max-w-3xl gap-4 sm:grid-cols-2">
        <Field label={t('compensation.form.serviceType')}>
          <RadioGroup value={service} onChange={(event) => setService(event.target.value as ServiceType)}>
            <Radio value="gachalog">{t('compensation.form.serviceGachalog')}</Radio>
            <Radio value="cdn">{t('compensation.form.serviceCdn')}</Radio>
          </RadioGroup>
        </Field>

        <Field label={t('compensation.form.target')}>
          <RadioGroup value={target} onChange={(event) => setTarget(event.target.value as Target)}>
            <Radio value="all">{t('compensation.form.targetAll')}</Radio>
            <Radio value="user">{t('compensation.form.targetUser')}</Radio>
          </RadioGroup>
        </Field>

        <Field label={t('compensation.form.days')}>
          <InputNumber value={days} min={-3650} max={3650} step={1} style={{ width: 200 }} onChange={(value) => setDays(Number(value))} />
        </Field>

        {target === 'user' && (
          <Field label={t('compensation.form.userEmail')}>
            <Input
              value={userName}
              placeholder={t('compensation.form.userEmailPlaceholder')}
              showClear
              style={{ width: '100%' }}
              onChange={setUserName}
              onEnterPress={onSubmit}
            />
          </Field>
        )}
      </div>

      <div className="mt-4">
        <Button theme="solid" type="primary" loading={submitting} onClick={onSubmit}>
          {t('compensation.form.submit')}
        </Button>
      </div>

      {result && <Banner className="mt-4" type="success" closeIcon={null} description={result} />}
    </Card>
  );
}
