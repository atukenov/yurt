#!/usr/bin/env node
// Applies cafes/<slug>/cafe.json to the Capacitor native projects (Android + iOS), copies the cafe's
// Firebase files from cafes/<slug>/secrets/ and its icon/splash sources from cafes/<slug>/assets/
// (--generate then makes every icon and splash size). See cafes/README.md.
import { spawnSync } from 'node:child_process';
import { existsSync, copyFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const APP_ID = /^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$/;
const VERSION_NAME = /^\d+\.\d+\.\d+$/;
const MERCHANT_ID = /^merchant\.[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$/;
const SLUG = /^[a-z0-9][a-z0-9-]*$/;
/** Source images @capacitor/assets understands; the first two are required for a branded build. */
const ASSETS = ['icon-only.png', 'splash.png', 'icon-foreground.png', 'icon-background.png', 'splash-dark.png'];
const REQUIRED_ASSETS = ['icon-only.png', 'splash.png'];

export function readCafe(root, slug) {
  if (!SLUG.test(slug)) throw new Error(`Invalid cafe name "${slug}" (use lowercase letters, digits and dashes).`);
  const file = join(root, 'cafes', slug, 'cafe.json');
  if (!existsSync(file)) throw new Error(`Missing ${file}`);
  const cafe = JSON.parse(readFileSync(file, 'utf8'));
  if (typeof cafe.appId !== 'string' || !APP_ID.test(cafe.appId))
    throw new Error('appId must look like kz.mycafe.app (lowercase, at least two dot-separated parts).');
  if (typeof cafe.appName !== 'string' || cafe.appName.trim().length < 1 || cafe.appName.length > 30)
    throw new Error('appName is required (max 30 characters).');
  if (cafe.versionName !== undefined && !VERSION_NAME.test(cafe.versionName))
    throw new Error('versionName must look like 1.2.3.');
  if (cafe.versionCode !== undefined && !(Number.isInteger(cafe.versionCode) && cafe.versionCode > 0))
    throw new Error('versionCode must be a positive whole number.');
  if (cafe.appleMerchantId !== undefined && !(typeof cafe.appleMerchantId === 'string' && MERCHANT_ID.test(cafe.appleMerchantId)))
    throw new Error('appleMerchantId must look like merchant.kz.mycafe.app.');
  if (cafe.tenant !== undefined && !(typeof cafe.tenant === 'string' && SLUG.test(cafe.tenant)))
    throw new Error('tenant must be the cafe key on the platform (lowercase letters, digits and dashes).');
  return { ...cafe, appName: cafe.appName.trim() };
}

const xml = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

/** Replaces `pattern` in `text`; throws when it does not match, so a changed native template is noticed. */
function must(text, pattern, replacement, what) {
  if (!pattern.test(text)) throw new Error(`Could not find ${what}; has the native project changed?`);
  return text.replace(pattern, replacement);
}

export function applyCafe({ root, slug, dryRun = false }) {
  const cafe = readCafe(root, slug);
  const changes = [];
  const edit = (relPath, fn) => {
    const file = join(root, relPath);
    const before = readFileSync(file, 'utf8');
    const after = fn(before);
    if (after !== before) {
      changes.push(relPath);
      if (!dryRun) writeFileSync(file, after);
    }
  };

  edit('frontend/android/app/build.gradle', (t) => {
    t = must(t, /applicationId "[^"]*"/, `applicationId "${cafe.appId}"`, 'applicationId');
    if (cafe.versionCode) t = must(t, /versionCode \d+/, `versionCode ${cafe.versionCode}`, 'versionCode');
    if (cafe.versionName) t = must(t, /versionName "[^"]*"/, `versionName "${cafe.versionName}"`, 'versionName');
    return t;
  });
  edit('frontend/android/app/src/main/res/values/strings.xml', (t) => {
    t = must(t, /(<string name="app_name">)[^<]*/, `$1${xml(cafe.appName)}`, 'app_name');
    t = must(t, /(<string name="title_activity_main">)[^<]*/, `$1${xml(cafe.appName)}`, 'title_activity_main');
    t = must(t, /(<string name="package_name">)[^<]*/, `$1${cafe.appId}`, 'package_name');
    return must(t, /(<string name="custom_url_scheme">)[^<]*/, `$1${cafe.appId}`, 'custom_url_scheme');
  });
  edit('frontend/ios/App/App/Info.plist', (t) =>
    must(t, /(<key>CFBundleDisplayName<\/key>\s*<string>)[^<]*/, `$1${xml(cafe.appName)}`, 'CFBundleDisplayName'));
  edit('frontend/ios/App/App.xcodeproj/project.pbxproj', (t) => {
    t = must(t, /PRODUCT_BUNDLE_IDENTIFIER = [^;]*;/g, `PRODUCT_BUNDLE_IDENTIFIER = ${cafe.appId};`, 'PRODUCT_BUNDLE_IDENTIFIER');
    return cafe.versionName ? must(t, /MARKETING_VERSION = [^;]*;/g, `MARKETING_VERSION = ${cafe.versionName};`, 'MARKETING_VERSION') : t;
  });

  if (cafe.appleMerchantId) {
    edit('frontend/ios/App/App/App.entitlements', (t) =>
      must(t, /(<key>com\.apple\.developer\.in-app-payments<\/key>\s*<array>\s*<string>)[^<]*/, `$1${cafe.appleMerchantId}`, 'in-app-payments merchant id'));
  }

  if (cafe.tenant) {
    for (const app of ['yurt-customer', 'yurt-admin']) {
      edit(`frontend/projects/${app}/src/environments/environment.ts`, (t) => {
        t = t.replace(/^  tenant: '[^']*',\n/m, '');
        return must(t, /(  apiUrl: [^\n]*\n)/, `$1  tenant: '${cafe.tenant}',\n`, 'apiUrl in environment.ts');
      });
    }
  }

  const secrets = [
    ['google-services.json', 'frontend/android/app/google-services.json'],
    ['GoogleService-Info.plist', 'frontend/ios/App/App/GoogleService-Info.plist'],
  ];
  const missing = [];
  for (const [name, target] of secrets) {
    const source = join(root, 'cafes', slug, 'secrets', name);
    if (existsSync(source)) {
      changes.push(target);
      if (!dryRun) copyFileSync(source, join(root, target));
    } else missing.push(name);
  }

  const missingAssets = [];
  for (const name of ASSETS) {
    const source = join(root, 'cafes', slug, 'assets', name);
    if (existsSync(source)) {
      changes.push(`frontend/assets/${name}`);
      if (!dryRun) {
        mkdirSync(join(root, 'frontend/assets'), { recursive: true });
        copyFileSync(source, join(root, 'frontend/assets', name));
      }
    } else if (REQUIRED_ASSETS.includes(name)) missingAssets.push(name);
  }
  return { cafe, changes, missingSecrets: missing, missingAssets };
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const [slug, ...flags] = process.argv.slice(2);
  if (!slug) {
    console.error('Usage: node scripts/apply-cafe.mjs <cafe> [--dry-run]');
    process.exit(2);
  }
  try {
    const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
    const dryRun = flags.includes('--dry-run');
    const generate = flags.includes('--generate');
    const { cafe, changes, missingSecrets, missingAssets } = applyCafe({ root, slug, dryRun });
    console.log(`${dryRun ? 'Would apply' : 'Applied'} ${slug}: ${cafe.appName} (${cafe.appId})`);
    for (const c of changes) console.log(`  ${dryRun ? 'would change' : 'changed'} ${c}`);
    if (!changes.length) console.log('  nothing to change (already applied)');
    for (const m of missingSecrets) console.warn(`  warning: cafes/${slug}/secrets/${m} not found; push notifications will not work in this build`);
    for (const m of missingAssets) console.warn(`  warning: cafes/${slug}/assets/${m} not found; the app keeps the default ${m.startsWith('icon') ? 'icon' : 'splash'}`);
    if (generate && !dryRun) {
      if (missingAssets.length) throw new Error('--generate needs icon-only.png and splash.png in the cafe assets folder.');
      const r = spawnSync('npx', ['--yes', '@capacitor/assets@3.0.5', 'generate', '--android', '--ios'], {
        cwd: join(root, 'frontend'), stdio: 'inherit', shell: process.platform === 'win32',
      });
      if (r.status !== 0) throw new Error('@capacitor/assets failed.');
    }
  } catch (e) {
    console.error(`apply-cafe: ${e.message}`);
    process.exit(1);
  }
}
