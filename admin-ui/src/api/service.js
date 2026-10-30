import http from '@/api/request'

// ===== 服务运维 (需维护者权限, /Service/*) =====

// 手动触发深渊统计数据刷新
export function runStatistics() {
  return http.post('/api/admin/statistics/refresh')
}

// 为所有用户补偿祈愿记录服务时长
export function gachaLogCompensation(days) {
  return http.post('/api/admin/gacha-logs/compensations', null, { params: { days } })
}

// 为指定用户补偿祈愿记录服务时长
export function gachaLogDesignation(userName, days) {
  return http.post('/api/admin/gacha-logs/compensations/designation', null, { params: { userName, days } })
}

// 为所有用户补偿 CDN 服务时长
export function distributionCompensation(days) {
  return http.post('/api/admin/cdn/compensations', null, { params: { days } })
}

// 为指定用户补偿 CDN 服务时长
export function distributionDesignation(userName, days) {
  return http.post('/api/admin/cdn/compensations/designation', null, { params: { userName, days } })
}

// 上传公告
export function uploadAnnouncement(payload) {
  return http.post('/api/admin/announcements', payload)
}

// 生成兑换码
export function generateRedeemCodes(payload) {
  return http.post('/api/admin/redeem-codes/generation', payload)
}

// 批量将缓存标记为过期 (payload: { Keys: string[], Patterns: string[] })
export function invalidateCache(payload) {
  return http.delete('/api/admin/cache', { data: payload })
}

// 列出匹配 glob 模式的缓存键
export function listCacheKeys(pattern = '*', limit = 500) {
  return http.get('/api/admin/cache/keys', { params: { pattern, limit } })
}

// 获取用户数据统计概览
export function getUserStatistics() {
  return http.get('/api/admin/users/statistics')
}

// 获取服务端运行状态概览（运行时/资源/数据库/Redis/定时任务）
export function getSystemOverview() {
  return http.get('/api/admin/system/overview')
}

// 获取胡桃客户端版本分布（按版本汇总在册设备数）
export function getClientVersionList() {
  return http.get('/api/admin/client-versions')
}

// 分页获取用户列表
export function getUserList(page = 1, pageSize = 20, keyword = '', filters = {}) {
  return http.get('/api/admin/users', { params: { page, pageSize, keyword, ...filters } })
}

// 更新用户维护者/开发者授权标志 (payload: { UserId, IsMaintainer?, IsLicensedDeveloper? })
export function updateUserPrivilege(payload) {
  return http.patch(`/api/admin/users/${payload.UserId}/privileges`, payload)
}

// 重置用户密码 (payload: { UserId, NewPassword })
export function resetUserPassword(payload) {
  return http.put(`/api/admin/users/${payload.UserId}/password`, payload)
}

// 分页获取封禁 Uid 列表
export function getBanList(page = 1, pageSize = 20, keyword = '') {
  return http.get('/api/admin/bans', { params: { page, pageSize, keyword } })
}

// 封禁 Uid
export function banUid(uid, reason = '') {
  return http.post('/api/admin/bans', null, { params: { uid, reason } })
}

// 解封 Uid
export function unbanUid(uid) {
  return http.delete(`/api/admin/bans/${encodeURIComponent(uid)}`)
}

// 公告列表 / 删除
export function getAnnouncementList(page = 1, pageSize = 20) {
  return http.get('/api/admin/announcements', { params: { page, pageSize } })
}
export function deleteAnnouncement(id, locale = 'ALL') {
  return http.delete(`/api/admin/announcements/${id}`, { params: { locale } })
}
// 编辑公告 (payload: { Id, Locale, Title, Content, Severity, Link, MaxPresentVersion })
export function updateAnnouncement(payload) {
  return http.put(`/api/admin/announcements/${payload.Id}`, payload)
}

// 兑换码列表
// 某张兑换码的使用者清单
export function getRedeemCodeUses(id) {
  return http.get(`/api/admin/redeem-codes/${id}/usages`)
}

export function getRedeemList(page = 1, pageSize = 20, keyword = '') {
  return http.get('/api/admin/redeem-codes', { params: { page, pageSize, keyword } })
}

// 编辑兑换码 (payload: { Id, ServiceType, Value, Description, ExpireTime, TimesAllowed })
export function updateRedeemCode(payload) {
  return http.put(`/api/admin/redeem-codes/${payload.Id}`, payload)
}

// 删除兑换码
export function deleteRedeemCode(id) {
  return http.delete(`/api/admin/redeem-codes/${id}`)
}

// 开源许可证申请列表
export function getLicenseApplications(page = 1, pageSize = 20, onlyPending = false) {
  return http.get('/api/admin/license-applications', { params: { page, pageSize, onlyPending } })
}

// 批准开源许可证申请 (维护者鉴权)
export function approveLicenseByService(userName, code) {
  return http.post('/api/admin/license-applications/approval', null, { params: { userName, code } })
}

// 放行版本 列表 / 新增 / 移除
export function getAllowedVersions() {
  return http.get('/api/admin/allowed-client-versions')
}
export function addAllowedVersion(header) {
  return http.post('/api/admin/allowed-client-versions', null, { params: { header } })
}
export function removeAllowedVersion(header) {
  return http.delete('/api/admin/allowed-client-versions', { params: { header } })
}

// 无效祈愿 Uid 列表 / 移除
export function getInvalidGachaUids(page = 1, pageSize = 20, keyword = '') {
  return http.get('/api/admin/invalid-gacha-uids', { params: { page, pageSize, keyword } })
}
export function removeInvalidGachaUid(uid) {
  return http.delete(`/api/admin/invalid-gacha-uids/${encodeURIComponent(uid)}`)
}

