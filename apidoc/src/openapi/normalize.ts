import type {
  ContentBlock,
  Field,
  ModelDef,
  NormalizedDoc,
  Operation,
  ParamItem,
  RawDocument,
  RawMediaType,
  RawOperation,
  RawParameter,
  RawResponse,
  RawSchema,
  ResponseBlock,
  SecurityScheme,
} from './types';

const HTTP_METHODS = ['get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace'];
const UNTAGGED = '未分组';
/** 字段树最深展开层数，超过则只保留模型名，避免深层嵌套把页面撑爆 */
const MAX_DEPTH = 6;

interface RefTarget {
  name: string;
  schema: RawSchema;
}

/** $ref 只支持本地引用：#/components/schemas/X（3.x）与 #/definitions/X（2.0） */
function resolveRef(ref: string, doc: RawDocument): RefTarget | null {
  if (!ref.startsWith('#/')) {
    return null;
  }
  const parts = ref
    .slice(2)
    .split('/')
    .map((p) => p.replace(/~1/g, '/').replace(/~0/g, '~'));
  let node: unknown = doc;
  for (const part of parts) {
    if (!node || typeof node !== 'object' || !(part in (node as Record<string, unknown>))) {
      return null;
    }
    node = (node as Record<string, unknown>)[part];
  }
  const name = decodeURIComponent(parts[parts.length - 1] ?? '');
  return { name, schema: (node ?? {}) as RawSchema };
}

function schemaName(ref: string): string {
  const tail = ref.split('/').pop() ?? '';
  return decodeURIComponent(tail);
}

function typeLabel(schema: RawSchema, doc: RawDocument): string {
  if (schema.$ref) {
    return schemaName(schema.$ref);
  }
  if (schema.oneOf?.length || schema.anyOf?.length) {
    const list = (schema.oneOf ?? schema.anyOf ?? []).map((s) => typeLabel(s, doc));
    return [...new Set(list)].join(' | ') || '混合类型';
  }
  if (schema.allOf?.length) {
    return schema.allOf.map((s) => typeLabel(s, doc)).filter(Boolean).join(' & ') || 'object';
  }
  const type = schema.type ?? (schema.properties ? 'object' : schema.items ? 'array' : '');
  if (type === 'array') {
    const inner = schema.items ? typeLabel(schema.items, doc) : 'any';
    return `${inner.includes(' ') ? `(${inner})` : inner}[]`;
  }
  if (!type) {
    return schema.enum?.length ? 'enum' : 'any';
  }
  if (schema.enum?.length) {
    return `${type}(${schema.enum.map((v) => String(v)).join('|')})`;
  }
  if (schema.format) {
    return `${type}(${schema.format})`;
  }
  return type;
}

function enumStrings(schema: RawSchema): string[] {
  return (schema.enum ?? []).map((v) => String(v));
}

function scalarText(value: unknown): string {
  if (value === undefined || value === null) {
    return '';
  }
  if (typeof value === 'object') {
    return JSON.stringify(value);
  }
  return String(value);
}

/**
 * 把 schema 摊平成字段树。seen 记录当前展开链上的模型名，遇到回环就标成 recursive 并停止，
 * 这样 IdpResponse 里套 IdpResponse 也不会卡死。
 */
function toFields(
  schema: RawSchema,
  doc: RawDocument,
  requiredNames: string[],
  depth: number,
  seen: string[],
  prefix = '',
): Field[] {
  const resolved = schema.$ref ? resolveRef(schema.$ref, doc)?.schema ?? {} : schema;
  const ownRequired = [...requiredNames, ...(resolved.required ?? [])];

  if (resolved.properties) {
    const out: Field[] = [];
    for (const [key, child] of Object.entries(resolved.properties)) {
      const name = prefix ? `${prefix}.${key}` : key;
      const refName = child.$ref ? schemaName(child.$ref) : '';
      const childResolved = child.$ref ? resolveRef(child.$ref, doc)?.schema ?? {} : child;
      const isRecursive = refName !== '' && seen.includes(refName);
      const field: Field = {
        name,
        label: key,
        type: typeLabel(child, doc),
        ref: refName || undefined,
        required: ownRequired.includes(key),
        nullable: Boolean(childResolved.nullable ?? child.nullable),
        description: (childResolved.description ?? child.description ?? '').trim(),
        enumValues: enumStrings(childResolved).length ? enumStrings(childResolved) : enumStrings(child),
        defaultValue: scalarText(childResolved.default ?? child.default),
        example: scalarText(childResolved.example ?? child.example),
        children: [],
        recursive: isRecursive,
      };
      if (!isRecursive && depth < MAX_DEPTH) {
        field.children = toFields(
          childResolved.type === 'array' ? childResolved.items ?? {} : childResolved,
          doc,
          [],
          depth + 1,
          refName ? [...seen, refName] : seen,
        );
      }
      out.push(field);
    }
    return out;
  }

  if (resolved.type === 'array' || resolved.items) {
    const item = resolved.items ?? {};
    const itemResolved = item.$ref ? resolveRef(item.$ref, doc)?.schema ?? {} : item;
    const refName = item.$ref ? schemaName(item.$ref) : '';
    const isRecursive = refName !== '' && seen.includes(refName);
    if (isRecursive || depth >= MAX_DEPTH) {
      return [
        {
          name: prefix ? `${prefix}[]` : '[]',
          label: '[]',
          type: typeLabel(item, doc),
          ref: refName || undefined,
          required: false,
          nullable: false,
          description: '',
          enumValues: [],
          defaultValue: '',
          example: '',
          children: [],
          recursive: isRecursive,
        },
      ];
    }
    return toFields(itemResolved, doc, resolved.required ?? [], depth + 1, refName ? [...seen, refName] : seen);
  }

  return [];
}

