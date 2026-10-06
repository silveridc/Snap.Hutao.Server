import http, { bare, RET_SUCCESS } from '@/api/request'
import { rsaEncrypt } from '@/utils/crypto'
import { getDeviceHeaders } from '@/utils/device'

// 登录：邮箱与密码需 RSA 加密后提交，成功返回 TokenResponse。
export async function login(email, password) {
  const [UserName, Password] = await Promise.all([rsaEncrypt(email), rsaEncrypt(password)])
  const { data: envelope } = await bare.post(
    '/Passport/v2/Login',
    { UserName, Password },
    { headers: getDeviceHeaders() },
  )
  if (envelope?.retcode !== RET_SUCCESS) {
    const err = new Error(envelope?.message || '登录失败')
    err.retcode = envelope?.retcode
    throw err
  }
  return envelope.data
}

// 获取当前登录用户信息 (含是否为维护者、各项服务到期时间)。
export async function fetchUserInfo() {
  const envelope = await http.get('/Passport/v2/UserInfo')
  return envelope.data
}
