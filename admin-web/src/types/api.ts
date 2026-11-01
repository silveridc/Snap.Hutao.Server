// 服务端契约类型。字段名与 C# 模型声明保持一致（PascalCase），
// 信封字段 retcode/message/data/l10nKey/time 是服务端显式命名的小写。

export interface Envelope<T = unknown> {
  retcode: number;
  message: string;
  data: T;
  l10nKey?: string;
  time?: string;
}

export interface TokenResponse {
  AccessToken: string;
  RefreshToken: string;
  ExpiresIn: number;
}

export interface UserInfo {
  UserName?: string;
  NormalizedUserName?: string;
  IsMaintainer: boolean;
  IsLicensedDeveloper: boolean;
  /** user = 主站用户，admin = 后台专属账号 */
  AuthKind: string;
  /** 启用单点登录后主站维护者会被降级为只读 */
  CanWrite: boolean;
  GachaLogExpireAt: string;
  CdnExpireAt: string;
}

export interface CaptchaSettings {
  Enabled: boolean;
  SiteKey?: string;
}

export interface AdminSsoStatus {
  Enabled: boolean;
  DisplayName?: string;
}

export interface AdminSsoTokenResponse {
  AccessToken: string;
  ExpiresIn: number;
}

export interface PagedResult<T> {
  Total: number;
  Page: number;
  PageSize: number;
  Items: T[];
}

export interface UserStatistics {
  TotalUsers: number;
  Maintainers: number;
  LicensedDevelopers: number;
  ActiveGachaLogUsers: number;
  ActiveCdnUsers: number;
  BannedUids: number;
  GachaItemCount: number;
  SpiralAbyssRecordCount: number;
  RoleCombatRecordCount: number;
  NewUsersToday: number;
  NewUsersLast7Days: number;
  NewUsersLast30Days: number;
}

// 列表与详情里的到期时间是 unix 秒，0 表示未授权
export interface UserListItem {
  Id: number;
  UserName?: string | null;
  IsMaintainer: boolean;
  IsLicensedDeveloper: boolean;
  GachaLogExpireAt: number;
  CdnExpireAt: number;
}

export interface UserGachaUid {
  Uid: string;
  ItemCount: number;
}

export interface UserDetail {
  Id: number;
  UserName?: string | null;
  IsMaintainer: boolean;
  IsLicensedDeveloper: boolean;
  GachaLogExpireAt: number;
  CdnExpireAt: number;
  RedeemUsedCount: number;
  GachaUids: UserGachaUid[];
}

// 封禁时间是 unix 秒
export interface BanItem {
  Uid: string;
  Reason?: string | null;
  BanTime: number;
}

export interface RuntimeInfo {
  ApplicationVersion: string;
  MachineName: string;
  OperatingSystem: string;
  Framework: string;
  ProcessId: number;
  StartedAt: string;
  UptimeSeconds: number;
  LocalTime: string;
  TimeZoneId: string;
}

export interface ResourceUsage {
  ProcessorCount: number;
  ProcessCpuPercent: number;
  ProcessCpuSeconds: number;
  WorkingSetBytes: number;
  PrivateMemoryBytes: number;
  ManagedHeapBytes: number;
  ThreadCount: number;
}

export interface DatabaseInfo {
  Name: string;
  Connected: boolean;
  ServerVersion?: string | null;
  Host?: string | null;
  Port?: number | null;
  Database?: string | null;
  MaxPoolSize?: number | null;
  RoundtripMs?: number | null;
  Error?: string | null;
}

export interface RedisInfo {
  Connected: boolean;
  Endpoint?: string | null;
  Version?: string | null;
  InstanceName: string;
  UsedMemory?: string | null;
  MaxMemoryPolicy?: string | null;
  ConnectedClients?: number | null;
  KeyCount?: number | null;
  UptimeDays?: number | null;
  PingMs?: number | null;
  Error?: string | null;
}

export interface SchedulerJobInfo {
  Group: string;
  Name: string;
  JobType: string;
  Cron?: string | null;
  State: string;
  NextFireAt?: string | null;
  PreviousFireAt?: string | null;
}

export interface SchedulerInfo {
  Name: string;
  InstanceId: string;
  State: string;
  JobCount: number;
  TriggerCount: number;
  Jobs: SchedulerJobInfo[];
  Error?: string | null;
}

export interface SystemOverview {
  Runtime: RuntimeInfo;
  Resource: ResourceUsage;
  Databases: DatabaseInfo[];
  Redis: RedisInfo;
  Scheduler: SchedulerInfo;
}

export interface AuditLogItem {
  Id: number;
  AdminUserId: number;
  AdminAccountId?: number | null;
  AdminUserName?: string | null;
  Action: string;
  Detail?: string | null;
  Ip?: string | null;
  IpRegion?: (string | null)[] | null;
  Success: boolean;
  Timestamp: number;
}

// 镜像模型带 JsonPropertyName，是服务端唯一的 snake_case 例外
export interface ReleaseMirror {
  url: string;
  mirror_name: string;
  mirror_type: string;
}

export interface HutaoReleaseItem {
  Id: number;
  Version: string;
  Validation: string;
  MinimumVersion?: string | null;
  Mirrors: ReleaseMirror[];
  ReleaseNotes?: string | null;
  IsActive: boolean;
  PublishedAt: string;
  PublishedBy: number;
  PublishedByUserName?: string | null;
}

export interface ReleasePublishForm {
  Version: string;
  Validation: string;
  MinimumVersion: string | null;
  Mirrors: ReleaseMirror[];
  ReleaseNotes: string | null;
}

