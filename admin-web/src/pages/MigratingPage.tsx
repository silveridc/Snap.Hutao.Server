import { useLocation, useNavigate } from 'react-router';

import { IconArrowLeft } from '@douyinfe/semi-icons';
import { Button, Empty, Typography } from '@douyinfe/semi-ui';

import { navItemByPath } from '@/config/nav';
import { t, useLocale } from '@/i18n';

// 页面按批次迁移，未迁到的路由先落到这里，保证导航与地址栏可用
export default function MigratingPage() {
  const navigate = useNavigate();
  const location = useLocation();
  useLocale();

  const item = navItemByPath(location.pathname);
  const title = item ? t(item.navKey) : t('common.appName');

  return (
    <div className="flex min-h-[60vh] items-center justify-center">
      <div className="text-center">
        <Empty title={title} description={t('common.migrating')} />
        <Typography.Text type="tertiary" size="small" className="mt-1 block">
          {t('common.migratingTip')}
        </Typography.Text>
        <Button className="mt-4" icon={<IconArrowLeft />} onClick={() => navigate('/')}>
          {t('common.backHome')}
        </Button>
      </div>
    </div>
  );
}
