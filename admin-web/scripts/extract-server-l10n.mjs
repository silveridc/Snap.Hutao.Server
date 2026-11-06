// 从 server/ServerKeys.cs 取键清单，用 launcher 的 SH.resx 文案生成后台的 server.json 语言包。
// 后台独有的键（客户端用不到，launcher resx 里没有）在下面的 adminOnly 里维护。
// 用法：在 admin-web 目录下 node scripts/extract-server-l10n.mjs
import fs from 'node:fs';

const SERVER_KEYS = '../server/ServerKeys.cs';
const RESX_DIR = 'D:/projects/hutao/launcher/src/Snap.Hutao/Snap.Hutao/Resource/Localization/';
const OUT_ZH = 'src/i18n/zh_CN/server.json';
const OUT_EN = 'src/i18n/en_US/server.json';

const unescape = (s) =>
  s
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&#\d+;/g, '?')
    .replace(/&amp;/g, '&')
    .trim();

const parseResx = (file) => {
  const text = fs.readFileSync(RESX_DIR + file, 'utf8');
  const map = new Map();
  for (const m of text.matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)) {
    map.set(m[1], unescape(m[2]));
  }
  return map;
};

// ServerKeys.cs 是键的唯一来源：常量名必须等于取值，否则后台按 l10nKey 查不到词条
const keys = [];
for (const m of fs.readFileSync(SERVER_KEYS, 'utf8').matchAll(/public const string (\w+) = "([^"]+)";/g)) {
  if (m[1] !== m[2]) {
    throw new Error(`ServerKeys.cs 常量名与取值不一致：${m[1]} = ${m[2]}`);
  }
  keys.push(m[1]);
}

const zh = parseResx('SH.resx');
const en = parseResx('SH.en.resx');

// 后台独有的键在 launcher 的 SH.resx 里不存在（客户端用不到），文案由本脚本维护
const adminOnly = {
  ServerAdminSsoNotEnabled: ['后台未启用单点登录', 'Admin SSO is not enabled'],
  ServerAdminSsoUpstreamFailed: ['身份源不可用，请稍后重试或改用账号密码登录', 'The identity provider is unavailable, please retry later'],
  ServerAdminSsoTicketInvalid: ['登录票据无效或已过期，请重新发起单点登录', 'The login ticket is invalid or has expired'],
  ServerAdminSsoLoginSucceed: ['后台单点登录成功', 'Admin SSO login succeeded'],
  ServerAdminWriteRequiresSso: ['启用单点登录后，只有后台专属账号可以修改数据', 'Only SSO admin accounts can modify data'],
  ServerAdminAccountListSucceed: ['后台账号清单获取成功', 'Admin account list acquired successfully'],
  ServerAdminAccountAddSucceed: ['后台账号已添加', 'Admin account added successfully'],
  ServerAdminAccountSubjectRequired: ['身份主体标识无效（为空或过长）', 'The identity subject is invalid'],
  ServerAdminAccountAlreadyExists: ['该身份已有后台账号', 'An admin account for this identity already exists'],
  ServerAdminAccountNotFound: ['后台账号不存在', 'Admin account does not exist'],
  ServerAdminAccountSelfOperation: ['不能停用或删除正在使用的后台账号', 'You cannot disable or remove the admin account you are using'],
  ServerAdminAccountLastEnabled: ['至少要保留一个可用的后台账号', 'At least one enabled admin account must remain'],
  ServerAdminAccountSetEnabledSucceed: ['后台账号状态已更新', 'Admin account state updated successfully'],
  ServerAdminAccountRemoveSucceed: ['后台账号已删除', 'Admin account removed successfully'],
  ServerAdminAccountBindSucceed: ['已绑定主站账号', 'Admin account bound successfully'],
  ServerAdminAccountUnbindSucceed: ['已解绑主站账号', 'Admin account unbound successfully'],
  ServerAdminAccountUserNotFound: ['要绑定的主站账号不存在', 'The Hutao account does not exist'],
  ServerAdminAccountUserRequired: ['请选择要绑定的主站账号', 'The Hutao account is required'],
  ServerAdminAccountUserTaken: ['该主站账号已被其他后台账号绑定', 'This Hutao account is already bound to another admin account'],
  ServerAdminAccountUserUnbound: ['该后台账号未绑定主站账号', 'This admin account is not bound to a Hutao account'],
  ServerAdminCannotDemoteDefaultAdmin: ['默认管理员不能被降级', 'The default administrator cannot be demoted'],
  ServerAdminSystemOverviewSucceed: ['获取系统概览成功', 'System overview acquired successfully'],
  ServerAdminWallpaperPoolInvalid: ['今日壁纸池不合法：直链必须是 HTTPS 绝对地址，且最多 50 条', 'Invalid wallpaper pool: every URL must be an absolute HTTPS URL and the pool is limited to 50 entries'],
  ServerAdminWallpaperPoolSaveSucceed: ['今日壁纸池已保存', 'Wallpaper pool saved'],
  ServerAdminMailTemplateEmpty: ['邮件模板不能为空', 'Mail template must not be empty'],
  ServerAdminMailTemplateGetSucceed: ['获取邮件模板成功', 'Mail template retrieved successfully'],
  ServerAdminMailTemplateNotFound: ['没有自定义邮件模板', 'No custom mail template'],
  ServerAdminMailTemplatePlaceholderRequired: ['邮件模板缺少必要占位符，不允许保存', 'Mail template is missing a required placeholder'],
  ServerAdminMailTemplateResetSucceed: ['邮件模板已恢复默认', 'Mail template restored to default'],
  ServerAdminMailTemplateSaveSucceed: ['邮件模板已保存', 'Mail template saved'],
  ServerAdminMailContentListSucceed: ['获取邮件内容清单成功', 'Mail content list acquired successfully'],
  ServerAdminMailContentGetSucceed: ['获取邮件内容成功', 'Mail content retrieved successfully'],
  ServerAdminMailContentSaveSucceed: ['邮件内容已保存', 'Mail content saved'],
  ServerAdminMailContentResetSucceed: ['邮件内容已恢复默认', 'Mail content restored to default'],
  ServerAdminMailContentNotFound: ['没有自定义邮件内容', 'No custom mail content'],
  ServerAdminMailContentKindInvalid: ['邮件种类不合法', 'The mail kind is invalid'],
  ServerAdminMailContentLanguageInvalid: ['邮件语言不合法', 'The mail language is invalid'],
  ServerAdminMailContentEmpty: ['邮件主题、标题、正文与页脚都不能为空', 'Mail subject, title, content and footer must not be empty'],
  ServerAdminMailContentVariableRequired: ['邮件正文缺少必要变量，不允许保存', 'Mail content is missing a required variable'],
  ServerAdminJobLogListSucceed: ['获取任务日志成功', 'Job log acquired successfully'],
  ServerAdminJobNotFound: ['任务不存在', 'Job does not exist'],
  ServerAdminJobTriggered: ['任务已触发', 'Job triggered'],
  ServerAdminAnalyticsAbyssSucceed: ['获取深渊数据分析成功', 'Abyss analytics acquired successfully'],
  ServerAdminAnalyticsDamageSucceed: ['获取伤害分布成功', 'Damage analytics acquired successfully'],
  ServerAdminAnalyticsGachaSucceed: ['获取卡池分布成功', 'Gacha analytics acquired successfully'],
  ServerAdminRedeemUsesSucceed: ['获取兑换码使用记录成功', 'Redeem code usages acquired successfully'],
  ServerAdminReleaseDeactivateSucceed: ['版本已下架', 'Release deactivated'],
};

