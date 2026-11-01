import { useState } from 'react';

import { Banner, Button, Card, Input, Modal, Table, Tag, Toast } from '@douyinfe/semi-ui';
import type { ColumnProps } from '@douyinfe/semi-ui/lib/es/table';

import {
  addAdminAccount,
  bindAdminAccount,
  getAdminAccounts,
  removeAdminAccount,
  setAdminAccountEnabled,
  unbindAdminAccount,
} from '@/api/service';
import { useAuth } from '@/auth/AuthProvider';
import { t, useLocale } from '@/i18n';
import { useQuery } from '@/hooks/useQuery';
import { formatDateTime } from '@/lib/format';
import type { AdminAccountItem } from '@/types/api';

export default function AdminAccountPage() {
  useLocale();
  const { user } = useAuth();
  // 服务端按返回码兜底，这里只为体验把写操作按钮置灰
  const canWrite = Boolean(user?.CanWrite);

  const [addVisible, setAddVisible] = useState(false);
  const [adding, setAdding] = useState(false);
  const [addForm, setAddForm] = useState({ subject: '', userName: '', email: '' });

  const [bindRow, setBindRow] = useState<AdminAccountItem | null>(null);
  const [binding, setBinding] = useState(false);
  const [bindUserName, setBindUserName] = useState('');

  const accounts = useQuery(getAdminAccounts, []);

  async function submitAdd() {
    const subject = addForm.subject.trim();
    if (!subject) {
      Toast.warning(t('adminaccount.add.subjectRequired'));
      return;
    }
    setAdding(true);
    try {
      const message = await addAdminAccount(subject, addForm.userName.trim() || null, addForm.email.trim() || null);
      Toast.success(message || t('adminaccount.messages.addSucceed'));
      setAddVisible(false);
      accounts.reload();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setAdding(false);
    }
  }

  async function toggleEnabled(row: AdminAccountItem) {
    try {
      const message = await setAdminAccountEnabled(row.Id, !row.IsEnabled);
      Toast.success(message || t('adminaccount.messages.toggleSucceed'));
      accounts.reload();
    } catch (error) {
      Toast.error((error as Error).message);
    }
  }

  async function submitBind() {
    if (!bindRow) {
      return;
    }
    const userName = bindUserName.trim();
    if (!userName) {
      Toast.warning(t('adminaccount.bind.userNameRequired'));
      return;
    }
    setBinding(true);
    try {
      const message = await bindAdminAccount(bindRow.Id, userName);
      Toast.success(message || t('adminaccount.messages.bindSucceed'));
      setBindRow(null);
      accounts.reload();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setBinding(false);
    }
  }

  function confirmUnbind(row: AdminAccountItem) {
    Modal.confirm({
      title: t('adminaccount.unbind.header'),
      content: t('adminaccount.unbind.body', { subject: row.Subject }),
      okText: t('adminaccount.unbind.confirm'),
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          const message = await unbindAdminAccount(row.Id);
          Toast.success(message || t('adminaccount.messages.unbindSucceed'));
          accounts.reload();
        } catch (error) {
          Toast.error((error as Error).message);
        }
      },
    });
  }

  function confirmRemove(row: AdminAccountItem) {
    Modal.confirm({
      title: t('adminaccount.remove.header'),
      content: t('adminaccount.remove.body', { subject: row.Subject }),
      okText: t('adminaccount.remove.confirm'),
      cancelText: t('common.cancel'),
      onOk: async () => {
        try {
          const message = await removeAdminAccount(row.Id);
          Toast.success(message || t('adminaccount.messages.removeSucceed'));
          accounts.reload();
        } catch (error) {
          Toast.error((error as Error).message);
        }
      },
    });
  }

  const columns: ColumnProps<AdminAccountItem>[] = [
    { title: t('adminaccount.colSubject'), dataIndex: 'Subject', ellipsis: true, minWidth: 200 },
    { title: t('adminaccount.colUserName'), dataIndex: 'UserName', ellipsis: true, width: 150 },
    { title: t('adminaccount.colEmail'), dataIndex: 'Email', ellipsis: true, width: 190, render: (value?: string | null) => value || '—' },
    {
      title: t('adminaccount.colBound'),
      dataIndex: 'BoundUserName',
      ellipsis: true,
      minWidth: 160,
      render: (value?: string | null) => (value ? <span>{value}</span> : <span className="text-[var(--semi-color-text-2)]">{t('adminaccount.unbound')}</span>),
    },
    {
      title: t('adminaccount.colState'),
      dataIndex: 'IsEnabled',
      width: 230,
      render: (_v, row) => (
        <div className="flex flex-wrap gap-1">
          <Tag size="small" type="light" color={row.IsEnabled ? 'green' : 'red'}>
            {row.IsEnabled ? t('adminaccount.enabled') : t('adminaccount.disabled')}
          </Tag>
          {row.IsCurrent && (
            <Tag size="small" type="light" color="blue">
              {t('adminaccount.current')}
            </Tag>
          )}
          {row.IsBootstrap && (
            <Tag size="small" type="light" color="amber">
              {t('adminaccount.bootstrap')}
            </Tag>
          )}
        </div>
      ),
    },
    { title: t('adminaccount.colCreated'), dataIndex: 'CreatedAt', width: 180, render: (value: string) => formatDateTime(value) },
    {
      title: t('adminaccount.colLastLogin'),
      dataIndex: 'LastLoginAt',
      width: 180,
      render: (value?: string | null) => (value ? formatDateTime(value) : t('adminaccount.never')),
    },
    {
      title: t('adminaccount.colOp'),
      key: 'op',
      width: 260,
      fixed: 'right',
      render: (_v, row) => (
        <div className="flex flex-wrap items-center gap-x-1 gap-y-1">
          <Button size="small" theme="borderless" type="primary" disabled={!canWrite || row.IsCurrent} onClick={() => toggleEnabled(row)}>
            {row.IsEnabled ? t('adminaccount.disable') : t('adminaccount.enable')}
          </Button>
          <Button
            size="small"
            theme="borderless"
            type="primary"
            disabled={!canWrite}
            onClick={() => {
              setBindRow(row);
              setBindUserName(row.BoundUserName || '');
            }}
          >
            {row.BoundUserId ? t('adminaccount.bind.rebind') : t('adminaccount.bind.btn')}
          </Button>
          {row.BoundUserId && (
            <Button size="small" theme="borderless" type="warning" disabled={!canWrite} onClick={() => confirmUnbind(row)}>
              {t('adminaccount.unbind.btn')}
            </Button>
          )}
          <Button size="small" theme="borderless" type="danger" disabled={!canWrite || row.IsCurrent} onClick={() => confirmRemove(row)}>
            {t('adminaccount.remove.btn')}
          </Button>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <Card
        title={t('adminaccount.title')}
        headerExtraContent={
          <div className="flex items-center gap-2">
            <Button size="small" type="tertiary" loading={accounts.loading} onClick={accounts.reload}>
              {t('adminaccount.refresh')}
            </Button>
            <Button
              size="small"
              theme="solid"
              disabled={!canWrite}
              onClick={() => {
                setAddForm({ subject: '', userName: '', email: '' });
                setAddVisible(true);
              }}
            >
              {t('adminaccount.add.btn')}
            </Button>
          </div>
        }
      >
        <p className="mb-4 text-[13px] text-[var(--semi-color-text-2)]">{t('adminaccount.tip')}</p>

        {!canWrite && <Banner type="warning" closeIcon={null} description={t('adminaccount.readonly')} className="mb-3" />}

        {accounts.error && <Banner type="danger" closeIcon={null} description={accounts.error.message} className="mb-3" />}

        <Table
          size="small"
          rowKey="Id"
          loading={accounts.loading}
          dataSource={accounts.data ?? []}
          columns={columns}
          pagination={false}
          scroll={{ x: '100%' }}
        />
      </Card>

      <Modal
        title={t('adminaccount.add.header')}
        visible={addVisible}
        okText={t('adminaccount.add.confirm')}
        cancelText={t('common.cancel')}
        confirmLoading={adding}
        onOk={submitAdd}
        onCancel={() => setAddVisible(false)}
      >
        <div className="space-y-3 py-2">
          <div>
            <div className="mb-1 text-[13px]">{t('adminaccount.add.subject')}</div>
            <Input value={addForm.subject} placeholder={t('adminaccount.add.subjectPlaceholder')} onChange={(value) => setAddForm({ ...addForm, subject: value })} />
          </div>
          <div>
            <div className="mb-1 text-[13px]">{t('adminaccount.add.userName')}</div>
            <Input value={addForm.userName} placeholder={t('adminaccount.add.userNamePlaceholder')} onChange={(value) => setAddForm({ ...addForm, userName: value })} />
          </div>
          <div>
            <div className="mb-1 text-[13px]">{t('adminaccount.add.email')}</div>
            <Input value={addForm.email} placeholder={t('adminaccount.add.emailPlaceholder')} onChange={(value) => setAddForm({ ...addForm, email: value })} />
          </div>
        </div>
      </Modal>

      <Modal
        title={t('adminaccount.bind.header')}
        visible={Boolean(bindRow)}
        okText={t('adminaccount.bind.confirm')}
        cancelText={t('common.cancel')}
        confirmLoading={binding}
        onOk={submitBind}
        onCancel={() => setBindRow(null)}
      >
        <p className="mb-3 text-[13px] text-[var(--semi-color-text-2)]">
          {t('adminaccount.bind.body', { subject: bindRow?.Subject ?? '' })}
        </p>
        <div>
          <div className="mb-1 text-[13px]">{t('adminaccount.bind.userName')}</div>
          <Input value={bindUserName} placeholder={t('adminaccount.bind.userNamePlaceholder')} onChange={setBindUserName} />
        </div>
      </Modal>
    </div>
  );
}
