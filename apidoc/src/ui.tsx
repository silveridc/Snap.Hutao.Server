import { Toast } from '@douyinfe/semi-ui';

const METHOD_CLASS: Record<string, string> = {
  get: 'get',
  post: 'post',
  put: 'put',
  delete: 'delete',
  patch: 'patch',
  options: 'options',
  head: 'options',
};

export function MethodChip({ method }: { method: string }) {
  const kind = METHOD_CLASS[method.toLowerCase()] ?? 'none';
  return <span className={`mchip mchip-${kind}`}>{method.toUpperCase()}</span>;
}

export async function copyText(text: string): Promise<void> {
  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text);
    } else {
      const area = document.createElement('textarea');
      area.value = text;
      area.style.position = 'fixed';
      area.style.opacity = '0';
      document.body.appendChild(area);
      area.select();
      document.execCommand('copy');
      document.body.removeChild(area);
    }
    Toast.success('已复制');
  } catch {
    Toast.error('复制失败，请手动选择文本');
  }
}

/** 相对地址按当前站点补全，调试请求需要绝对地址 */
export function absoluteUrl(url: string): string {
  if (url === '') {
    return '';
  }
  if (/^https?:\/\//i.test(url)) {
    return url;
  }
  return window.location.origin + (url.startsWith('/') ? url : '/' + url);
}

export function prettyBody(text: string, contentType: string): string {
  if (text === '') {
    return '';
  }
  if (contentType.includes('json') || /^[[{]/.test(text.trim())) {
    try {
      return JSON.stringify(JSON.parse(text), null, 2);
    } catch {
      return text;
    }
  }
  return text;
}