export interface ReleasePublishFromBucketForm {
  Key: string;
  Version: string | null;
  Validation: string | null;
  MirrorName: string | null;
  MinimumVersion: string | null;
  ReleaseNotes: string | null;
}

export interface ReleaseBucketObjectItem {
  Key: string;
  Size: number;
  LastModified: string;
}

export interface ReleaseBucketListResult {
  Host: string;
  Prefix: string;
  Truncated: boolean;
  Objects: ReleaseBucketObjectItem[];
}

export interface LicenseApplicationItem {
  PrimaryId: number;
  UserId: number;
  UserName?: string | null;
  ProjectUrl: string;
  ApprovalCode: string;
  IsApproved: boolean;
}

// 主键是 (Id, Locale)，同一公告的多语言版本共用 Id
export interface AnnouncementItem {
  Id: number;
  Locale: string;
  Title: string;
  Content: string;
  /** 0 普通 / 1 成功 / 2 警告 / 3 错误 */
  Severity: number;
  Link: string;
  MaxPresentVersion?: string | null;
  Enabled: boolean;
  /** unix 秒 */
  LastUpdateTime: number;
}

export interface AnnouncementForm {
  Title: string;
  Content: string;
  Severity: number;
  Link: string;
  MaxPresentVersion: string | null;
}

export interface RedeemCodeItem {
  Id: number;
  Code: string;
  /** 位标志：1 限时 / 2 限次，可同时置位 */
  Type: number;
  /** 1 祈愿记录 / 2 CDN */
  ServiceType: number;
  /** 兑换后增加的服务天数 */
  Value: number;
  Description: string;
  Creator: string;
  CreateTime: string;
  ExpireTime: string;
  TimesAllowed: number;
  UsedCount: number;
}

export interface RedeemCodeUsageItem {
  UserId: number;
  UserName: string | null;
  UseTime: string;
}

export interface RedeemGenerateForm {
  Count: number;
  Type: number;
  ServiceType: number;
  Value: number;
  Description: string;
  ExpireTime: string;
  Times: number;
}

export interface RedeemCodeUpdateForm {
  Id: number;
  Type: number;
  ServiceType: number;
  Value: number;
  Description: string;
  ExpireTime: string;
  TimesAllowed: number;
}

// 兑换成功才有 data（失败走 retcode），这里只保留结果字段
export interface RedeemUseResult {
  Type: number;
  Value: number;
  ExpireTime: string;
}

// ===== 数据分析（MySQL 明细聚合） =====

export interface GachaPoolAnalytics {
  GachaType: number;
  Pulls: number;
  Star5: number;
  Star5Rate: number;
  AveragePullsPerStar5: number;
}

export interface GachaItemRank {
  ItemId: number;
  Count: number;
}

export interface GachaAnalytics {
  TotalPulls: number;
  TotalStar5: number;
  UntrustedPulls: number;
  Days: number;
  Pools: GachaPoolAnalytics[];
  TopStar5Items: GachaItemRank[];
}

export interface AbyssFloorAnalytics {
  Index: number;
  Records: number;
  AverageStar: number;
}

export interface AvatarRank {
  AvatarId: number;
  Count: number;
}

export interface AbyssModeAnalytics {
  Records: number;
  UniqueUids: number;
  FullStarRecords: number;
  Floors: AbyssFloorAnalytics[];
  TopAvatars: AvatarRank[];
}

export interface AbyssAnalytics {
  SpiralAbyss: AbyssModeAnalytics;
  Theater: AbyssModeAnalytics;
}

export interface DamageRankItem {
  AvatarId: number;
  TotalDamage: number;
  Share: number;
}

export interface DamageAnalytics {
  TotalDamage: number;
  SampleRecords: number;
  TopAvatars: DamageRankItem[];
}

export interface ClientVersionCount {
  Version: string;
  DeviceCount: number;
  /** unix 秒 */
  FirstSeenAt: number;
  LastSeenAt: number;
}

export interface ClientVersionStatistics {
  TotalDevices: number;
  Versions: ClientVersionCount[];
}

export interface RankEntry {
  Uid: string;
  Value: number;
}

// LoginAt 是 ISO 字符串；失败原因存的是服务端词条键，成功时为空
export interface LoginLogItem {
  Id: number;
  UserId?: number | null;
  UserName?: string | null;
  Method: string;
  Success: boolean;
  FailureKey?: string | null;
  Ip?: string | null;
  IpRegion?: (string | null)[] | null;
  DeviceId?: string | null;
  DeviceName?: string | null;
  OperatingSystem?: string | null;
  LoginAt: string;
}

// ===== 壁纸池 / 数据工具 / 缓存 / 邮件模板 / 后台账号 =====

export interface WallpaperPoolItem {
  Url: string;
  SourceUrl?: string | null;
  Author?: string | null;
  Uploader?: string | null;
}

export interface MailTemplatePayload {
  Template: string;
  DefaultTemplate: string;
  Customized: boolean;
}

export interface CacheInvalidationResult {
  RemovedKeys: number;
  RemovedByPattern: number;
}

export interface AdminAccountItem {
  Id: number;
  Provider: string;
  Subject: string;
  UserName: string;
  Email?: string | null;
  IsEnabled: boolean;
  IsBootstrap: boolean;
  CreatedAt: string;
  LastLoginAt?: string | null;
  BoundUserId?: number | null;
  BoundUserName?: string | null;
  IsCurrent: boolean;
}

// 动态配置就是 key -> 原始字符串 的字典，布尔/数字在服务端也存字符串
export type SystemConfigMap = Record<string, string | null>;
