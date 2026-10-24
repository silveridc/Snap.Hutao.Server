<script setup lang="ts">
import { DialogPlugin, MessagePlugin } from 'tdesign-vue-next';
import { computed, onMounted, reactive, ref } from 'vue';

import {
  getMetadataDocuments,
  getMetadataKinds,
  getMetadataLeaves,
  getMetadataTranslations,
  removeMetadataTranslation,
  setMetadataTranslation,
} from '@/api/service';
import { t } from '@/locales';

// 上游 metadata 的语言目录名，大小写敏感
const LOCALES = ['CHS', 'CHT', 'DE', 'EN', 'ES', 'FR', 'ID', 'IT', 'JP', 'KR', 'PT', 'RU', 'TH', 'TR', 'VI'];

interface DocRow {
  Name: string;
  Size: number;
  Hash: string;
  UpdatedAt: string;
}

interface OverrideRow {
  Locale: string;
  Name: string;
  Pointer: string;
  Value: string;
  OriginalValue: string;
  UpdatedAt: string;
}

interface LeafRow {
  Pointer: string;
  BaselineValue: string;
  LocalValue: string;
  Differs: boolean;
}

interface KindRow {
  Kind: string;
  DocumentName: string;
  RootShape: number;
  ObjectCount: number;
  LocaleCount: number;
  RawCount: number;
  ProjectedAt: string;
  StaleSince?: string;
}