function contentBlocks(
  content: Record<string, RawMediaType> | undefined,
  doc: RawDocument,
): { blocks: ContentBlock[]; ref: string } {
  const blocks: ContentBlock[] = [];
  let firstRef = '';
  for (const [contentType, media] of Object.entries(content ?? {})) {
    const schema = media.schema ?? {};
    const resolved = schema.$ref ? resolveRef(schema.$ref, doc)?.schema ?? {} : schema;
    const ref = schema.$ref ? schemaName(schema.$ref) : '';
    if (!firstRef && ref) {
      firstRef = ref;
    }
    blocks.push({
      contentType,
      ref,
      schema: resolved,
      fields: toFields(resolved, doc, resolved.required ?? [], 0, ref ? [ref] : []),
    });
  }
  return { blocks, ref: firstRef };
}

function normalizeParams(list: RawParameter[] | undefined, doc: RawDocument): ParamItem[] {
  return (list ?? []).map((p) => {
    const schema = p.schema ?? {};
    const resolved = schema.$ref ? resolveRef(schema.$ref, doc)?.schema ?? {} : schema;
    const enumValues = enumStrings(resolved).length ? enumStrings(resolved) : (p.enum ?? []).map(String);
    return {
      name: p.name,
      location: p.in,
      required: Boolean(p.required),
      description: (p.description ?? resolved.description ?? '').trim(),
      type: p.in === 'body' ? typeLabel(schema, doc) : typeLabel(schema, doc) || p.type || 'any',
      enumValues,
      defaultValue: scalarText(resolved.default ?? p.default),
      example: scalarText(resolved.example ?? ''),
    };
  });
}

function normalizeResponses(
  responses: Record<string, RawResponse> | undefined,
  doc: RawDocument,
): ResponseBlock[] {
  const out: ResponseBlock[] = [];
  for (const [status, resp] of Object.entries(responses ?? {})) {
    const fromContent = contentBlocks(resp.content, doc);
    let blocks = fromContent.blocks;
    if (!blocks.length && resp.schema) {
      blocks = contentBlocks({ 'application/json': { schema: resp.schema } }, doc).blocks;
    }
    const headers = Object.entries((resp as { headers?: Record<string, RawSchema & { description?: string }> }).headers ?? {}).map(
      ([name, schema]) => ({ name, type: typeLabel(schema, doc), description: (schema.description ?? '').trim() }),
    );
    out.push({ status, description: (resp.description ?? '').trim(), contents: blocks, headers });
  }
  return out;
}

function securitySchemes(doc: RawDocument): SecurityScheme[] {
  const raw = doc.components?.securitySchemes ?? doc.securityDefinitions ?? {};
  return Object.entries(raw).map(([name, scheme]) => ({
    name,
    type: scheme.type ?? '',
    scheme: scheme.scheme ?? '',
    in: scheme.in ?? (scheme.type === 'apiKey' ? 'header' : ''),
    headerName:
      scheme.name ??
      (scheme.type === 'http' && scheme.scheme?.toLowerCase() === 'bearer' ? 'Authorization' : ''),
    bearerHint:
      scheme.type === 'apiKey' &&
      (scheme.name ?? '').toLowerCase() === 'authorization' &&
      /bearer/i.test(scheme.description ?? ''),
    description: (scheme.description ?? '').trim(),
  }));
}

function operationSecurity(op: RawOperation, doc: RawDocument): string[] {
  const list = op.security ?? doc.security ?? [];
  const names = new Set<string>();
  for (const requirement of list) {
    for (const key of Object.keys(requirement)) {
      names.add(key);
    }
  }
  return [...names];
}

function pathTitle(path: string, op: RawOperation, method: string): string {
  if (op.summary?.trim()) {
    return op.summary.trim();
  }
  if (op.operationId?.trim()) {
    return op.operationId.trim();
  }
  const seg = path.split('/').filter(Boolean).pop() ?? method.toUpperCase();
  return seg;
}

