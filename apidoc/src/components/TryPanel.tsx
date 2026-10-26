import { useMemo, useState } from 'react';
import { Button, Input, Select, TextArea, Toast } from '@douyinfe/semi-ui';
import { IconSend, IconClear } from '@douyinfe/semi-icons';
import type { NormalizedDoc, Operation, RawDocument, SecurityScheme } from '../openapi/types';
import { buildExample, stringifyExample } from '../openapi/example';
import { buildCurl } from '../lib/curl';
import { sendRequest, type RequestResult } from '../api/client';
import { absoluteUrl, copyText, prettyBody } from '../ui';
import CodeBlock from './CodeBlock';

const AUTH_KEY = 'semi-swagger-doc:auth';
const VERBS = ['GET', 'POST', 'PUT', 'DELETE', 'PATCH', 'HEAD', 'OPTIONS'];

interface Props {
  op: Operation;
  doc: NormalizedDoc;
  raw: RawDocument;
}

function readAuthStore(): Record<string, string> {
  try {
    return JSON.parse(localStorage.getItem(AUTH_KEY) ?? '{}') as Record<string, string>;
  } catch {
    return {};
  }
}

function writeAuthStore(value: Record<string, string>) {
  try {
    localStorage.setItem(AUTH_KEY, JSON.stringify(value));
  } catch {
    /* 隐私模式写不进去，忽略 */
  }
}

function authPair(scheme: SecurityScheme, token: string): [string, string] | null {
  if (token === '') {
    return null;
  }
  const header = scheme.headerName || scheme.name;
  if (scheme.type === 'http') {
    return [header, scheme.scheme.toLowerCase() === 'bearer' ? `Bearer ${token}` : token];
  }
  if (scheme.type === 'apiKey' && scheme.in === 'query') {
    return null;
  }
  if (scheme.bearerHint) {
    return [header, /^Bearer\s+/i.test(token) ? token : `Bearer ${token}`];
  }
  return [header, token];
}

