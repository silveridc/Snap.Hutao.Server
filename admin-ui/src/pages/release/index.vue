<script setup lang="ts">
import { CopyIcon } from 'tdesign-icons-vue-next';
import { DialogPlugin, MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, reactive, ref } from 'vue';

import { activateRelease, deactivateRelease, getReleaseBucketObjects, getReleaseCurrent, getReleaseList, publishRelease, publishReleaseFromBucket } from '@/api/service';
import { t } from '@/locales';

const MIRROR_TYPE_OPTIONS = [
  { label: 'Direct', value: 'Direct' },
  { label: 'Archive', value: 'Archive' },
  { label: 'Browser', value: 'Browser' },
];

const form = reactive({
  Version: '',
  Validation: '',
  MinimumVersion: '',
  ReleaseNotes: '',
  mirrors: [{ MirrorName: '', Url: '', MirrorType: 'Direct' }] as { MirrorName: string; Url: string; MirrorType: string }[],
});
const loading = ref(false);

function addMirror() {
  form.mirrors.push({ MirrorName: '', Url: '', MirrorType: 'Direct' });
}

function removeMirror(index: number) {
  form.mirrors.splice(index, 1);
}

async function submit() {
  if (!form.Version.trim()) {
    MessagePlugin.warning(t('pages.release.messages.versionRequired'));
    return;
  }
  if (!form.Validation.trim()) {
    MessagePlugin.warning(t('pages.release.messages.validationRequired'));
    return;
  }
  if (!form.mirrors.length) {
    MessagePlugin.warning(t('pages.release.messages.mirrorRequired'));
    return;
  }
  for (let i = 0; i < form.mirrors.length; i++) {
    const m = form.mirrors[i];
    if (!m.MirrorName.trim() || !m.Url.trim()) {
      MessagePlugin.warning(t('pages.release.messages.mirrorEmpty', { n: i + 1 }));
      return;
    }
  }
  loading.value = true;
  try {
    const payload = {
      Version: form.Version.trim(),
      Validation: form.Validation.trim(),
      MinimumVersion: form.MinimumVersion.trim() || null,
      Mirrors: form.mirrors.map((m) => ({
        MirrorName: m.MirrorName.trim(),
        Url: m.Url.trim(),
        MirrorType: m.MirrorType,
      })),
      ReleaseNotes: form.ReleaseNotes.trim() || null,
    };
    const res: any = await publishRelease(payload);
    MessagePlugin.success(res.message || t('pages.release.messages.publishSuccess'));
    form.Version = '';
    form.Validation = '';
    form.MinimumVersion = '';
    form.ReleaseNotes = '';
    form.mirrors = [{ MirrorName: '', Url: '', MirrorType: 'Direct' }];
    pagination.current = 1;
    await loadList();
    await loadCurrent();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.release.messages.publishFail'));
  } finally {
    loading.value = false;
  }
}

const listLoading = ref(false);
const rows = ref<any[]>([]);
const pagination = reactive({ current: 1, pageSize: 10, total: 0 });
const currentRelease = ref<any>(null);

const bucket = reactive({
  prefix: '',
  host: '',
  truncated: false,
  objects: [] as { Key: string; Size: number; LastModified: string }[],
  selected: '',
  version: '',
  mirrorName: '',
  minimumVersion: '',
  validation: '',
  releaseNotes: '',
});
const bucketLoading = ref(false);
const bucketPublishing = ref(false);

const bucketOptions = computed(() =>
  bucket.objects.map((object) => ({ label: `${object.Key} · ${fmtSize(object.Size)}`, value: object.Key })),
);

