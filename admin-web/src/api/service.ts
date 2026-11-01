import { del, get, patch, post, put } from '@/lib/http';
import type {
  AbyssAnalytics,
  AdminAccountItem,
  AnnouncementForm,
  AnnouncementItem,
  AuditLogItem,
  BanItem,
  CacheInvalidationResult,
  ClientVersionStatistics,
  DamageAnalytics,
  GachaAnalytics,
  HutaoReleaseItem,
  LicenseApplicationItem,
  LoginLogItem,
  MailTemplatePayload,
  PagedResult,
  RankEntry,
  RedeemCodeItem,
  RedeemCodeUpdateForm,
  RedeemCodeUsageItem,
  RedeemGenerateForm,
  RedeemUseResult,
  ReleaseBucketListResult,
  ReleasePublishForm,
  ReleasePublishFromBucketForm,
  SystemConfigMap,
  SystemOverview,
  UserDetail,
  UserListItem,
  UserStatistics,
  WallpaperPoolItem,
} from '@/types/api';

export async function getUserStatistics(): Promise<UserStatistics> {
  const envelope = await get<UserStatistics>('/api/admin/users/statistics');
  return envelope.data;
}

export async function getSystemOverview(): Promise<SystemOverview> {
  const envelope = await get<SystemOverview>('/api/admin/system/overview');
  return envelope.data;
}

// 立即执行一个 Quartz 任务，name 是任务实现类的类名
export async function runJob(name: string): Promise<string> {
  const envelope = await post<null>(`/api/admin/jobs/${encodeURIComponent(name)}/run`);
  return envelope.message;
}

export async function runStatistics(): Promise<string> {
  const envelope = await post<null>('/api/admin/statistics/refresh');
  return envelope.message;
}

export async function getRankKeys(): Promise<string[]> {
  const envelope = await get<string[]>('/api/admin/ranks');
  return envelope.data;
}

export async function getRankLeaderboard(key: string, count: number): Promise<RankEntry[]> {
  const envelope = await get<RankEntry[]>(`/api/admin/ranks/${encodeURIComponent(key)}`, { count });
  return envelope.data;
}

export async function getLoginLogs(params: {
  page: number;
  pageSize: number;
  keyword?: string;
  success?: boolean;
}): Promise<PagedResult<LoginLogItem>> {
  const envelope = await get<PagedResult<LoginLogItem>>('/api/admin/login-logs', params);
  return envelope.data;
}

export async function getClientVersionList(): Promise<ClientVersionStatistics> {
  const envelope = await get<ClientVersionStatistics>('/api/admin/client-versions');
  return envelope.data;
}

export async function getGachaAnalytics(days: number): Promise<GachaAnalytics> {
  const envelope = await get<GachaAnalytics>('/api/admin/analytics/gacha', { days });
  return envelope.data;
}

export async function getAbyssAnalytics(): Promise<AbyssAnalytics> {
  const envelope = await get<AbyssAnalytics>('/api/admin/analytics/abyss');
  return envelope.data;
}

export async function getDamageAnalytics(top: number): Promise<DamageAnalytics> {
  const envelope = await get<DamageAnalytics>('/api/admin/analytics/damage', { top });
  return envelope.data;
}

export async function getAuditLogs(page = 1, pageSize = 20): Promise<PagedResult<AuditLogItem>> {
  const envelope = await get<PagedResult<AuditLogItem>>('/api/admin/audit-logs', { page, pageSize });
  return envelope.data;
}

export async function getLicenseApplications(page = 1, pageSize = 20, onlyPending = false): Promise<PagedResult<LicenseApplicationItem>> {
  const envelope = await get<PagedResult<LicenseApplicationItem>>('/api/admin/license-applications', {
    page,
    pageSize,
    onlyPending,
  });
  return envelope.data;
}

export async function getCurrentRelease(): Promise<HutaoReleaseItem | null> {
  const envelope = await get<HutaoReleaseItem | null>('/api/admin/releases/current');
  return envelope.data ?? null;
}