const fmtSize = (bytes?: number) => (bytes === undefined ? '' : bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(2)} MiB` : `${(bytes / 1024).toFixed(1)} KiB`);
const fmtTime = (raw?: string) => (raw ? raw.replace('T', ' ').slice(0, 19) : '');

const docLocale = ref('CHS');
const docKeyword = ref('');
const docLoading = ref(false);
const docs = ref<DocRow[]>([]);
const docPage = reactive({ current: 1, pageSize: 20 });

const filteredDocs = computed(() => {
  const keyword = docKeyword.value.trim().toLowerCase();
  return keyword ? docs.value.filter((row) => row.Name.toLowerCase().includes(keyword)) : docs.value;
});
const docColumns = computed(() => [
  { colKey: 'Name', title: t('pages.metadata.docs.colName'), minWidth: 260, ellipsis: true },
  { colKey: 'Size', title: t('pages.metadata.docs.colSize'), width: 110 },
  { colKey: 'Hash', title: t('pages.metadata.docs.colHash'), width: 160 },
  { colKey: 'UpdatedAt', title: t('pages.metadata.docs.colUpdatedAt'), width: 180 },
  { colKey: 'op', title: t('pages.metadata.docs.colOp'), width: 120, fixed: 'right' },
]);

async function loadDocs() {
  docLoading.value = true;
  try {
    const res: any = await getMetadataDocuments(docLocale.value);
    docs.value = res.data ?? [];
    docPage.current = 1;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.metadata.messages.loadDocsFail'));
  } finally {
    docLoading.value = false;
  }
}

const kindLoading = ref(false);
const kinds = ref<KindRow[]>([]);
const kindKeyword = ref('');
const kindOnlyStale = ref(false);
const kindPage = reactive({ current: 1, pageSize: 20 });

const staleKinds = computed(() => kinds.value.filter((row) => row.StaleSince).length);
const filteredKinds = computed(() => {
  const keyword = kindKeyword.value.trim().toLowerCase();
  return kinds.value.filter((row) => {
    if (kindOnlyStale.value && !row.StaleSince) {
      return false;
    }
    return !keyword || `${row.Kind} ${row.DocumentName}`.toLowerCase().includes(keyword);
  });
});
const kindColumns = computed(() => [
  { colKey: 'Kind', title: t('pages.metadata.projection.colKind'), width: 160, ellipsis: true },
  { colKey: 'DocumentName', title: t('pages.metadata.projection.colDoc'), width: 160, ellipsis: true },
  { colKey: 'RootShape', title: t('pages.metadata.projection.colShape'), width: 90 },
  { colKey: 'ObjectCount', title: t('pages.metadata.projection.colObjects'), width: 100 },
  { colKey: 'LocaleCount', title: t('pages.metadata.projection.colLocales'), width: 80 },
  { colKey: 'RawCount', title: t('pages.metadata.projection.colRaw'), width: 90 },
  { colKey: 'ProjectedAt', title: t('pages.metadata.projection.colProjectedAt'), width: 180 },
  { colKey: 'StaleSince', title: t('pages.metadata.projection.colState'), width: 200 },
]);

async function loadKinds() {
  kindLoading.value = true;
  try {
    const res: any = await getMetadataKinds();
    kinds.value = res.data ?? [];
    kindPage.current = 1;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.metadata.messages.loadKindsFail'));
  } finally {
    kindLoading.value = false;
  }
}

const overrideLocale = ref('EN');
const overrideName = ref('');
const overrideLoading = ref(false);
const overrides = ref<OverrideRow[]>([]);
const overridePage = reactive({ current: 1, pageSize: 20 });

const filteredOverrides = computed(() => {
  const name = overrideName.value.trim();
  return name ? overrides.value.filter((row) => row.Name === name || row.Name.includes(name)) : overrides.value;
});
const overrideColumns = computed(() => [
  { colKey: 'Locale', title: t('pages.metadata.overrides.colLocale'), width: 80 },
  { colKey: 'Name', title: t('pages.metadata.overrides.colName'), width: 180, ellipsis: true },
  { colKey: 'Pointer', title: t('pages.metadata.overrides.colPointer'), width: 200, ellipsis: true },
  { colKey: 'OriginalValue', title: t('pages.metadata.overrides.colOriginal'), minWidth: 180, ellipsis: true },
  { colKey: 'Value', title: t('pages.metadata.overrides.colValue'), minWidth: 180, ellipsis: true },
  { colKey: 'op', title: t('pages.metadata.docs.colOp'), width: 100, fixed: 'right' },
]);

async function loadOverrides() {
  overrideLoading.value = true;
  try {
    const res: any = await getMetadataTranslations(overrideLocale.value, overrideName.value.trim() || undefined);
    overrides.value = res.data ?? [];
    overridePage.current = 1;
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.metadata.messages.loadOverridesFail'));
  } finally {
    overrideLoading.value = false;
  }
}

const leafLocale = ref('EN');
const leafBaseline = ref('CHS');
const leafName = ref('');
const leafKeyword = ref('');
const leafOnlyDiff = ref(true);
const leafLoading = ref(false);
const leaves = ref<LeafRow[]>([]);
const leafPage = reactive({ current: 1, pageSize: 50 });
const drafts = ref<Record<string, string>>({});
const saving = ref('');

// 当前文档已存在的覆盖，用来标记行内「已覆盖」并允许还原
const overridden = computed(() => new Set(overrides.value.filter((row) => row.Locale === leafLocale.value && row.Name === leafName.value).map((row) => row.Pointer)));

const filteredLeaves = computed(() => {
  const keyword = leafKeyword.value.trim().toLowerCase();
  return leaves.value.filter((leaf) => {
    if (leafOnlyDiff.value && !leaf.Differs) {
      return false;
    }
    if (!keyword) {
      return true;
    }
    return `${leaf.Pointer} ${leaf.BaselineValue ?? ''} ${leaf.LocalValue ?? ''}`.toLowerCase().includes(keyword);
  });
});
const leafColumns = computed(() => [
  { colKey: 'Pointer', title: t('pages.metadata.leaves.colPointer'), width: 220, ellipsis: true },
  { colKey: 'BaselineValue', title: t('pages.metadata.leaves.colBaseline', { baseline: leafBaseline.value }), minWidth: 220 },
  { colKey: 'LocalValue', title: t('pages.metadata.leaves.colLocal', { locale: leafLocale.value }), minWidth: 220 },
  { colKey: 'op', title: t('pages.metadata.docs.colOp'), width: 200, fixed: 'right' },
]);

async function loadLeaves() {
  if (!leafName.value.trim()) {
    MessagePlugin.warning(t('pages.metadata.messages.nameRequired'));
    return;
  }
  leafLoading.value = true;
  drafts.value = {};
  try {
    const res: any = await getMetadataLeaves(leafName.value.trim(), leafLocale.value, leafBaseline.value);
    leaves.value = (res.data ?? []).map((leaf: any) => ({
      Pointer: leaf.Pointer,
      BaselineValue: leaf.BaselineValue ?? '',
      LocalValue: leaf.LocalValue ?? '',
      Differs: leaf.Differs === true,
    }));
    leafPage.current = 1;
  } catch (err) {
    leaves.value = [];
    MessagePlugin.error((err as Error).message || t('pages.metadata.messages.loadLeavesFail'));
  } finally {
    leafLoading.value = false;
  }
}

function editDoc(row: DocRow) {
  leafLocale.value = docLocale.value;
  leafName.value = row.Name;
  leafOnlyDiff.value = false;
  loadLeaves();
}

function draftValue(leaf?: LeafRow) {
  return leaf && Object.prototype.hasOwnProperty.call(drafts.value, leaf.Pointer) ? drafts.value[leaf.Pointer] : leaf?.LocalValue ?? '';
}

function onDraft(leaf: LeafRow | undefined, value: string) {
  if (leaf) {
    drafts.value = { ...drafts.value, [leaf.Pointer]: value };
  }
}

async function saveLeaf(leaf: LeafRow) {
  const value = draftValue(leaf);
  if (value === leaf.LocalValue) {
    MessagePlugin.info(t('pages.metadata.messages.unchanged'));
    return;
  }
  saving.value = leaf.Pointer;
  try {
    const res: any = await setMetadataTranslation(leafLocale.value, leafName.value.trim(), leaf.Pointer, value);
    MessagePlugin.success(res.message || t('pages.metadata.messages.setSuccess', { hash: res.data?.Hash ?? '' }));
    const next = { ...drafts.value };
    delete next[leaf.Pointer];
    drafts.value = next;
    await Promise.all([loadLeaves(), loadOverrides(), loadDocs(), loadKinds()]);
  } catch (err) {
    MessagePlugin.error((err as Error).message || t('pages.metadata.messages.setFail'));
  } finally {
    saving.value = '';
  }
}

function revertLeaf(leaf: LeafRow) {
  confirmRemove(leafLocale.value, leafName.value.trim(), leaf.Pointer);
}

function confirmRemove(locale: string, name: string, pointer: string) {
  const dialog = DialogPlugin.confirm({
    header: t('pages.metadata.dialog.removeHeader'),
    body: t('pages.metadata.dialog.removeBody', { locale, name, pointer }),
    theme: 'warning',
    confirmBtn: t('pages.metadata.dialog.removeConfirm'),
    onConfirm: async () => {
      dialog.hide();
      try {
        const res: any = await removeMetadataTranslation(locale, name, pointer);
        MessagePlugin.success(res.message || t('pages.metadata.messages.removeSuccess'));
        await Promise.all([loadOverrides(), loadLeaves(), loadDocs(), loadKinds()]);
      } catch (err) {
        MessagePlugin.error((err as Error).message || t('pages.metadata.messages.removeFail'));
      }
    },
  });
}

onMounted(() => {
  loadDocs();
  loadOverrides();
  loadKinds();
});
</script>

<template>
  <div class="hutao-page">
    <t-card :title="$t('pages.metadata.projection.title')" :bordered="false" class="hutao-card">
      <p class="tip">{{ $t('pages.metadata.projection.tip') }}</p>
      <t-space size="8px" class="toolbar">
        <t-input v-model="kindKeyword" :placeholder="$t('pages.metadata.projection.searchPlaceholder')" style="width: 260px" clearable />
        <t-checkbox v-model="kindOnlyStale">{{ $t('pages.metadata.projection.onlyStale') }}</t-checkbox>
        <t-button theme="primary" :loading="kindLoading" @click="loadKinds">{{ $t('pages.metadata.docs.refresh') }}</t-button>
        <span class="count">{{ $t('pages.metadata.projection.staleCount', { count: staleKinds }) }}</span>
      </t-space>
      <t-table
        row-key="Kind"
        :data="filteredKinds"
        :columns="kindColumns"
        :loading="kindLoading"
        max-height="420"
        size="small"
        :pagination="{ current: kindPage.current, pageSize: kindPage.pageSize, total: filteredKinds.length }"
        @page-change="(info: any) => ((kindPage.current = info.current), (kindPage.pageSize = info.pageSize))"
      >
        <template #RootShape="{ row }">{{ row?.RootShape === 1 ? $t('pages.metadata.projection.shapeScattered') : $t('pages.metadata.projection.shapeArray') }}</template>
        <template #ProjectedAt="{ row }">{{ fmtTime(row?.ProjectedAt) }}</template>
        <template #StaleSince="{ row }">
          <t-tag v-if="row?.StaleSince" theme="warning" size="small" variant="light">
            {{ $t('pages.metadata.projection.stale', { time: fmtTime(row.StaleSince) }) }}
          </t-tag>
          <t-tag v-else theme="success" size="small" variant="light">{{ $t('pages.metadata.projection.fresh') }}</t-tag>
        </template>
      </t-table>
    </t-card>

    <t-card :title="$t('pages.metadata.leaves.title')" :bordered="false" class="hutao-card">
      <p class="tip">{{ $t('pages.metadata.leaves.tip') }}</p>
      <t-space break-line size="8px" class="toolbar">
        <t-select v-model="leafLocale" :placeholder="$t('pages.metadata.leaves.locale')" style="width: 120px">
          <t-option v-for="l in LOCALES" :key="l" :value="l" :label="l" />
        </t-select>
        <span class="against">{{ $t('pages.metadata.leaves.against') }}</span>
        <t-select v-model="leafBaseline" style="width: 120px">
          <t-option v-for="l in LOCALES" :key="l" :value="l" :label="l" />
        </t-select>
        <t-input v-model="leafName" :placeholder="$t('pages.metadata.leaves.namePlaceholder')" style="width: 260px" @enter="loadLeaves" />
        <t-input v-model="leafKeyword" :placeholder="$t('pages.metadata.leaves.searchPlaceholder')" style="width: 220px" clearable />
        <t-checkbox v-model="leafOnlyDiff">{{ $t('pages.metadata.leaves.onlyDiff') }}</t-checkbox>
        <t-button theme="primary" :loading="leafLoading" @click="loadLeaves">{{ $t('pages.metadata.leaves.load') }}</t-button>
      </t-space>
      <t-table
        row-key="Pointer"
        :data="filteredLeaves"
        :columns="leafColumns"
        :loading="leafLoading"
        max-height="480"
        size="small"
        :pagination="{ current: leafPage.current, pageSize: leafPage.pageSize, total: filteredLeaves.length }"
        @page-change="(info: any) => ((leafPage.current = info.current), (leafPage.pageSize = info.pageSize))"
      >
        <template #BaselineValue="{ row }">
          <div class="cell-text">{{ row?.BaselineValue }}</div>
        </template>
        <template #LocalValue="{ row }">
          <t-input :value="draftValue(row)" :placeholder="row?.LocalValue" size="small" @change="(v: string) => onDraft(row, v)" />
        </template>
        <template #op="{ row }">
          <t-space size="4px">
            <t-button
              theme="primary"
              variant="outline"
              size="small"
              :loading="saving === row?.Pointer"
              :disabled="draftValue(row) === row?.LocalValue"
              @click="saveLeaf(row)"
            >
              {{ $t('pages.metadata.leaves.save') }}
            </t-button>
            <t-button v-if="overridden.has(row?.Pointer)" theme="default" variant="text" size="small" @click="revertLeaf(row)">
              {{ $t('pages.metadata.leaves.revert') }}
            </t-button>
            <t-tag v-else-if="row.Differs" size="small" variant="light">{{ $t('pages.metadata.leaves.sourceDiff') }}</t-tag>
          </t-space>
        </template>
      </t-table>
    </t-card>

    <t-card :title="$t('pages.metadata.overrides.title')" :bordered="false" class="hutao-card">
      <p class="tip">{{ $t('pages.metadata.overrides.tip') }}</p>
      <t-space size="8px" class="toolbar">
        <t-select v-model="overrideLocale" style="width: 120px">
          <t-option v-for="l in LOCALES" :key="l" :value="l" :label="l" />
        </t-select>
        <t-input v-model="overrideName" :placeholder="$t('pages.metadata.overrides.namePlaceholder')" style="width: 260px" clearable @enter="loadOverrides" />
        <t-button theme="primary" :loading="overrideLoading" @click="loadOverrides">{{ $t('pages.metadata.docs.refresh') }}</t-button>
      </t-space>
      <t-table
        row-key="Pointer"
        :data="filteredOverrides"
        :columns="overrideColumns"
        :loading="overrideLoading"
        max-height="420"
        size="small"
        :pagination="{ current: overridePage.current, pageSize: overridePage.pageSize, total: filteredOverrides.length }"
        @page-change="(info: any) => ((overridePage.current = info.current), (overridePage.pageSize = info.pageSize))"
      >
        <template #op="{ row }">
          <t-button theme="danger" variant="text" size="small" @click="confirmRemove(row.Locale, row.Name, row.Pointer)">
            {{ $t('pages.metadata.overrides.revert') }}
          </t-button>
        </template>
      </t-table>
    </t-card>

    <t-card :title="$t('pages.metadata.docs.title')" :bordered="false" class="hutao-card">
      <p class="tip">{{ $t('pages.metadata.docs.tip') }}</p>
      <t-space size="8px" class="toolbar">
        <t-select v-model="docLocale" style="width: 120px" @change="loadDocs">
          <t-option v-for="l in LOCALES" :key="l" :value="l" :label="l" />
        </t-select>
        <t-input v-model="docKeyword" :placeholder="$t('pages.metadata.docs.searchPlaceholder')" style="width: 260px" clearable />
        <t-button theme="primary" :loading="docLoading" @click="loadDocs">{{ $t('pages.metadata.docs.refresh') }}</t-button>
        <span class="count">{{ $t('pages.metadata.docs.count', { count: filteredDocs.length }) }}</span>
      </t-space>
      <t-table
        row-key="Name"
        :data="filteredDocs"
        :columns="docColumns"
        :loading="docLoading"
        max-height="480"
        size="small"
        :pagination="{ current: docPage.current, pageSize: docPage.pageSize, total: filteredDocs.length }"
        @page-change="(info: any) => ((docPage.current = info.current), (docPage.pageSize = info.pageSize))"
      >
        <template #Size="{ row }">{{ fmtSize(row?.Size) }}</template>
        <template #UpdatedAt="{ row }">{{ fmtTime(row?.UpdatedAt) }}</template>
        <template #op="{ row }">
          <t-button theme="primary" variant="text" size="small" @click="editDoc(row)">{{ $t('pages.metadata.docs.edit') }}</t-button>
        </template>
      </t-table>
    </t-card>
  </div>
</template>

<style lang="less" scoped>
.hutao-card {
  margin-bottom: 16px;
}
.tip {
  margin-bottom: 12px;
  color: var(--td-text-color-secondary);
  font-size: 13px;
}
.toolbar {
  margin-bottom: 16px;
}
.against,
.count {
  color: var(--td-text-color-secondary);
  font-size: 13px;
}
.cell-text {
  white-space: pre-wrap;
  word-break: break-all;
  font-size: 13px;
}
</style>
