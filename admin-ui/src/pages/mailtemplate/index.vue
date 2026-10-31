<script setup lang="ts">
import Editor from '@tinymce/tinymce-vue';
import { MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, ref, watch } from 'vue';

import { getMailTemplate, resetMailTemplate, saveMailTemplate } from '@/api/service';
import { t } from '@/locales';

interface MailTemplatePayload {
  Template: string;
  DefaultTemplate: string;
  Customized: boolean;
}

const TINYMCE_BASE = '/admin/tinymce';
const PLACEHOLDERS = ['{{options.Title}}', '{{options.RawContent}}', '{{options.Footer}}'];

const loading = ref(false);
const saving = ref(false);
const resetting = ref(false);
const customized = ref(false);
const advanced = ref(false);
const defaultHtml = ref('');
const fullHtml = ref('');
const bodyHtml = ref('');

const tinymceInit = computed(() => ({
  height: 640,
  menubar: false,
  branding: false,
  promotion: false,
  plugins: 'code preview searchreplace link image lists table',
  toolbar:
    'undo redo | blocks | bold italic underline | alignleft aligncenter alignright | bullist numlist | link image table | code preview',
  convert_urls: false,
  entity_encoding: 'raw',
  // 占位符原样保留，不要让 TinyMCE 当成普通文本改写
  protect: [/\{\{options\.(Title|RawContent|Footer)\}\}/g],
  content_style: `${extractCss(fullHtml.value)}\nbody { background: #fff; padding: 24px; }`,
}));

function extractCss(html: string) {
  return [...html.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/gi)].map((match) => match[1]).join('\n');
}

function splitBody(html: string) {
  const match = html.match(/<body([^>]*)>([\s\S]*)<\/body>/i);
  return match ? { attrs: match[1], body: match[2] } : { attrs: '', body: html };
}

function composeHtml(body: string) {
  return fullHtml.value.replace(/(<body[^>]*>)([\s\S]*)(<\/body>)/i, (_match, open: string, _old: string, close: string) => `${open}${body}${close}`);
}

function applyPayload(payload: MailTemplatePayload) {
  customized.value = payload.Customized;
  defaultHtml.value = payload.DefaultTemplate;
  fullHtml.value = payload.Template;
  bodyHtml.value = splitBody(payload.Template).body;
}

async function load() {
  loading.value = true;
  try {
    const res: any = await getMailTemplate();
    applyPayload(res.data);
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.mailtemplate.messages.loadFail'));
  } finally {
    loading.value = false;
  }
}

function loadDefaultIntoEditor() {
  fullHtml.value = defaultHtml.value;
  bodyHtml.value = splitBody(defaultHtml.value).body;
  MessagePlugin.info(t('pages.mailtemplate.messages.loadDefaultSuccess'));
}

async function save() {
  const html = (advanced.value ? fullHtml.value : composeHtml(bodyHtml.value)).trim();
  const missing = PLACEHOLDERS.find((placeholder) => !html.includes(placeholder));
  if (missing) {
    MessagePlugin.warning(t('pages.mailtemplate.messages.missingPlaceholder', { placeholder: missing }));
    return;
  }

  saving.value = true;
  try {
    const res: any = await saveMailTemplate(html);
    MessagePlugin.success(res.message || t('pages.mailtemplate.messages.saveSuccess'));
    await load();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.mailtemplate.messages.saveFail'));
  } finally {
    saving.value = false;
  }
}

async function resetToDefault() {
  resetting.value = true;
  try {
    const res: any = await resetMailTemplate();
    MessagePlugin.success(res.message || t('pages.mailtemplate.messages.resetSuccess'));
    await load();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.mailtemplate.messages.resetFail'));
  } finally {
    resetting.value = false;
  }
}

// 源码模式切换到可视化时刷新编辑器内容，避免两边状态互相覆盖
watch(advanced, (isAdvanced) => {
  if (isAdvanced) {
    fullHtml.value = composeHtml(bodyHtml.value);
  } else {
    bodyHtml.value = splitBody(fullHtml.value).body;
  }
});

onMounted(load);
</script>

<template>
  <div class="hutao-page">
    <t-card :title="$t('pages.mailtemplate.title')" :bordered="false" class="hutao-card">
      <template #subtitle>
        <span class="section-desc">{{ $t('pages.mailtemplate.description') }}</span>
      </template>
      <template #actions>
        <t-space>
          <t-tag :theme="customized ? 'warning' : 'default'" variant="light">
            {{ customized ? $t('pages.mailtemplate.customized') : $t('pages.mailtemplate.default') }}
          </t-tag>
          <span class="advanced-label">{{ $t('pages.mailtemplate.advanced') }}</span>
          <t-switch v-model="advanced" size="small" />
          <t-button variant="outline" size="small" :loading="loading" @click="load">
            {{ $t('pages.mailtemplate.reload') }}
          </t-button>
          <t-button variant="outline" size="small" @click="loadDefaultIntoEditor">
            {{ $t('pages.mailtemplate.loadDefault') }}
          </t-button>
          <t-button theme="primary" size="small" :loading="saving" @click="save">
            {{ $t('pages.mailtemplate.save') }}
          </t-button>
          <t-popconfirm
            :content="$t('pages.mailtemplate.messages.resetConfirm')"
            :confirm-btn="$t('pages.mailtemplate.reset')"
            @confirm="resetToDefault"
          >
            <t-button theme="danger" variant="outline" size="small" :loading="resetting">
              {{ $t('pages.mailtemplate.reset') }}
            </t-button>
          </t-popconfirm>
        </t-space>
      </template>

      <p class="tip">
        {{ $t('pages.mailtemplate.placeholders') }}
        <code>{{ PLACEHOLDERS[0] }}</code>
        <code>{{ PLACEHOLDERS[1] }}</code>
        <code>{{ PLACEHOLDERS[2] }}</code>
      </p>

      <t-loading :loading="loading" size="small" class="editor-wrap">
        <Editor v-if="!advanced" v-model="bodyHtml" :init="tinymceInit" :tinymce-script-src="`${TINYMCE_BASE}/tinymce.min.js`" />
        <t-textarea v-else v-model="fullHtml" :autosize="{ minRows: 20, maxRows: 40 }" class="raw-editor" />
      </t-loading>

      <p class="tip">{{ $t('pages.mailtemplate.hint') }}</p>
    </t-card>
  </div>
</template>

<style lang="less" scoped>
.section-desc {
  color: var(--td-text-color-placeholder);
  font-size: 12px;
}
.advanced-label {
  color: var(--td-text-color-secondary);
  font-size: 12px;
}
.editor-wrap {
  width: 100%;
}
.raw-editor {
  font-family: Consolas, Monaco, monospace;
  font-size: 12px;
}
.tip {
  margin: 12px 0;
  color: var(--td-text-color-placeholder);
  font-size: 12px;
  line-height: 18px;

  code {
    margin-right: 8px;
    padding: 1px 4px;
    border-radius: 3px;
    background: var(--td-bg-color-component);
    font-family: Consolas, Monaco, monospace;
  }
}
</style>