export async function getReleases(page: number, pageSize: number): Promise<PagedResult<HutaoReleaseItem>> {
  const envelope = await get<PagedResult<HutaoReleaseItem>>('/api/admin/releases', { page, pageSize });
  return envelope.data;
}

export async function publishRelease(form: ReleasePublishForm): Promise<string> {
  const envelope = await post<HutaoReleaseItem>('/api/admin/releases', form);
  return envelope.message;
}

export async function activateRelease(id: number): Promise<string> {
  const envelope = await put<null>(`/api/admin/releases/${id}/activation`, { Id: id });
  return envelope.message;
}

export async function deactivateRelease(id: number): Promise<string> {
  const envelope = await del<null>(`/api/admin/releases/${id}/activation`);
  return envelope.message;
}

export async function getReleaseBucketObjects(prefix: string): Promise<ReleaseBucketListResult> {
  const envelope = await get<ReleaseBucketListResult>('/api/admin/releases/bucket-objects', { prefix: prefix || undefined });
  return envelope.data;
}

export async function publishReleaseFromBucket(form: ReleasePublishFromBucketForm): Promise<string> {
  const envelope = await post<HutaoReleaseItem>('/api/admin/releases/from-bucket', form);
  return envelope.message;
}

export interface UserListFilters {
  keyword?: string;
  maintainer?: boolean;
  developer?: boolean;
  expiringInDays?: number;
}

export async function getUsers(page: number, pageSize: number, filters: UserListFilters = {}): Promise<PagedResult<UserListItem>> {
  const envelope = await get<PagedResult<UserListItem>>('/api/admin/users', {
    page,
    pageSize,
    keyword: filters.keyword || undefined,
    maintainer: filters.maintainer,
    developer: filters.developer,
    expiringInDays: filters.expiringInDays,
  });
  return envelope.data;
}

export async function getUserDetail(userId: number): Promise<UserDetail> {
  const envelope = await get<UserDetail>(`/api/admin/users/${userId}`);
  return envelope.data;
}

export async function updateUserPrivilege(userId: number, isMaintainer: boolean, isLicensedDeveloper: boolean): Promise<string> {
  const envelope = await patch<null>(`/api/admin/users/${userId}/privileges`, {
    IsMaintainer: isMaintainer,
    IsLicensedDeveloper: isLicensedDeveloper,
  });
  return envelope.message;
}

export async function resetUserPassword(userId: number, newPassword: string): Promise<string> {
  const envelope = await put<null>(`/api/admin/users/${userId}/password`, { NewPassword: newPassword });
  return envelope.message;
}

// 服务时长走补偿接口，按用户名定位，days 可为负数扣减
export async function grantGachaLogDuration(userName: string, days: number): Promise<string> {
  const envelope = await post<null>('/api/admin/gacha-logs/compensations/designation', undefined, { userName, days });
  return envelope.message;
}

export async function grantCdnDuration(userName: string, days: number): Promise<string> {
  const envelope = await post<null>('/api/admin/cdn/compensations/designation', undefined, { userName, days });
  return envelope.message;
}

// 全员补偿只有天数入参，接口不返回 l10nKey，回执直接用 message
export async function compensateGachaLogDuration(days: number): Promise<string> {
  const envelope = await post<null>('/api/admin/gacha-logs/compensations', undefined, { days });
  return envelope.message;
}

export async function compensateCdnDuration(days: number): Promise<string> {
  const envelope = await post<null>('/api/admin/cdn/compensations', undefined, { days });
  return envelope.message;
}

export async function getBans(page: number, pageSize: number, keyword = ''): Promise<PagedResult<BanItem>> {
  const envelope = await get<PagedResult<BanItem>>('/api/admin/bans', { page, pageSize, keyword: keyword || undefined });
  return envelope.data;
}

export async function banUid(uid: string, reason = ''): Promise<string> {
  const envelope = await post<null>('/api/admin/bans', undefined, { uid, reason: reason || undefined });
  return envelope.message;
}

