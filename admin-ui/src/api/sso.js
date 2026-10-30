import http from '@/api/request'

// 后台是否启用单点登录，登录页据此决定是否显示 SSO 入口
export function getSsoStatus() {
  return http.get('/api/admin/sso/status')
}

// 取跳转到身份源的授权地址，拿到后整页跳转 (不要在 XHR 里跟随)
export function getSsoChallenge(returnPath) {
  return http.get('/api/admin/sso/challenge', { params: { returnPath } })
}

// 用一次性票据换取后台令牌，票据只能用一次
export function exchangeSsoTicket(ticket) {
  return http.post('/api/admin/sso/sessions', { Ticket: ticket })
}
