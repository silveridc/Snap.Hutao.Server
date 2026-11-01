// 把 TinyMCE 的运行时资源复制到 public/，避免从 CDN 加载
import { cp, mkdir, rm } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const from = path.join(root, 'node_modules/tinymce');
const to = path.join(root, 'public/tinymce');
const entries = ['tinymce.min.js', 'license.txt', 'themes', 'models', 'icons', 'skins', 'plugins'];

if (!existsSync(from)) {
  console.error('tinymce is not installed, run pnpm install first');
  process.exit(1);
}

await rm(to, { recursive: true, force: true });
await mkdir(to, { recursive: true });
for (const entry of entries) {
  if (!existsSync(path.join(from, entry))) continue;
  await cp(path.join(from, entry), path.join(to, entry), { recursive: true });
}

console.log(`TinyMCE assets copied to ${path.relative(root, to)}`);