// 后台审计日志
export function getAuditLogs(page = 1, pageSize = 20) {
  return http.get('/api/admin/audit-logs', { params: { page, pageSize } })
}

// 客户端登录日志 (params: { page, pageSize, keyword, success, from, to })
export function getLoginLogs(params) {
  return http.get('/api/admin/login-logs', { params })
}

// 公告上/下架
export function setAnnouncementEnabled(id, enabled, locale = 'ALL') {
  return http.patch(`/api/admin/announcements/${id}`, null, { params: { enabled, locale } })
}

// 用户详情
export function getUserDetail(userId) {
  return http.get(`/api/admin/users/${userId}`)
}

// 批量封禁 Uid
export function batchBanUids(uids, reason = '') {
  return http.post('/api/admin/bans/batch', { Uids: uids, Reason: reason })
}

// 批量删除兑换码
export function batchDeleteRedeemCodes(ids) {
  return http.post('/api/admin/redeem-codes/batch-deletion', { Ids: ids })
}

// 排行榜键 / 排行榜数据
export function getRankKeys() {
  return http.get('/api/admin/ranks')
}
export function getRankLeaderboard(key, count = 50) {
  return http.get(`/api/admin/ranks/${encodeURIComponent(key)}`, { params: { count } })
}

// ===== Release 发布管理 =====

// 获取当前激活的发布版本
export function getReleaseCurrent() {
  return http.get('/api/admin/releases/current')
}

// 分页获取发布历史
export function getReleaseList(page = 1, pageSize = 20) {
  return http.get('/api/admin/releases', { params: { page, pageSize } })
}

// 发布新版本 (payload: { Version, Validation, Mirrors, ReleaseNotes })
export function publishRelease(payload) {
  return http.post('/api/admin/releases', payload)
}

// 激活历史版本 (payload: { Id })
export function activateRelease(payload) {
  return http.put(`/api/admin/releases/${payload.Id}/activation`, payload)
}

// 下架当前激活版本（客户端不再收到该版本更新）
export function deactivateRelease(payload) {
  return http.delete(`/api/admin/releases/${payload.Id}/activation`)
}

// 列出补丁存储桶中的对象（从存储桶发布用）
export function getReleaseBucketObjects(prefix = '') {
  return http.get('/api/admin/releases/bucket-objects', { params: { prefix } })
}

// 从存储桶发布 (payload: { Key, Version, MirrorName, MinimumVersion, ReleaseNotes })
// 服务端要读完整个对象算 SHA-256，几百 MiB 的安装包会超过默认 20 秒超时
export function publishReleaseFromBucket(payload) {
  return http.post('/api/admin/releases/from-bucket', payload, { timeout: 0 })
}

// ===== 今日壁纸池 =====

// 获取今日壁纸池（旧版单张配置会作为一条记录返回）
export function getWallpaperPool() {
  return http.get('/api/admin/wallpaper-pool')
}

// 保存今日壁纸池 (payload: [{ Url, SourceUrl, Author, Uploader }])
export function saveWallpaperPool(items) {
  return http.put('/api/admin/wallpaper-pool', items)
}

// ===== 系统配置 =====

// 获取所有动态配置
export function getSystemConfig() {
  return http.get('/api/admin/settings')
}

// 批量更新配置 (payload: { key: value, ... })
export function updateSystemConfig(payload) {
  return http.patch('/api/admin/settings', payload)
}

// 重置配置项为默认值
export function resetSystemConfig(key) {
  return http.delete(`/api/admin/settings/${encodeURIComponent(key)}`)
}

// ===== 后台专属账号 (SSO) =====

// 后台账号清单，当前调用者的那一行会带 IsCurrent
export function getAdminAccounts() {
  return http.get('/api/admin/accounts')
}

// 允许身份源里的某个主体使用后台
export function addAdminAccount(subject, userName, email) {
  return http.post('/api/admin/accounts', { Subject: subject, UserName: userName, Email: email })
}

// 启用或停用后台账号，停用会让已签发的后台令牌立即失效
export function setAdminAccountEnabled(accountId, enabled) {
  return http.patch(`/api/admin/accounts/${accountId}`, null, { params: { enabled } })
}

// 删除后台账号
export function removeAdminAccount(accountId) {
  return http.delete(`/api/admin/accounts/${accountId}`)
}

// 把后台账号绑到一个主站账号（邮箱）上，绑定后这个身份才能用单点登录进启动器
export function bindAdminAccount(accountId, userName) {
  return http.put(`/api/admin/accounts/${accountId}/binding`, { UserName: userName })
}

// 解除绑定，该身份随即不能再用于启动器登录
export function unbindAdminAccount(accountId) {
  return http.delete(`/api/admin/accounts/${accountId}/binding`)
}

// 卡池抽卡分布（MySQL 明细聚合）
export function getGachaAnalytics(days = 30) {
  return http.get('/api/admin/analytics/gacha', { params: { days } })
}

// 深渊玩法数据：深境螺旋 + 幻想真境剧诗
export function getAbyssAnalytics() {
  return http.get('/api/admin/analytics/abyss')
}

// 伤害分布（MySQL damage_ranks 聚合）
export function getDamageAnalytics(top = 10) {
  return http.get('/api/admin/analytics/damage', { params: { top } })
}

// ===== 其它管理操作 =====

// 使用兑换码 (username 为空则表示当前登录账号)
export function useRedeemCode(payload) {
  return http.post('/api/redeem-codes/redemptions', payload)
}