export async function batchBanUids(uids: string[], reason = ''): Promise<string> {
  const envelope = await post<number>('/api/admin/bans/batch', { Uids: uids, Reason: reason || null });
  return envelope.message;
}

export async function unbanUid(uid: string): Promise<string> {
  const envelope = await del<null>(`/api/admin/bans/${encodeURIComponent(uid)}`);
  return envelope.message;
}

export async function getAnnouncements(page: number, pageSize: number): Promise<PagedResult<AnnouncementItem>> {
  const envelope = await get<PagedResult<AnnouncementItem>>('/api/admin/announcements', { page, pageSize });
  return envelope.data;
}

export async function uploadAnnouncement(form: AnnouncementForm): Promise<string> {
  const envelope = await post<null>('/api/admin/announcements', form);
  return envelope.message;
}

// locale 必传：主键是 (Id, Locale)，缺省会让服务端按 ALL 处理
export async function updateAnnouncement(id: number, locale: string, form: AnnouncementForm): Promise<string> {
  const envelope = await put<null>(`/api/admin/announcements/${id}`, { Id: id, Locale: locale, ...form });
  return envelope.message;
}

export async function setAnnouncementEnabled(id: number, enabled: boolean, locale: string): Promise<string> {
  const envelope = await patch<null>(`/api/admin/announcements/${id}`, undefined, { enabled, locale });
  return envelope.message;
}

export async function deleteAnnouncement(id: number, locale: string): Promise<string> {
  const envelope = await del<null>(`/api/admin/announcements/${id}`, { locale });
  return envelope.message;
}

export async function getRedeemCodes(page: number, pageSize: number, keyword = ''): Promise<PagedResult<RedeemCodeItem>> {
  const envelope = await get<PagedResult<RedeemCodeItem>>('/api/admin/redeem-codes', { page, pageSize, keyword: keyword || undefined });
  return envelope.data;
}

export async function generateRedeemCodes(form: RedeemGenerateForm): Promise<string[]> {
  const envelope = await post<{ Codes: string[] }>('/api/admin/redeem-codes/generation', form);
  return envelope.data.Codes;
}

// 使用记录按需拉取，列表接口不带
export async function getRedeemCodeUsages(id: number): Promise<RedeemCodeUsageItem[]> {
  const envelope = await get<RedeemCodeUsageItem[]>(`/api/admin/redeem-codes/${id}/usages`);
  return envelope.data;
}

export async function updateRedeemCode(form: RedeemCodeUpdateForm): Promise<string> {
  const envelope = await put<null>(`/api/admin/redeem-codes/${form.Id}`, form);
  return envelope.message;
}

export async function deleteRedeemCode(id: number): Promise<string> {
  const envelope = await del<null>(`/api/admin/redeem-codes/${id}`);
  return envelope.message;
}

// 批量删除只回条数，没有 l10nKey，文案由前端拼
export async function batchDeleteRedeemCodes(ids: number[]): Promise<number> {
  const envelope = await post<number>('/api/admin/redeem-codes/batch-deletion', { Ids: ids });
  return envelope.data;
}

// 兑换走公开接口，报文字段是小写，成功文案没有 l10nKey，由前端按 data 拼
export async function useRedeemCode(code: string, username: string | null): Promise<RedeemUseResult> {
  const envelope = await post<RedeemUseResult>('/api/redeem-codes/redemptions', { code, username });
  return envelope.data;
}

// ===== 壁纸池 =====

export async function getWallpaperPool(): Promise<WallpaperPoolItem[]> {
  const envelope = await get<WallpaperPoolItem[]>('/api/admin/wallpaper-pool');
  return envelope.data ?? [];
}

export async function saveWallpaperPool(items: WallpaperPoolItem[]): Promise<string> {
  const envelope = await put<null>('/api/admin/wallpaper-pool', items);
  return envelope.message;
}

// ===== 数据工具（放行版本 / 无效祈愿 UID） =====

