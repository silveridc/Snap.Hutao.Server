import { normalizeDocument } from '../openapi/normalize';
import type { NormalizedDoc, RawDocument } from '../openapi/types';

export interface LoadedSpec {
  doc: NormalizedDoc;
  raw: RawDocument;
}

async function readBody(res: Response): Promise<string> {
  try {
    return await res.text();
  } catch {
    return '';
  }
}

export async function loadSpec(url: string): Promise<LoadedSpec> {
  let res: Response;
  try {
    res = await fetch(url, { cache: 'no-store' });
  } catch (e) {
    throw new Error(
      `无法请求 ${url}：${e instanceof Error ? e.message : '网络错误'}。跨域被拦截时请把文档部署到与接口同源，或给服务端加 CORS。`,
    );
  }
  const text = await readBody(res);
  if (!res.ok) {
    throw new Error(`${url} 返回 HTTP ${res.status}${text ? `：${text.slice(0, 160)}` : ''}`);
  }
  let raw: RawDocument;
  try {
    raw = JSON.parse(text) as RawDocument;
  } catch {
    throw new Error(`${url} 不是合法 JSON（拿到的是 ${text.slice(0, 60) || '空内容'}）`);
  }
  if (!raw.paths && !raw.definitions && !raw.components) {
    throw new Error(`${url} 看起来不是 OpenAPI/Swagger 文档，缺少 paths 字段`);
  }
  return { raw, doc: normalizeDocument(raw, url) };
}

export interface RequestInput {
  method: string;
  url: string;
  headers: Record<string, string>;
  body: string;
}

export interface RequestResult {
  status: number;
  statusText: string;
  ok: boolean;
  durationMs: number;
  headers: Record<string, string>;
  body: string;
  contentType: string;
}

export async function sendRequest({ method, url, headers, body }: RequestInput): Promise<RequestResult> {
  const started = performance.now();
  const res = await fetch(url, {
    method: method.toUpperCase(),
    headers,
    body: ['GET', 'HEAD'].includes(method.toUpperCase()) ? undefined : body || undefined,
  });
  const text = await readBody(res);
  const out: Record<string, string> = {};
  res.headers.forEach((value, key) => {
    out[key] = value;
  });
  return {
    status: res.status,
    statusText: res.statusText,
    ok: res.ok,
    durationMs: Math.round(performance.now() - started),
    headers: out,
    body: text,
    contentType: out['content-type'] ?? '',
  };
}
