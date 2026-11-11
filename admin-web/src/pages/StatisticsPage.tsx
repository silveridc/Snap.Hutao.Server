import { useState } from 'react';

import { Banner, Button, Card, Modal, Toast, Typography } from '@douyinfe/semi-ui';

import { runStatistics } from '@/api/service';
import { t, useLocale } from '@/i18n';

export default function StatisticsPage() {
  useLocale();

  const [loading, setLoading] = useState(false);
  const [lastResult, setLastResult] = useState('');

  async function run() {
    setLoading(true);
    setLastResult('');
    try {
      const message = await runStatistics();
      setLastResult(message || t('common.success'));
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  function confirmRun() {
    Modal.confirm({
      title: t('statistics.run.dialogHeader'),
      content: t('statistics.run.dialogBody'),
      okText: t('statistics.run.dialogConfirm'),
      cancelText: t('common.cancel'),
      onOk: run,
    });
  }

  return (
    <div className="space-y-4">
      <Card title={t('statistics.run.title')}>
        <Typography.Text type="tertiary" className="mb-4 block">
          {t('statistics.run.tipPre')} <code>GET /Service/Statistics/Run</code>
          {t('statistics.run.tipSuf')}
        </Typography.Text>
        <Button theme="solid" type="primary" loading={loading} onClick={confirmRun}>
          {t('statistics.run.triggerBtn')}
        </Button>
        {lastResult && <Banner type="success" closeIcon={null} description={lastResult} className="mt-3" />}
      </Card>
    </div>
  );
}