export async function getAllowedVersions(): Promise<string[]> {
  const envelope = await get<string[]>('/api/admin/allowed-client-versions');
  return envelope.data ?? [];
}

export async function addAllowedVersion(header: string): Promise<string> {
  const envelope = await post<null>('/api/admin/allowed-client-versions', null, { header });
  return envelope.message;
}

export async function removeAllowedVersion(header: string): Promise<string> {
  const envelope = await del<null>('/api/admin/allowed-client-versions', { header });
  return envelope.message;
}

export async function getInvalidGachaUids(page = 1, pageSize = 20, keyword = ''): Promise<PagedResult<string>> {
  const envelope = await get<PagedResult<string>>('/api/admin/invalid-gacha-uids', { page, pageSize, keyword });
  return envelope.data;
}

export async function removeInvalidGachaUid(uid: string): Promise<string> {
  const envelope = await del<null>(`/api/admin/invalid-gacha-uids/${encodeURIComponent(uid)}`);
  return envelope.message;
}

// ===== 缓存 =====

export async function invalidateCache(payload: { Keys: string[]; Patterns: string[] }): Promise<CacheInvalidationResult> {
  const envelope = await del<CacheInvalidationResult>('/api/admin/cache', undefined, payload);
  return envelope.data;
}

export async function listCacheKeys(pattern = '*', limit = 500): Promise<string[]> {
  const envelope = await get<string[]>('/api/admin/cache/keys', { pattern, limit });
  return envelope.data ?? [];
}

// ===== 邮件模板 =====

export async function getMailTemplate(): Promise<MailTemplatePayload> {
  const envelope = await get<MailTemplatePayload>('/api/admin/mail/template');
  return envelope.data;
}

export async function saveMailTemplate(template: string): Promise<string> {
  const envelope = await put<null>('/api/admin/mail/template', { Template: template });
  return envelope.message;
}

export async function resetMailTemplate(): Promise<string> {
  const envelope = await del<null>('/api/admin/mail/template');
  return envelope.message;
}

// ===== 许可证审批 =====

export async function approveLicenseByService(userName: string, code: string): Promise<string> {
  const envelope = await post<null>('/api/admin/license-applications/approval', null, { userName, code });
  return envelope.message;
}

// ===== 后台账号（SSO 身份源） =====

export async function getAdminAccounts(): Promise<AdminAccountItem[]> {
  const envelope = await get<AdminAccountItem[]>('/api/admin/accounts');
  return envelope.data ?? [];
}

export async function addAdminAccount(subject: string, userName: string | null, email: string | null): Promise<string> {
  const envelope = await post<null>('/api/admin/accounts', { Subject: subject, UserName: userName, Email: email });
  return envelope.message;
}

export async function setAdminAccountEnabled(accountId: number, enabled: boolean): Promise<string> {
  const envelope = await patch<null>(`/api/admin/accounts/${accountId}`, null, { enabled });
  return envelope.message;
}

export async function removeAdminAccount(accountId: number): Promise<string> {
  const envelope = await del<null>(`/api/admin/accounts/${accountId}`);
  return envelope.message;
}

export async function bindAdminAccount(accountId: number, userName: string): Promise<string> {
  const envelope = await put<null>(`/api/admin/accounts/${accountId}/binding`, { UserName: userName });
  return envelope.message;
}

export async function unbindAdminAccount(accountId: number): Promise<string> {
  const envelope = await del<null>(`/api/admin/accounts/${accountId}/binding`);
  return envelope.message;
}

// ===== 系统设置（动态配置） =====

export async function getSystemConfig(): Promise<SystemConfigMap> {
  const envelope = await get<SystemConfigMap>('/api/admin/settings');
  return envelope.data ?? {};
}

export async function updateSystemConfig(payload: Record<string, string | null>): Promise<string> {
  const envelope = await patch<null>('/api/admin/settings', payload);
  return envelope.message;
}

export async function resetSystemConfig(key: string): Promise<string> {
  const envelope = await del<null>(`/api/admin/settings/${encodeURIComponent(key)}`);
  return envelope.message;
}
