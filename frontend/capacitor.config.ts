import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import type { CapacitorConfig } from '@capacitor/cli';

// Defaults describe the Yurt platform app. A cafe's branded build picks its app id and name from
// cafes/<slug>/cafe.json (see cafes/README.md):
//   YURT_CAFE=mycafe npx cap sync
// YURT_APP_ID / YURT_APP_NAME still override single values.
function cafeFile(): { appId?: string; appName?: string } {
  const slug = process.env['YURT_CAFE'];
  if (!slug) return {};
  if (!/^[a-z0-9][a-z0-9-]*$/.test(slug)) throw new Error(`Invalid YURT_CAFE "${slug}"`);
  const file = join(__dirname, '..', 'cafes', slug, 'cafe.json');
  if (!existsSync(file)) throw new Error(`Missing ${file}`);
  return JSON.parse(readFileSync(file, 'utf8'));
}

const cafe = cafeFile();

const config: CapacitorConfig = {
  appId: process.env['YURT_APP_ID'] ?? cafe.appId ?? 'com.yurt.app',
  appName: process.env['YURT_APP_NAME'] ?? cafe.appName ?? 'Yurt',
  webDir: 'dist/yurt-customer/browser'
};

export default config;
