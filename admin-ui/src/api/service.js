import http from '@/api/request'

// ===== 服务运维 (需维护者权限, /Service/*) =====

// 手动触发深渊统计数据刷新
export function runStatistics() {
  return http.get('/Service/Statistics/Run')
}

// 为所有用户补偿祈愿记录服务时长
export function gachaLogCompensation(days) {
  return http.get('/Service/GachaLog/Compensation', { params: { days } })
}

// 为指定用户补偿祈愿记录服务时长
export function gachaLogDesignation(userName, days) {
  return http.get('/Service/GachaLog/Designation', { params: { userName, days } })
}

// 为所有用户补偿 CDN 服务时长
export function distributionCompensation(days) {
  return http.get('/Service/Distribution/Compensation', { params: { days } })
}

// 为指定用户补偿 CDN 服务时长
export function distributionDesignation(userName, days) {
  return http.get('/Service/Distribution/Designation', { params: { userName, days } })
}

// 上传公告
export function uploadAnnouncement(payload) {
  return http.post('/Service/Announcement/Upload', payload)
}

// 生成兑换码
export function generateRedeemCodes(payload) {
  return http.post('/Service/Redeem/Generate', payload)
}

// 批量将缓存标记为过期 (payload: { Keys: string[], Patterns: string[] })
export function invalidateCache(payload) {
  return http.post('/Service/Cache/Invalidate', payload)
}

// 列出匹配 glob 模式的缓存键
export function listCacheKeys(pattern = '*', limit = 500) {
  return http.get('/Service/Cache/Keys', { params: { pattern, limit } })
}

// 获取用户数据统计概览
export function getUserStatistics() {
  return http.get('/Service/User/Statistics')
}

// 分页获取用户列表
export function getUserList(page = 1, pageSize = 20, keyword = '', filters = {}) {
  return http.get('/Service/User/List', { params: { page, pageSize, keyword, ...filters } })
}

// 更新用户维护者/开发者授权标志 (payload: { UserId, IsMaintainer?, IsLicensedDeveloper? })
export function updateUserPrivilege(payload) {
  return http.post('/Service/User/Privilege', payload)
}

// 重置用户密码 (payload: { UserId, NewPassword })
export function resetUserPassword(payload) {
  return http.post('/Service/User/ResetPassword', payload)
}

// 分页获取封禁 Uid 列表
export function getBanList(page = 1, pageSize = 20, keyword = '') {
  return http.get('/Service/Ban/List', { params: { page, pageSize, keyword } })
}

// 封禁 Uid
export function banUid(uid, reason = '') {
  return http.post('/Service/Ban/Add', null, { params: { uid, reason } })
}

// 解封 Uid
export function unbanUid(uid) {
  return http.post('/Service/Ban/Remove', null, { params: { uid } })
}

// 公告列表 / 删除
export function getAnnouncementList(page = 1, pageSize = 20) {
  return http.get('/Service/Announcement/List', { params: { page, pageSize } })
}
export function deleteAnnouncement(id, locale = 'ALL') {
  return http.post('/Service/Announcement/Delete', null, { params: { id, locale } })
}
// 编辑公告 (payload: { Id, Locale, Title, Content, Severity, Link, MaxPresentVersion })
export function updateAnnouncement(payload) {
  return http.post('/Service/Announcement/Update', payload)
}

// 兑换码列表
export function getRedeemList(page = 1, pageSize = 20, keyword = '') {
  return http.get('/Service/Redeem/List', { params: { page, pageSize, keyword } })
}

// 编辑兑换码 (payload: { Id, ServiceType, Value, Description, ExpireTime, TimesAllowed })
export function updateRedeemCode(payload) {
  return http.post('/Service/Redeem/Update', payload)
}

// 删除兑换码
export function deleteRedeemCode(id) {
  return http.post('/Service/Redeem/Delete', null, { params: { id } })
}

// 开源许可证申请列表
export function getLicenseApplications(page = 1, pageSize = 20, onlyPending = false) {
  return http.get('/Service/License/Applications', { params: { page, pageSize, onlyPending } })
}

// 批准开源许可证申请 (维护者鉴权)
export function approveLicenseByService(userName, code) {
  return http.post('/Service/License/Approve', null, { params: { userName, code } })
}

// 放行版本 列表 / 新增 / 移除
export function getAllowedVersions() {
  return http.get('/Service/AllowedVersion/List')
}
export function addAllowedVersion(header) {
  return http.post('/Service/AllowedVersion/Add', null, { params: { header } })
}
export function removeAllowedVersion(header) {
  return http.post('/Service/AllowedVersion/Remove', null, { params: { header } })
}

// 无效祈愿 Uid 列表 / 移除
export function getInvalidGachaUids(page = 1, pageSize = 20, keyword = '') {
  return http.get('/Service/InvalidGachaUid/List', { params: { page, pageSize, keyword } })
}
export function removeInvalidGachaUid(uid) {
  return http.post('/Service/InvalidGachaUid/Remove', null, { params: { uid } })
}

// 后台审计日志
export function getAuditLogs(page = 1, pageSize = 20) {
  return http.get('/Service/Audit/List', { params: { page, pageSize } })
}

// 公告上/下架
export function setAnnouncementEnabled(id, enabled, locale = 'ALL') {
  return http.post('/Service/Announcement/SetEnabled', null, { params: { id, enabled, locale } })
}

// 用户详情
export function getUserDetail(userId) {
  return http.get('/Service/User/Detail', { params: { userId } })
}

// 批量封禁 Uid
export function batchBanUids(uids, reason = '') {
  return http.post('/Service/Ban/BatchAdd', { Uids: uids, Reason: reason })
}

// 批量删除兑换码
export function batchDeleteRedeemCodes(ids) {
  return http.post('/Service/Redeem/BatchDelete', { Ids: ids })
}

// 排行榜键 / 排行榜数据
export function getRankKeys() {
  return http.get('/Service/Rank/Keys')
}
export function getRankLeaderboard(key, count = 50) {
  return http.get('/Service/Rank/Leaderboard', { params: { key, count } })
}

// ===== Release 发布管理 =====

// 获取当前激活的发布版本
export function getReleaseCurrent() {
  return http.get('/Service/Release/Current')
}

// 分页获取发布历史
export function getReleaseList(page = 1, pageSize = 20) {
  return http.get('/Service/Release/List', { params: { page, pageSize } })
}

// 发布新版本 (payload: { Version, Validation, Mirrors, ReleaseNotes })
export function publishRelease(payload) {
  return http.post('/Service/Release/Publish', payload)
}

// 激活历史版本 (payload: { Id })
export function activateRelease(payload) {
  return http.post('/Service/Release/Activate', payload)
}

// ===== 系统配置 =====

// 获取所有动态配置
export function getSystemConfig() {
  return http.get('/Service/Config/Get')
}

// 批量更新配置 (payload: { key: value, ... })
export function updateSystemConfig(payload) {
  return http.post('/Service/Config/Update', payload)
}

// 重置配置项为默认值
export function resetSystemConfig(key) {
  return http.post('/Service/Config/Reset', null, { params: { key } })
}

// ===== 其它管理操作 =====

// 使用兑换码 (username 为空则表示当前登录账号)
export function useRedeemCode(payload) {
  return http.post('/Redeem/Use', payload)
}