export default function TryPanel({ op, doc, raw }: Props) {
  const defaults = useMemo(() => {
    const out: Record<string, string> = {};
    for (const p of op.params) {
      out[`${p.location}:${p.name}`] = p.defaultValue || p.example || '';
    }
    return out;
  }, [op]);

  const bodyDefault = useMemo(() => {
    const first = op.requestContents[0];
    return first ? stringifyExample(buildExample(first.schema ?? {}, raw)) : '';
  }, [op, raw]);

  const [method, setMethod] = useState(op.method);
  const [url, setUrl] = useState(() => absoluteUrl((doc.meta.baseUrl || '') + op.path));
  const [values, setValues] = useState<Record<string, string>>(defaults);
  const [body, setBody] = useState(bodyDefault);
  const [auth, setAuth] = useState<Record<string, string>>(readAuthStore);
  const [sending, setSending] = useState(false);
  const [result, setResult] = useState<RequestResult | null>(null);

  const schemes = op.securityNames
    .map((name) => doc.securitySchemes.find((s) => s.name === name))
    .filter((s): s is SecurityScheme => Boolean(s));

  const setValue = (key: string, value: string) => setValues((prev) => ({ ...prev, [key]: value }));

  const requestUrl = useMemo(() => {
    let base = url.trim();
    for (const p of op.params.filter((item) => item.location === 'path')) {
      base = base.replace(`{${p.name}}`, encodeURIComponent(values[`path:${p.name}`] ?? ''));
    }
    const pairs = op.params
      .filter((p) => p.location === 'query')
      .map((p) => [p.name, values[`query:${p.name}`] ?? ''] as const)
      .filter(([, value]) => value !== '')
      .map(([name, value]) => `${encodeURIComponent(name)}=${encodeURIComponent(value)}`);
    const queryAuth = schemes
      .map((s) => [s.headerName || s.name, auth[s.name] ?? ''] as const)
      .filter(([name, value]) => {
        const scheme = doc.securitySchemes.find((s) => s.name === name);
        return scheme?.in === 'query' && value !== '';
      })
      .map(([name, value]) => `${encodeURIComponent(name)}=${encodeURIComponent(value)}`);
    const all = [...pairs, ...queryAuth];
    return all.length ? `${base}${base.includes('?') ? '&' : '?'}${all.join('&')}` : base;
  }, [url, op.params, values, schemes, auth, doc.securitySchemes]);

  const headers = useMemo(() => {
    const out: Record<string, string> = {};
    for (const scheme of schemes) {
      const pair = authPair(scheme, auth[scheme.name] ?? '');
      if (pair) {
        out[pair[0]] = pair[1];
      }
    }
    for (const p of op.params.filter((item) => item.location === 'header')) {
      const value = values[`header:${p.name}`] ?? '';
      if (value !== '') {
        out[p.name] = value;
      }
    }
    if (op.requestContents.length > 0 && !out['Content-Type']) {
      out['Content-Type'] = op.requestContents[0].contentType;
    }
    return out;
  }, [schemes, auth, op.params, op.requestContents, values]);

  const hasBody = !['GET', 'HEAD'].includes(method) && op.requestContents.length > 0;
  const curl = buildCurl({ method, url: requestUrl, headers, body: hasBody ? body : '' });

  const updateAuth = (name: string, value: string) => {
    const next = { ...auth, [name]: value };
    setAuth(next);
    writeAuthStore(next);
  };

  async function submit() {
    setSending(true);
    setResult(null);
    try {
      const res = await sendRequest({
        method,
        url: requestUrl,
        headers,
        body: hasBody ? body : '',
      });
      setResult(res);
      if (!res.ok) {
        Toast.warning(`响应状态 ${res.status}`);
      }
    } catch (e) {
      Toast.error(e instanceof Error ? e.message : '请求失败，可能是跨域被拦截');
    } finally {
      setSending(false);
    }
  }

  const formParams = op.params.filter((p) => p.location !== 'cookie');

  return (
    <div className="card">
      <div className="section-title">在线调试</div>

      {schemes.length > 0 && (
        <div className="auth-box">
          {schemes.map((scheme) => (
            <div className="debug-row" key={scheme.name}>
              <span className="label" title={scheme.description}>
                {scheme.name}
                {scheme.type === 'http'
                  ? `（HTTP ${scheme.scheme} → ${scheme.headerName || 'Authorization'}）`
                  : `（${scheme.in} ${scheme.headerName}）`}
              </span>
              <Input
                mode="password"
                style={{ flex: 1 }}
                placeholder={scheme.type === 'http' || scheme.bearerHint ? '只填 token，自动加 Bearer 前缀' : '凭证值'}
                value={auth[scheme.name] ?? ''}
                onChange={(value) => updateAuth(scheme.name, value)}
              />
            </div>
          ))}
        </div>
      )}

      <div className="debug-row">
        <Select value={method} onChange={(value) => setMethod(String(value))} style={{ width: 110 }} optionList={VERBS.map((v) => ({ label: v, value: v }))} />
        <Input value={url} onChange={(value) => setUrl(String(value))} style={{ flex: 1 }} placeholder="请求地址" />
        <Button theme="solid" type="primary" icon={<IconSend />} loading={sending} onClick={() => void submit()}>
          发送请求
        </Button>
      </div>

      <div className="debug-row">
        <span className="label">实际请求</span>
        <code className="dim" style={{ fontSize: 12, wordBreak: 'break-all', flex: 1 }}>
          {requestUrl}
        </code>
        <Button size="small" theme="borderless" onClick={() => void copyText(requestUrl)}>
          复制
        </Button>
      </div>

      {formParams.length > 0 && (
        <div className="debug-grid" style={{ margin: '12px 0' }}>
          {formParams.map((p) => {
            const key = `${p.location}:${p.name}`;
            return (
              <div className="debug-row" key={key}>
                <span className="label" title={p.description}>
                  {p.name}
                  <span className="dim"> · {p.location} · {p.type}</span>
                  {p.required && <span className="req-star">*</span>}
                </span>
                {p.enumValues.length > 0 ? (
                  <Select
                    style={{ flex: 1 }}
                    value={values[key] ?? ''}
                    onChange={(value) => setValue(key, String(value))}
                    optionList={p.enumValues.map((v) => ({ label: v, value: v }))}
                  />
                ) : (
                  <Input
                    style={{ flex: 1 }}
                    placeholder={p.description || p.name}
                    value={values[key] ?? ''}
                    onChange={(value) => setValue(key, String(value))}
                  />
                )}
              </div>
            );
          })}
        </div>
      )}

      {op.requestContents.length > 0 && (
        <div style={{ marginBottom: 12 }}>
          <div className="debug-row">
            <span className="label">请求体（{op.requestContents[0].contentType}）</span>
            <Button size="small" theme="borderless" icon={<IconClear />} onClick={() => setBody(bodyDefault)}>
              重置为示例
            </Button>
          </div>
          <TextArea rows={8} value={body} onChange={(value) => setBody(String(value))} style={{ fontFamily: 'Consolas, Menlo, monospace', fontSize: 12.5 }} />
        </div>
      )}

      {result && (
        <>
          <div className="result-head">
            <span className={`mchip ${result.ok ? 'mchip-get' : 'mchip-delete'}`}>{result.status}</span>
            <span>耗时 {result.durationMs} ms</span>
            <span>{result.contentType || '无 content-type'}</span>
            <span>{result.body.length} 字节</span>
          </div>
          <CodeBlock text={prettyBody(result.body, result.contentType)} maxHeight={340} />
          <details style={{ marginTop: 10 }}>
            <summary className="dim" style={{ fontSize: 12, cursor: 'pointer' }}>
              响应头
            </summary>
            <pre className="code code-light" style={{ maxHeight: 220 }}>
              {JSON.stringify(result.headers, null, 2)}
            </pre>
          </details>
        </>
      )}

      <div className="section-title" style={{ marginTop: 16, fontSize: 13 }}>
        curl 命令
      </div>
      <CodeBlock text={curl} light maxHeight={220} />
    </div>
  );
}
