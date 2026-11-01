import { useEffect, useRef, useState } from 'react';

import { Editor } from '@tinymce/tinymce-react';
import { Banner, Button, Card, Modal, Switch, Tag, TextArea, Toast } from '@douyinfe/semi-ui';

import { getMailTemplate, resetMailTemplate, saveMailTemplate } from '@/api/service';
import { t, useLocale } from '@/i18n';
import { useQuery } from '@/hooks/useQuery';

const TINYMCE_BASE = '/admin/tinymce';
const PLACEHOLDERS = ['{{options.Title}}', '{{options.RawContent}}', '{{options.Footer}}'];

function extractCss(html: string): string {
  return [...html.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/gi)].map((match) => match[1]).join('\n');
}

function splitBody(html: string): { attrs: string; body: string } {
  const match = html.match(/<body([^>]*)>([\s\S]*)<\/body>/i);
  return match ? { attrs: match[1] ?? '', body: match[2] ?? '' } : { attrs: '', body: html };
}

export default function MailTemplatePage() {
  useLocale();

  const [advanced, setAdvanced] = useState(false);
  const [bodyHtml, setBodyHtml] = useState('');
  const [fullHtml, setFullHtml] = useState('');
  const [saving, setSaving] = useState(false);
  const [resetting, setResetting] = useState(false);
  const editorRef = useRef<Editor | null>(null);
  // 可视化编辑器失焦/源码切换时把内容同步回 state；保存以 state 为准
  const latestBody = useRef('');
  const latestFull = useRef('');
  const initialized = useRef(false);

  const template = useQuery(getMailTemplate, []);
  const payload = template.data;

  useEffect(() => {
    if (!payload || initialized.current) {
      return;
    }
    initialized.current = true;
    setFullHtml(payload.Template);
    setBodyHtml(splitBody(payload.Template).body);
    latestFull.current = payload.Template;
    latestBody.current = splitBody(payload.Template).body;
  }, [payload]);

  function loadDefaultIntoEditor() {
    if (!payload) {
      return;
    }
    setFullHtml(payload.DefaultTemplate);
    setBodyHtml(splitBody(payload.DefaultTemplate).body);
    latestFull.current = payload.DefaultTemplate;
    latestBody.current = splitBody(payload.DefaultTemplate).body;
    Toast.info(t('mailtemplate.messages.loadDefaultSuccess'));
  }

  function switchMode(next: boolean) {
    if (next) {
      const body = editorRef.current ? latestBody.current : bodyHtml;
      setFullHtml(fullHtml.replace(/(<body[^>]*>)([\s\S]*)(<\/body>)/i, (_m, open: string, _old: string, close: string) => `${open}${body}${close}`));
    } else {
      setBodyHtml(splitBody(fullHtml).body);
    }
    setAdvanced(next);
  }

  async function save() {
    const html = (advanced ? latestFull.current : (editorRef.current ? latestBody.current : bodyHtml)).trim();
    const composed = advanced ? html : fullHtml.replace(/(<body[^>]*>)([\s\S]*)(<\/body>)/i, (_m, open: string, _old: string, close: string) => `${open}${html}${close}`);
    const missing = PLACEHOLDERS.find((placeholder) => !composed.includes(placeholder));
    if (missing) {
      Toast.warning(t('mailtemplate.messages.missingPlaceholder', { placeholder: missing }));
      return;
    }

    setSaving(true);
    try {
      const message = await saveMailTemplate(composed);
      Toast.success(message || t('mailtemplate.messages.saveSuccess'));
      initialized.current = false;
      template.reload();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setSaving(false);
    }
  }

  function confirmReset() {
    Modal.confirm({
      title: t('mailtemplate.reset'),
      content: t('mailtemplate.messages.resetConfirm'),
      okText: t('mailtemplate.reset'),
      cancelText: t('common.cancel'),
      onOk: async () => {
        setResetting(true);
        try {
          const message = await resetMailTemplate();
          Toast.success(message || t('mailtemplate.messages.resetSuccess'));
          initialized.current = false;
          template.reload();
        } catch (error) {
          Toast.error((error as Error).message);
        } finally {
          setResetting(false);
        }
      },
    });
  }

  const tinymceInit = {
    height: 640,
    menubar: false,
    branding: false,
    promotion: false,
    plugins: 'code preview searchreplace link image lists table',
    toolbar: 'undo redo | blocks | bold italic underline | alignleft aligncenter alignright | bullist numlist | link image table | code preview',
    convert_urls: false,
    entity_encoding: 'raw' as const,
    // 占位符原样保留，不要让 TinyMCE 当成普通文本改写
    protect: [/\{\{options\.(Title|RawContent|Footer)\}\}/g],
    content_style: `${payload ? extractCss(payload.Template) : ''}\nbody { background: #fff; padding: 24px; }`,
    base_url: TINYMCE_BASE,
  };

  return (
    <div className="space-y-4">
      <Card
        title={t('mailtemplate.title')}
        headerExtraContent={
          <div className="flex items-center gap-3">
            <Tag color={payload?.Customized ? 'amber' : 'grey'} type="light">
              {payload?.Customized ? t('mailtemplate.customized') : t('mailtemplate.default')}
            </Tag>
            <span className="text-xs text-[var(--semi-color-text-2)]">{t('mailtemplate.advanced')}</span>
            <Switch size="small" checked={advanced} onChange={(value) => switchMode(value)} />
            <Button size="small" type="tertiary" loading={template.loading} onClick={template.reload}>
              {t('mailtemplate.reload')}
            </Button>
            <Button size="small" type="tertiary" onClick={loadDefaultIntoEditor}>
              {t('mailtemplate.loadDefault')}
            </Button>
            <Button size="small" theme="solid" loading={saving} onClick={save}>
              {t('mailtemplate.save')}
            </Button>
            <Button size="small" type="danger" theme="light" loading={resetting} onClick={confirmReset}>
              {t('mailtemplate.reset')}
            </Button>
          </div>
        }
      >
        <p className="mb-4 text-[13px] text-[var(--semi-color-text-2)]">{t('mailtemplate.description')}</p>

        <p className="mb-4 text-[13px] text-[var(--semi-color-text-2)]">
          {t('mailtemplate.placeholders')}
          {PLACEHOLDERS.map((placeholder) => (
            <code key={placeholder} className="mx-1 rounded bg-[var(--semi-color-fill-0)] px-1 py-0.5 font-mono text-xs">
              {placeholder}
            </code>
          ))}
        </p>

        {template.error && <Banner type="danger" closeIcon={null} description={template.error.message} className="mb-3" />}

        {payload && (
          <>
            {advanced ? (
              <TextArea
                value={fullHtml}
                autosize={{ minRows: 20, maxRows: 40 }}
                onChange={(value) => {
                  setFullHtml(value);
                  latestFull.current = value;
                }}
                className="font-mono text-xs"
              />
            ) : (
              <Editor
                tinymceScriptSrc={`${TINYMCE_BASE}/tinymce.min.js`}
                onInit={(_evt, editor) => {
                  editorRef.current = editor as never;
                  latestBody.current = editor.getContent();
                }}
                init={tinymceInit}
                initialValue={bodyHtml}
                onEditorChange={(value) => {
                  setBodyHtml(value);
                  latestBody.current = value;
                }}
              />
            )}
          </>
        )}

        <p className="mt-3 text-[13px] text-[var(--semi-color-text-2)]">{t('mailtemplate.hint')}</p>
      </Card>
    </div>
  );
}
