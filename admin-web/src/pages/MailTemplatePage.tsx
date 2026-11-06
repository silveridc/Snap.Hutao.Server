import { useEffect, useRef, useState } from 'react';

import { Editor } from '@tinymce/tinymce-react';
import { Banner, Button, Card, Input, Modal, Select, Switch, Tag, TextArea, Toast } from '@douyinfe/semi-ui';

import { getMailContent, getMailContents, getMailTemplate, resetMailContent, resetMailTemplate, saveMailContent, saveMailTemplate } from '@/api/service';
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

  const [contentKind, setContentKind] = useState('RegisterCode');
  const [contentLanguage, setContentLanguage] = useState('CHS');
  const [contentSubject, setContentSubject] = useState('');
  const [contentTitle, setContentTitle] = useState('');
  const [contentRaw, setContentRaw] = useState('');
  const [contentFooter, setContentFooter] = useState('');
  const [contentSaving, setContentSaving] = useState(false);
  const [contentResetting, setContentResetting] = useState(false);
  const contentEditorRef = useRef<Editor | null>(null);
  const latestRaw = useRef('');
  const contentsQuery = useQuery(getMailContents, []);
  const contents = contentsQuery.data;
  const kindSummary = contents?.find((item) => item.Kind === contentKind);
  const contentQuery = useQuery(() => getMailContent(contentKind, contentLanguage), [contentKind, contentLanguage]);
  const contentPayload = contentQuery.data;
  // 切换种类/语言时旧数据还在，只有与当前选择一致的响应才能进编辑器
  const contentReady = !!contentPayload && contentPayload.Kind === contentKind && contentPayload.Language === contentLanguage;

  // 种类/语言列表来自后端：清单加载后把不在列表里的当前选中项拉回合法值
  useEffect(() => {
    if (!contents) {
      return;
    }
    if (!contents.some((item) => item.Kind === contentKind)) {
      const first = contents[0];
      if (first) {
        setContentKind(first.Kind);
      }
      return;
    }
    // 兼容旧后端：summary 里可能没有 Languages 字段
    if (kindSummary && !(kindSummary.Languages ?? []).includes(contentLanguage) && (kindSummary.Languages?.length ?? 0) > 0) {
      setContentLanguage(kindSummary.Languages?.[0] ?? contentLanguage);
    }
  }, [contents, contentKind, contentLanguage, kindSummary]);

  useEffect(() => {
    if (!contentPayload) {
      return;
    }
    setContentSubject(contentPayload.Subject);
    setContentTitle(contentPayload.Title);
    setContentRaw(contentPayload.RawContent);
    setContentFooter(contentPayload.Footer);
    latestRaw.current = contentPayload.RawContent;
  }, [contentPayload]);

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

  async function saveContent() {
    const subject = contentSubject.trim();
    const title = contentTitle.trim();
    const raw = (contentEditorRef.current ? latestRaw.current : contentRaw).trim();
    const footer = contentFooter.trim();
    if (!subject || !title || !raw || !footer) {
      Toast.warning(t('mailtemplate.content.messages.emptyFields'));
      return;
    }
    const variable = contentPayload?.Variables.find((name) => !raw.includes(`{{${name}}}`));
    if (variable) {
      Toast.warning(t('mailtemplate.content.messages.missingVariable', { variable: `{{${variable}}}` }));
      return;
    }

    setContentSaving(true);
    try {
      const message = await saveMailContent(contentKind, { Language: contentLanguage, Subject: subject, Title: title, RawContent: raw, Footer: footer });
      Toast.success(message || t('mailtemplate.content.messages.saveSuccess'));
      contentQuery.reload();
      contentsQuery.reload();
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setContentSaving(false);
    }
  }

  function confirmContentReset() {
    Modal.confirm({
      title: t('mailtemplate.reset'),
      content: t('mailtemplate.content.messages.resetConfirm'),
      okText: t('mailtemplate.reset'),
      cancelText: t('common.cancel'),
      onOk: async () => {
        setContentResetting(true);
        try {
          const message = await resetMailContent(contentKind, contentLanguage);
          Toast.success(message || t('mailtemplate.content.messages.resetSuccess'));
          contentQuery.reload();
          contentsQuery.reload();
        } catch (error) {
          Toast.error((error as Error).message);
        } finally {
          setContentResetting(false);
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
    // raw 让 {{options.*}} 原样进出；不要用 protect 保护占位符——TinyMCE 会把命中的内容换成
    // 注释节点，可视化模式里既看不见也点不动
    entity_encoding: 'raw' as const,
    content_style: `${payload ? extractCss(payload.Template) : ''}\nbody { background: #fff; padding: 24px; }`,
    base_url: TINYMCE_BASE,
  };

  return (
    <div className="space-y-4">
      <Card
        title={t('mailtemplate.title')}
        headerExtraContent={
          <div className="flex flex-wrap items-center justify-end gap-2">
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

      <Card
        title={t('mailtemplate.content.title')}
        headerExtraContent={
          <div className="flex flex-wrap items-center justify-end gap-2">
            <Tag color={contentReady ? (contentPayload?.Customized ? 'amber' : 'grey') : 'grey'} type="light">
              {contentReady && contentPayload?.Customized ? t('mailtemplate.customized') : t('mailtemplate.default')}
            </Tag>
            <Button size="small" type="tertiary" loading={contentQuery.loading} onClick={contentQuery.reload}>
              {t('mailtemplate.reload')}
            </Button>
            <Button size="small" theme="solid" loading={contentSaving} onClick={saveContent}>
              {t('mailtemplate.save')}
            </Button>
            <Button size="small" type="danger" theme="light" loading={contentResetting} onClick={confirmContentReset}>
              {t('mailtemplate.reset')}
            </Button>
          </div>
        }
      >
        <p className="mb-4 text-[13px] text-[var(--semi-color-text-2)]">{t('mailtemplate.content.description')}</p>
        {contentQuery.error && <Banner type="danger" closeIcon={null} description={contentQuery.error.message} className="mb-3" />}
        {contentsQuery.error && <Banner type="danger" closeIcon={null} description={contentsQuery.error.message} className="mb-3" />}

        <div className="mb-4 flex flex-wrap gap-3">
          <div className="w-72">
            <p className="mb-1 text-[13px] text-[var(--semi-color-text-2)]">{t('mailtemplate.content.kind')}</p>
            <Select
              value={contentKind}
              onChange={(value) => setContentKind(String(value))}
              optionList={(contents ?? []).map((item) => ({ value: item.Kind, label: t(`mailtemplate.kind.${item.Kind}`) }))}
              style={{ width: '100%' }}
              disabled={contentQuery.loading || contentsQuery.loading}
            />
          </div>
          <div className="w-48">
            <p className="mb-1 text-[13px] text-[var(--semi-color-text-2)]">{t('mailtemplate.content.language')}</p>
            <Select
              value={contentLanguage}
              onChange={(value) => setContentLanguage(String(value))}
              optionList={(kindSummary?.Languages ?? []).map((language, index) => ({
                value: language,
                label: (
                  <span className="flex items-center gap-1.5">
                    {t(`mailtemplate.lang.${language}`)}
                    {kindSummary?.Customized[index] && <span className="h-1.5 w-1.5 rounded-full bg-[var(--semi-color-warning)]" />}
                  </span>
                ),
              }))}
              style={{ width: '100%' }}
              disabled={contentQuery.loading || contentsQuery.loading}
            />
          </div>
        </div>

        {contentReady && contentPayload && (
          <>
            <p className="mb-4 text-[13px] text-[var(--semi-color-text-2)]">
              {t('mailtemplate.content.variables')}
              {contentPayload.Variables.map((variable) => (
                <code key={variable} className="mx-1 rounded bg-[var(--semi-color-fill-0)] px-1 py-0.5 font-mono text-xs">
                  {`{{${variable}}}`}
                </code>
              ))}
            </p>

            <div className="mb-4 space-y-3">
              <div>
                <p className="mb-1 text-[13px] text-[var(--semi-color-text-2)]">{t('mailtemplate.content.subject')}</p>
                <Input value={contentSubject} onChange={setContentSubject} />
              </div>
              <div>
                <p className="mb-1 text-[13px] text-[var(--semi-color-text-2)]">{t('mailtemplate.content.contentTitle')}</p>
                <Input value={contentTitle} onChange={setContentTitle} />
              </div>
            </div>

            <p className="mb-1 text-[13px] text-[var(--semi-color-text-2)]">{t('mailtemplate.content.rawContent')}</p>
            <Editor
              key={`${contentKind}-${contentLanguage}`}
              tinymceScriptSrc={`${TINYMCE_BASE}/tinymce.min.js`}
              onInit={(_evt, editor) => {
                contentEditorRef.current = editor as never;
                latestRaw.current = editor.getContent();
              }}
              init={{
                height: 640,
                menubar: false,
                branding: false,
                promotion: false,
                plugins: 'code preview searchreplace link image lists table',
                toolbar: 'undo redo | bold italic underline | alignleft aligncenter alignright | link image | code preview',
                convert_urls: false,
                entity_encoding: 'raw' as const,
                content_style: `${payload ? extractCss(payload.Template) : ''}\nbody { background: #fff; padding: 24px; }`,
                base_url: TINYMCE_BASE,
              }}
              initialValue={contentRaw}
              onEditorChange={(value) => {
                setContentRaw(value);
                latestRaw.current = value;
              }}
            />

            <div className="mt-3">
              <p className="mb-1 text-[13px] text-[var(--semi-color-text-2)]">{t('mailtemplate.content.footer')}</p>
              <Input value={contentFooter} onChange={setContentFooter} />
            </div>
          </>
        )}
      </Card>
    </div>
  );
}