const zhOut = { server: {} };
const enOut = { server: {} };
const missing = [];
for (const key of keys) {
  const extra = adminOnly[key];
  const zhText = extra?.[0] ?? zh.get(key);
  const enText = extra?.[1] ?? en.get(key) ?? zhText;
  if (!zhText) {
    missing.push(key);
    continue;
  }
  zhOut.server[key] = zhText;
  enOut.server[key] = enText;
}
if (missing.length) {
  throw new Error(`ServerKeys.cs 里这些键没有任何文案来源（SH.resx 与 adminOnly 都没有）：\n${missing.join('\n')}`);
}

// 前端自己维护的通用回执文案
zhOut.server.requestFailed = '请求失败';
zhOut.server.httpError = '服务异常 (HTTP {code})';
zhOut.server.networkError = '网络错误，请检查网络连接';
enOut.server.requestFailed = 'Request failed';
enOut.server.httpError = 'Service error (HTTP {code})';
enOut.server.networkError = 'Network error, please check your connection';

// resx 里以 Server 开头但已不在 ServerKeys.cs 的词条，说明服务端改过名，提示清理
const stale = [...zh.keys()].filter((k) => k.startsWith('Server') && !keys.includes(k));
if (stale.length) {
  console.log(`SH.resx 里有 ${stale.length} 条 Server* 词条已不在 ServerKeys.cs：\n${stale.join('\n')}`);
}

for (const [file, content] of [[OUT_ZH, zhOut], [OUT_EN, enOut]]) {
  const sorted = { server: Object.fromEntries(Object.entries(content.server).sort(([a], [b]) => (a < b ? -1 : 1))) };
  fs.writeFileSync(file, `${JSON.stringify(sorted, null, 2)}\n`);
  JSON.parse(fs.readFileSync(file, 'utf8'));
}
console.log('已生成', OUT_ZH, OUT_EN, '| server keys:', Object.keys(zhOut.server).length, '| JSON 合法');
