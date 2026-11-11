import { getLocale, t } from '@/i18n';

const localeTag = () => (getLocale() === 'zh_CN' ? 'zh-CN' : 'en-US');

export function formatNumber(value?: number | null): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return '—';
  }
  return value.toLocaleString(localeTag());
}

export function formatBytes(value?: number | null): string {
  if (value === null || value === undefined || !Number.isFinite(value) || value < 0) {
    return '—';
  }
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let size = value;
  let index = 0;
  while (size >= 1024 && index < units.length - 1) {
    size /= 1024;
    index += 1;
  }
  return `${index === 0 ? String(size) : size.toFixed(1)} ${units[index]}`;
}

// 只保留最大的两档，「3 天 6 小时」比四个单位更好读
export function formatDuration(seconds?: number | null): string {
  if (seconds === null || seconds === undefined || !Number.isFinite(seconds)) {
    return '—';
  }
  const parts: Array<[number, string]> = [
    [Math.floor(seconds / 86400), 'home.duration.d'],
    [Math.floor((seconds % 86400) / 3600), 'home.duration.h'],
    [Math.floor((seconds % 3600) / 60), 'home.duration.m'],
    [Math.floor(seconds % 60), 'home.duration.s'],
  ];
  const kept = parts.filter(([value]) => value > 0).slice(0, 2);
  if (!kept.length) {
    return t('home.duration.s', { count: 0 });
  }
  return kept.map(([value, key]) => t(key, { count: value })).join(' ');
}

// 服务端时间有两类：ISO 字符串（运行时、调度器）与 unix 秒（部分到期时间）
function toDate(value: string | number): Date | null {
  if (typeof value === 'number') {
    if (value <= 0) {
      return null;
    }
    return new Date(value * 1000);
  }
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

export function formatDateTime(value?: string | number | null): string {
  if (value === null || value === undefined || value === '') {
    return '—';
  }
  const date = toDate(value);
  return date ? date.toLocaleString(localeTag()) : String(value);
}

export function formatTime(value?: string | number | null): string {
  if (value === null || value === undefined || value === '') {
    return '—';
  }
  const date = toDate(value);
  return date ? date.toLocaleTimeString(localeTag()) : String(value);
}

// 账号到期时间常用「永久/已过期」表达
export function formatExpiry(value?: string | number | null): string {
  if (value === null || value === undefined || value === '') {
    return '—';
  }
  const date = toDate(value);
  if (!date) {
    return String(value);
  }
  if (date.getFullYear() >= 2100) {
    return t('common.permanent');
  }
  if (date.getTime() < Date.now()) {
    return `${date.toLocaleDateString(localeTag())} · ${t('common.expired')}`;
  }
  return date.toLocaleDateString(localeTag());
}
