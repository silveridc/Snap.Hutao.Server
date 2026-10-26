// 一次性脚本：从 launcher 客户端 resx 提取 Server* 词条，生成 admin-ui 分页语言包
import fs from 'node:fs';

const RESX_DIR = 'D:/projects/hutao/launcher/src/Snap.Hutao/Snap.Hutao/Resource/Localization/';
const OUT_ZH = 'src/locales/lang/zh_CN/pages/server.json';
const OUT_EN = 'src/locales/lang/en_US/pages/server.json';

const unescape = (s) =>
  s
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&#\d+;/g, '?')
    .replace(/&amp;/g, '&')
    .trim();

const parse = (file) => {
  const text = fs.readFileSync(RESX_DIR + file, 'utf8');
  const map = new Map();
  for (const m of text.matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)) {
    map.set(m[1], unescape(m[2]));
  }
  return map;
};

const zh = parse('SH.resx');
const en = parse('SH.en.resx');
const keys = [...zh.keys()].filter((k) => k.startsWith('Server')).sort();
console.log('keys:', keys.length, '| en 缺失:', keys.filter((k) => !en.has(k)).length);

const special = [];
for (const k of keys) {
  for (const [lang, map] of [['zh', zh], ['en', en]]) {
    const v = map.get(k) || '';
    if (/[{}|@]/.test(v)) special.push(`${lang}:${k} -> ${v}`);
  }
}
console.log('含 vue-i18n 保留字符的值:', special.length ? `\n${special.join('\n')}` : '无');

const zhOut = { server: {} };
const enOut = { server: {} };
for (const k of keys) {
  zhOut.server[k] = zh.get(k);
  enOut.server[k] = en.get(k) || zh.get(k);
}
zhOut.server.requestFailed = '请求失败';
zhOut.server.httpError = '服务异常 (HTTP {code})';
zhOut.server.networkError = '网络错误，请检查网络连接';
enOut.server.requestFailed = 'Request failed';
enOut.server.httpError = 'Service error (HTTP {code})';
enOut.server.networkError = 'Network error, please check your connection';

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
};
for (const [key, [zhText, enText]] of Object.entries(adminOnly)) {
  zhOut.server[key] = zhText;
  enOut.server[key] = enText;
}

fs.writeFileSync(OUT_ZH, `${JSON.stringify(zhOut, null, 2)}\n`);
fs.writeFileSync(OUT_EN, `${JSON.stringify(enOut, null, 2)}\n`);
JSON.parse(fs.readFileSync(OUT_ZH, 'utf8'));
JSON.parse(fs.readFileSync(OUT_EN, 'utf8'));
console.log('已生成', OUT_ZH, OUT_EN, '| server keys:', Object.keys(zhOut.server).length, '| JSON 合法');
