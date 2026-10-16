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

fs.writeFileSync(OUT_ZH, `${JSON.stringify(zhOut, null, 2)}\n`);
fs.writeFileSync(OUT_EN, `${JSON.stringify(enOut, null, 2)}\n`);
JSON.parse(fs.readFileSync(OUT_ZH, 'utf8'));
JSON.parse(fs.readFileSync(OUT_EN, 'utf8'));
console.log('已生成', OUT_ZH, OUT_EN, '| server keys:', Object.keys(zhOut.server).length, '| JSON 合法');
