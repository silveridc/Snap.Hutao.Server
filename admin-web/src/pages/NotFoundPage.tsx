import { useNavigate } from 'react-router';

import { IconArrowLeft } from '@douyinfe/semi-icons';
import { Button, Empty } from '@douyinfe/semi-ui';

import { t, useLocale } from '@/i18n';

export default function NotFoundPage() {
  const navigate = useNavigate();
  useLocale();

  return (
    <div className="flex min-h-[60vh] items-center justify-center">
      <div className="text-center">
        <Empty title={t('common.notFound')} description={t('common.notFoundTip')} />
        <Button className="mt-4" icon={<IconArrowLeft />} onClick={() => navigate('/')}>
          {t('common.backHome')}
        </Button>
      </div>
    </div>
  );
}