function fmtSize(bytes: number) {
  if (!bytes) return '0 B';
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / 1048576).toFixed(1)} MiB`;
}

function parseVersionFromKey(key: string) {
  const name = key.split('/').pop() ?? '';
  return name.match(/\d+\.\d+\.\d+\.\d+/)?.[0] ?? '';
}

async function loadObjects(silent = false) {
  bucketLoading.value = true;
  try {
    const res: any = await getReleaseBucketObjects(bucket.prefix.trim());
    const data = res.data ?? {};
    bucket.host = data.Host ?? '';
    bucket.truncated = data.Truncated === true;
    bucket.objects = data.Objects ?? [];
    if (bucket.selected && !bucket.objects.some((object) => object.Key === bucket.selected)) {
      bucket.selected = '';
    }
    if (!bucket.mirrorName && bucket.host) {
      bucket.mirrorName = bucket.host;
    }
  } catch (err) {
    bucket.objects = [];
    bucket.selected = '';
    if (!silent) {
      MessagePlugin.error((err as Error).message || t('pages.release.bucket.listFail'));
    }
  } finally {
    bucketLoading.value = false;
  }
}

function onBucketObjectChange(value: string) {
  bucket.selected = value;
  if (!bucket.version.trim()) {
    bucket.version = parseVersionFromKey(value);
  }
}

async function submitFromBucket() {
  if (!bucket.selected) {
    MessagePlugin.warning(t('pages.release.bucket.objectRequired'));
    return;
  }

  const validation = bucket.validation.trim();
  if (validation && !/^[0-9a-fA-F]{64}$/.test(validation)) {
    MessagePlugin.warning(t('pages.release.bucket.validationInvalid'));
    return;
  }

  bucketPublishing.value = true;
  try {
    const payload = {
      Key: bucket.selected,
      Version: bucket.version.trim() || null,
      MirrorName: bucket.mirrorName.trim() || null,
      MinimumVersion: bucket.minimumVersion.trim() || null,
      Validation: validation || null,
      ReleaseNotes: bucket.releaseNotes.trim() || null,
    };
    const res: any = await publishReleaseFromBucket(payload);
    MessagePlugin.success(res.message || t('pages.release.bucket.publishSuccess'));
    bucket.selected = '';
    bucket.version = '';
    bucket.minimumVersion = '';
    bucket.validation = '';
    bucket.releaseNotes = '';
    pagination.current = 1;
    await loadList();
    await loadCurrent();
    await loadObjects();
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.release.bucket.publishFail'));
  } finally {
    bucketPublishing.value = false;
  }
}

const columns = computed(() => [
  { colKey: 'Id', title: 'ID', width: 80 },
  { colKey: 'Version', title: t('pages.release.list.colVersion'), width: 140 },
  { colKey: 'Validation', title: 'SHA-256', ellipsis: true, minWidth: 200 },
  { colKey: 'Mirrors', title: t('pages.release.list.colMirrors'), width: 80 },
  { colKey: 'IsActive', title: t('pages.release.list.colStatus'), width: 90 },
  { colKey: 'PublishedByUserName', title: t('pages.release.list.colPublisher'), width: 120 },
  { colKey: 'PublishedAt', title: t('pages.release.list.colPublishedAt'), width: 190 },
  { colKey: 'op', title: t('pages.release.list.colOp'), width: 100, fixed: 'right' },
]);

function fmtTime(value?: string) {
  if (!value) return '—';
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleString('zh-CN');
}

const HASH_PREVIEW_LENGTH = 12;

function shortHash(value?: string) {
  if (!value) return '—';
  return value.length > HASH_PREVIEW_LENGTH ? `${value.slice(0, HASH_PREVIEW_LENGTH)}…` : value;
}

async function copyHash(value?: string) {
  if (!value) return;
  try {
    await navigator.clipboard.writeText(value);
    MessagePlugin.success(t('pages.release.current.copiedHash'));
  } catch {
    MessagePlugin.error(t('pages.release.current.copyFail'));
  }
}

async function loadCurrent() {
  try {
    const res: any = await getReleaseCurrent();
    currentRelease.value = res.data ?? null;
  } catch {
    currentRelease.value = null;
  }
}

async function loadList() {
  listLoading.value = true;
  try {
    const res: any = await getReleaseList(pagination.current, pagination.pageSize);
    const d = res.data ?? {};
    rows.value = d.Items ?? [];
    pagination.total = d.Total ?? 0;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.release.messages.listFail'));
  } finally {
    listLoading.value = false;
  }
}

function onPageChange(pageInfo: { current: number; pageSize: number }) {
  pagination.current = pageInfo.current;
  pagination.pageSize = pageInfo.pageSize;
  loadList();
}

function activateRow(row: any) {
  if (row.IsActive) {
    MessagePlugin.info(t('pages.release.messages.alreadyActive'));
    return;
  }
  const dialog = DialogPlugin.confirm({
    header: t('pages.release.dialog.activateHeader'),
    body: t('pages.release.dialog.activateConfirmBody', { version: row.Version }),
    theme: 'warning',
    confirmBtn: t('pages.release.actions.activate'),
    onConfirm: async () => {
      dialog.hide();
      try {
        const res: any = await activateRelease({ Id: row.Id });
        MessagePlugin.success(res.message || t('pages.release.messages.activateSuccess'));
        await loadList();
        await loadCurrent();
      } catch (err) {
        MessagePlugin.error((err as Error).message || t('pages.release.messages.activateFail'));
      }
    },
  });
}

function deactivateRow(row: any) {
  const dialog = DialogPlugin.confirm({
    header: t('pages.release.dialog.deactivateHeader'),
    body: t('pages.release.dialog.deactivateConfirmBody', { version: row.Version }),
    theme: 'warning',
    confirmBtn: t('pages.release.actions.deactivate'),
    onConfirm: async () => {
      dialog.hide();
      try {
        const res: any = await deactivateRelease({ Id: row.Id });
        MessagePlugin.success(res.message || t('pages.release.messages.deactivateSuccess'));
        await loadList();
        await loadCurrent();
      } catch (err) {
        MessagePlugin.error((err as Error).message || t('pages.release.messages.deactivateFail'));
      }
    },
  });
}

onMounted(() => {
  loadCurrent();
  loadList();
  loadObjects(true);
});
</script>

<template>
  <div class="hutao-page">
    <t-card v-if="currentRelease" :title="$t('pages.release.current.title')" :bordered="false" class="hutao-card">
      <t-descriptions :column="3" layout="horizontal" size="small">
        <t-descriptions-item :label="$t('pages.release.current.version')">{{ currentRelease.Version }}</t-descriptions-item>
        <t-descriptions-item :label="$t('pages.release.current.publisher')">{{ currentRelease.PublishedByUserName || '—' }}</t-descriptions-item>
        <t-descriptions-item :label="$t('pages.release.current.publishedAt')">{{ fmtTime(currentRelease.PublishedAt) }}</t-descriptions-item>
        <t-descriptions-item label="SHA-256">
          <span class="hash-cell">
            <t-tooltip placement="top-left" :overlay-inner-style="{ maxWidth: '480px' }">
              <template #content>
                <span class="mono hash-full">{{ currentRelease.Validation }}</span>
              </template>
              <span class="mono hash-trigger">{{ shortHash(currentRelease.Validation) }}</span>
            </t-tooltip>
            <t-tooltip :content="$t('pages.release.current.copyHash')" placement="top">
              <t-button class="hash-copy" shape="square" size="small" variant="text" @click="copyHash(currentRelease.Validation)">
                <copy-icon />
              </t-button>
            </t-tooltip>
          </span>
        </t-descriptions-item>
        <t-descriptions-item :label="$t('pages.release.current.mirrors')">{{ currentRelease.Mirrors?.length ?? 0 }}</t-descriptions-item>
        <t-descriptions-item v-if="currentRelease.MinimumVersion" :label="$t('pages.release.form.minimumVersion')">
          {{ currentRelease.MinimumVersion }}
        </t-descriptions-item>
        <t-descriptions-item v-if="currentRelease.ReleaseNotes" :label="$t('pages.release.current.releaseNotes')">{{ currentRelease.ReleaseNotes }}</t-descriptions-item>
      </t-descriptions>
    </t-card>

    <t-card :title="$t('pages.release.bucket.title')" :bordered="false" class="hutao-card">
      <p class="tip">{{ $t('pages.release.bucket.tip') }}</p>
      <t-form label-align="top">
        <t-row :gutter="16">
          <t-col :span="5">
            <t-form-item :label="$t('pages.release.bucket.prefix')">
              <div class="bucket-prefix">
                <t-input
                  v-model="bucket.prefix"
                  :placeholder="$t('pages.release.bucket.prefixPlaceholder')"
                  clearable
                  @enter="loadObjects()"
                />
                <t-button variant="outline" :loading="bucketLoading" @click="loadObjects()">
                  {{ $t('pages.release.bucket.refreshObjects') }}
                </t-button>
              </div>
            </t-form-item>
          </t-col>
          <t-col :span="7">
            <t-form-item :label="$t('pages.release.bucket.object')">
              <t-select
                v-model="bucket.selected"
                :options="bucketOptions"
                :placeholder="$t('pages.release.bucket.objectPlaceholder')"
                :loading="bucketLoading"
                filterable
                clearable
                style="width: 100%"
                @change="onBucketObjectChange"
              />
            </t-form-item>
          </t-col>
          <t-col :span="4">
            <t-form-item :label="$t('pages.release.form.version')" :help="$t('pages.release.bucket.versionAutoHint')">
              <t-input v-model="bucket.version" :placeholder="$t('pages.release.form.versionPlaceholder')" clearable />
            </t-form-item>
          </t-col>
          <t-col :span="4">
            <t-form-item :label="$t('pages.release.form.minimumVersion')" :help="$t('pages.release.form.minimumVersionHint')">
              <t-input v-model="bucket.minimumVersion" :placeholder="$t('pages.release.form.minimumVersionPlaceholder')" clearable />
            </t-form-item>
          </t-col>
          <t-col :span="4">
            <t-form-item :label="$t('pages.release.bucket.mirrorName')" :help="$t('pages.release.bucket.mirrorNameHint')">
              <t-input v-model="bucket.mirrorName" :placeholder="bucket.host" clearable />
            </t-form-item>
          </t-col>
          <t-col :span="12">
            <t-form-item :label="$t('pages.release.bucket.validation')" :help="$t('pages.release.bucket.validationHint')">
              <t-input v-model="bucket.validation" :placeholder="$t('pages.release.bucket.validationPlaceholder')" clearable class="mono" />
            </t-form-item>
          </t-col>
          <t-col :span="12">
            <t-form-item :label="$t('pages.release.form.releaseNotes')">
              <t-textarea v-model="bucket.releaseNotes" :placeholder="$t('pages.release.form.releaseNotesPlaceholder')" :autosize="{ minRows: 2, maxRows: 6 }" />
            </t-form-item>
          </t-col>
          <t-col :span="12">
            <t-form-item>
              <t-button theme="primary" :loading="bucketPublishing" @click="submitFromBucket">
                {{ $t('pages.release.bucket.submit') }}
              </t-button>
              <span v-if="bucketPublishing" class="bucket-hint">
                {{ bucket.validation.trim() ? $t('pages.release.bucket.publishingWithHash') : $t('pages.release.bucket.publishing') }}
              </span>
              <span v-else-if="bucket.truncated" class="bucket-hint">{{ $t('pages.release.bucket.truncated') }}</span>
            </t-form-item>
          </t-col>
        </t-row>
      </t-form>
    </t-card>

    <t-card :title="$t('pages.release.form.title')" :bordered="false" class="hutao-card">
      <p class="tip">
        {{ $t('pages.release.form.tipPre') }}<code>POST /Service/Release/Publish</code>{{ $t('pages.release.form.tipSuf') }}
      </p>
      <t-form label-align="top">
        <t-row :gutter="16">
          <t-col :span="6">
            <t-form-item :label="$t('pages.release.form.version')">
              <t-input v-model="form.Version" :placeholder="$t('pages.release.form.versionPlaceholder')" clearable />
            </t-form-item>
          </t-col>
          <t-col :span="6">
            <t-form-item :label="$t('pages.release.form.validation')">
              <t-input v-model="form.Validation" :placeholder="$t('pages.release.form.validationPlaceholder')" clearable class="mono" />
            </t-form-item>
          </t-col>
          <t-col :span="6">
            <t-form-item :label="$t('pages.release.form.minimumVersion')" :help="$t('pages.release.form.minimumVersionHint')">
              <t-input v-model="form.MinimumVersion" :placeholder="$t('pages.release.form.minimumVersionPlaceholder')" clearable />
            </t-form-item>
          </t-col>
          <t-col :span="12">
            <t-form-item :label="$t('pages.release.form.mirrors')">
              <div class="mirror-list">
                <div v-for="(mirror, idx) in form.mirrors" :key="idx" class="mirror-row">
                  <t-input v-model="mirror.MirrorName" :placeholder="$t('pages.release.form.mirrorNamePlaceholder')" clearable style="width: 160px" />
                  <t-select v-model="mirror.MirrorType" :options="MIRROR_TYPE_OPTIONS" style="width: 120px" />
                  <t-input v-model="mirror.Url" :placeholder="$t('pages.release.form.mirrorUrlPlaceholder')" clearable class="mirror-url" />
                  <t-button theme="danger" variant="outline" :disabled="form.mirrors.length <= 1" @click="removeMirror(idx)">
                    {{ $t('pages.release.form.removeMirror') }}
                  </t-button>
                </div>
              </div>
              <t-button variant="outline" size="small" style="margin-top: 8px" @click="addMirror">{{ $t('pages.release.form.addMirror') }}</t-button>
            </t-form-item>
          </t-col>
          <t-col :span="12">
            <t-form-item :label="$t('pages.release.form.releaseNotes')">
              <t-textarea v-model="form.ReleaseNotes" :placeholder="$t('pages.release.form.releaseNotesPlaceholder')" :autosize="{ minRows: 2, maxRows: 8 }" />
            </t-form-item>
          </t-col>
          <t-col :span="12">
            <t-form-item>
              <t-button theme="primary" :loading="loading" @click="submit">{{ $t('pages.release.form.submit') }}</t-button>
            </t-form-item>
          </t-col>
        </t-row>
      </t-form>
    </t-card>

    <t-card :title="$t('pages.release.list.title')" :bordered="false" class="hutao-card">
      <template #actions>
        <t-button variant="outline" size="small" :loading="listLoading" @click="loadList">{{ $t('pages.release.list.refresh') }}</t-button>
      </template>
      <t-table
        row-key="Id"
        :data="rows"
        :columns="columns"
        :loading="listLoading"
        :pagination="pagination"
        size="small"
        @page-change="onPageChange"
      >
        <template #Validation="{ row }">
          <span class="mono hash">{{ row.Validation }}</span>
        </template>
        <template #Mirrors="{ row }">{{ row.Mirrors?.length ?? 0 }}</template>
        <template #IsActive="{ row }">
          <t-tag v-if="row.IsActive" theme="success" variant="light">{{ $t('pages.release.list.active') }}</t-tag>
          <t-tag v-else theme="default" variant="light">{{ $t('pages.release.list.history') }}</t-tag>
        </template>
        <template #PublishedAt="{ row }">{{ fmtTime(row.PublishedAt) }}</template>
        <template #op="{ row }">
            <t-link v-if="!row.IsActive" theme="primary" hover="color" @click="activateRow(row)">{{ $t('pages.release.actions.activate') }}</t-link>
            <t-link v-else theme="danger" hover="color" @click="deactivateRow(row)">{{ $t('pages.release.actions.deactivate') }}</t-link>
        </template>
      </t-table>
    </t-card>
  </div>
</template>

<style lang="less" scoped>
.hash {
  font-size: 12px;
  color: var(--td-text-color-secondary);
}
.hash-cell {
  display: inline-flex;
  gap: 4px;
  align-items: center;
}
.hash-trigger {
  cursor: help;
}
.hash-full {
  word-break: break-all;
}
.hash-copy {
  margin-left: 2px;
  color: var(--td-text-color-secondary);
}
.mirror-list {
  width: 100%;
}
.mirror-row {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-bottom: 8px;
}
.mirror-url {
  flex: 1;
}
.bucket-prefix {
  display: flex;
  gap: 8px;
  align-items: center;
  width: 100%;
}
.bucket-hint {
  margin-left: 12px;
  font-size: 12px;
  color: var(--td-text-color-secondary);
}
.text-disabled {
  color: var(--td-text-color-disabled);
  font-size: 13px;
}
</style>
