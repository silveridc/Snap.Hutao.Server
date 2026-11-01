const DEVICE_ID_KEY = 'hutao-admin-device-id';

function uuid(): string {
  if (typeof globalThis.crypto?.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export function getDeviceId(): string {
  let id = null;
  try {
    id = localStorage.getItem(DEVICE_ID_KEY);
  } catch {
    id = null;
  }
  if (!id) {
    id = uuid();
    try {
      localStorage.setItem(DEVICE_ID_KEY, id);
    } catch {
      /* ignore */
    }
  }
  return id;
}

// 后端 DeviceExtension 要求 x-hutao-device-id 必须存在，缺失会让登录直接抛异常
export function getDeviceHeaders(): Record<string, string> {
  return {
    'x-hutao-device-id': getDeviceId(),
    'x-hutao-device-name': 'Hutao Admin Web',
    // refresh_tokens.operating_system 是 varchar(64)，超长的尾巴服务端会截掉，这里先按同一上限传
    'x-hutao-device-os': navigator.userAgent.slice(0, 64),
  };
}