function specOrigin(specUrl: string): string {
  if (!specUrl) {
    return '';
  }
  try {
    return new URL(specUrl, globalThis.location?.href ?? 'http://localhost/').origin;
  } catch {
    return '';
  }
}

export function normalizeDocument(raw: RawDocument, specUrl = ''): NormalizedDoc {
  const warnings: string[] = [];
  const operations: Operation[] = [];

  for (const [path, item] of Object.entries(raw.paths ?? {})) {
    const shared = item.parameters ?? [];
    for (const method of HTTP_METHODS) {
      const op = (item as Record<string, RawOperation | undefined>)[method];
      if (!op) {
        continue;
      }
      const params = normalizeParams([...shared, ...(op.parameters ?? [])].filter(Boolean) as RawParameter[], raw);
      const bodyParam = params.find((p) => p.location === 'body' || p.location === 'formData');
      if (bodyParam) {
        warnings.push(`${method.toUpperCase()} ${path} 使用了 ${bodyParam.location} 参数，已按查询参数展示`);
      }
      const request = contentBlocks(op.requestBody?.content, raw);
      operations.push({
        id: `${method} ${path}`,
        docKey: '',
        method: method.toUpperCase(),
        path,
        tag: op.tags?.[0] ?? UNTAGGED,
        summary: pathTitle(path, op, method),
        description: (op.description ?? '').trim(),
        operationId: op.operationId ?? '',
        deprecated: Boolean(op.deprecated),
        params: params.filter((p) => p.location !== 'body'),
        requestContents: request.blocks,
        requestBodyRequired: Boolean(op.requestBody?.required),
        responses: normalizeResponses(op.responses, raw),
        securityNames: operationSecurity(op, raw),
      });
    }
  }

  if (!operations.length) {
    warnings.push('文档里没有找到任何接口');
  }

  const tagMeta = new Map((raw.tags ?? []).map((t) => [t.name, (t.description ?? '').trim()]));
  const groupMap = new Map<string, Operation[]>();
  for (const op of operations) {
    const list = groupMap.get(op.tag) ?? [];
    list.push(op);
    groupMap.set(op.tag, list);
  }
  const orderedTags = [...(raw.tags ?? []).map((t) => t.name)].filter((t) => groupMap.has(t));
  for (const tag of groupMap.keys()) {
    if (!orderedTags.includes(tag)) {
      orderedTags.push(tag);
    }
  }
  const groups = orderedTags.map((tag) => ({ tag, description: tagMeta.get(tag) ?? '', operations: groupMap.get(tag) ?? [] }));

  const schemas = raw.components?.schemas ?? raw.definitions ?? {};
  const usedBy = new Map<string, Set<string>>();
  const collectRefs = (node: unknown, owner: string) => {
    if (!node || typeof node !== 'object') {
      return;
    }
    if (Array.isArray(node)) {
      node.forEach((n) => collectRefs(n, owner));
      return;
    }
    for (const [key, value] of Object.entries(node as Record<string, unknown>)) {
      if (key === '$ref' && typeof value === 'string') {
        const name = schemaName(value);
        const set = usedBy.get(name) ?? new Set<string>();
        set.add(owner);
        usedBy.set(name, set);
      } else {
        collectRefs(value, owner);
      }
    }
  };
  for (const op of operations) {
    collectRefs(
      {
        params: op.params,
        request: Object.fromEntries(op.requestContents.map((c) => [c.contentType, c.schema])),
        responses: Object.fromEntries(op.responses.map((r) => [r.status, r.contents.map((c) => c.schema)])),
      },
      `${op.method} ${op.path}`,
    );
  }
  for (const [name, schema] of Object.entries(schemas)) {
    collectRefs(schema, `model:${name}`);
  }

  const models: ModelDef[] = Object.entries(schemas).map(([name, schema]) => ({
    name,
    type: typeLabel(schema, raw),
    description: (schema.description ?? '').trim(),
    fields: toFields(schema, raw, schema.required ?? [], 0, [name]),
    enumValues: enumStrings(schema),
    usedBy: [...(usedBy.get(name) ?? [])].filter((u) => !u.startsWith('model:')).sort(),
  }));

  const servers = raw.servers?.[0]?.url ?? '';
  const v2Host = raw.host
    ? `${(raw.schemes?.[0] ?? 'https').toLowerCase()}://${raw.host}${raw.basePath ?? ''}`
    : '';
  // Swashbuckle 之类不写 servers，此时接口与 swagger.json 同源，用文档地址的 origin 兜底
  const baseUrl = servers || v2Host || specOrigin(specUrl);

  return {
    meta: {
      title: raw.info?.title ?? '未命名文档',
      version: raw.info?.version ?? '',
      description: (raw.info?.description ?? '').trim(),
      baseUrl: baseUrl.replace(/\/+$/, ''),
      specVersion: raw.openapi ?? raw.swagger ?? '',
    },
    groups,
    operations,
    models,
    securitySchemes: securitySchemes(raw),
    warnings,
  };
}
