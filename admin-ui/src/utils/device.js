// 生成并持久化一个稳定的设备标识，用于登录时的 x-hutao-device-* 请求头。
// 后端 DeviceExtension 要求 x-hutao-device-id 必须存在，否则登录会抛异常。
const DEVICE_ID_KEY = 'hutao-admin-device-id'

function uuid() {
  if (globalThis.crypto?.randomUUID) {
    return crypto.randomUUID()
  }
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0
    const v = c === 'x' ? r : (r & 0x3) | 0x8
    return v.toString(16)
  })
}

export function getDeviceId() {
  let id = null
  try {
    id = localStorage.getItem(DEVICE_ID_KEY)
  } catch {
    id = null
  }
  if (!id) {
    id = uuid()
    try {
      localStorage.setItem(DEVICE_ID_KEY, id)
    } catch {
      /* ignore */
    }
  }
  return id
}

export function getDeviceHeaders() {
  return {
    'x-hutao-device-id': getDeviceId(),
    'x-hutao-device-name': 'Hutao Admin Web',
    'x-hutao-device-os': navigator.userAgent.slice(0, 120),
  }
}
