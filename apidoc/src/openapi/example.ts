import type { RawDocument, RawSchema } from './types';

function resolve(ref: string, doc: RawDocument): RawSchema {
  if (!ref.startsWith('#/')) {
    return {};
  }
  let node: unknown = doc;
  for (const part of ref.slice(2).split('/')) {
    if (!node || typeof node !== 'object') {
      return {};
    }
    node = (node as Record<string, unknown>)[part.replace(/~1/g, '/').replace(/~0/g, '~')];
  }
  return (node ?? {}) as RawSchema;
}

const FORMAT_SAMPLE: Record<string, unknown> = {
  date: '2024-01-01',
  'date-time': '2024-01-01T08:00:00Z',
  uuid: '00000000-0000-0000-0000-000000000000',
  email: 'user@example.com',
  uri: 'https://example.com',
  'ipv4': '127.0.0.1',
  int32: 0,
  int64: 0,
};

function scalar(schema: RawSchema): unknown {
  if (schema.example !== undefined) {
    return schema.example;
  }
  if (schema.default !== undefined) {
    return schema.default;
  }
  if (schema.enum?.length) {
    return schema.enum[0];
  }
  if (schema.format && schema.format in FORMAT_SAMPLE) {
    return FORMAT_SAMPLE[schema.format];
  }
  switch (schema.type) {
    case 'integer':
    case 'number':
      return 0;
    case 'boolean':
      return false;
    case 'string':
      return 'string';
    default:
      return null;
  }
}

/** 按 schema 生成可直接提交的 JSON 示例，$ref 回环用 seen 截断 */
export function buildExample(schema: RawSchema, doc: RawDocument, seen: string[] = []): unknown {
  if (schema.$ref) {
    const name = decodeURIComponent(schema.$ref.split('/').pop() ?? '');
    if (seen.includes(name)) {
      return {};
    }
    return buildExample(resolve(schema.$ref, doc), doc, [...seen, name]);
  }
  if (schema.example !== undefined) {
    return schema.example;
  }
  if (schema.allOf?.length) {
    return schema.allOf.reduce<Record<string, unknown>>(
      (acc, part) => Object.assign(acc, buildExample(part, doc, seen) as object),
      {},
    );
  }
  if (schema.oneOf?.length) {
    return buildExample(schema.oneOf[0], doc, seen);
  }
  if (schema.anyOf?.length) {
    return buildExample(schema.anyOf[0], doc, seen);
  }
  if (schema.type === 'array' || schema.items) {
    return [buildExample(schema.items ?? {}, doc, seen)];
  }
  if (schema.properties) {
    const out: Record<string, unknown> = {};
    for (const [key, child] of Object.entries(schema.properties)) {
      out[key] = buildExample(child, doc, seen);
    }
    return out;
  }
  if (schema.type === 'object') {
    return {};
  }
  return scalar(schema);
}

export function stringifyExample(value: unknown): string {
  return JSON.stringify(value, null, 2);
}
