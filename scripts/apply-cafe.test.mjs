import test from 'node:test';
import assert from 'node:assert/strict';
import { cpSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { applyCafe, readCafe } from './apply-cafe.mjs';

const repo = join(dirname(fileURLToPath(import.meta.url)), '..');
const native = [
  'frontend/android/app/build.gradle',
  'frontend/android/app/src/main/res/values/strings.xml',
  'frontend/ios/App/App/Info.plist',
  'frontend/ios/App/App.xcodeproj/project.pbxproj',
];

/** A scratch copy of the native files plus a cafe, so the real working tree is never touched. */
function scratch(cafe, withSecrets = false) {
  const root = mkdtempSync(join(tmpdir(), 'apply-cafe-'));
  for (const f of native) {
    mkdirSync(dirname(join(root, f)), { recursive: true });
    cpSync(join(repo, f), join(root, f));
  }
  mkdirSync(join(root, 'frontend/android/app'), { recursive: true });
  mkdirSync(join(root, 'frontend/ios/App/App'), { recursive: true });
  mkdirSync(join(root, 'cafes/brew/secrets'), { recursive: true });
  writeFileSync(join(root, 'cafes/brew/cafe.json'), JSON.stringify(cafe));
  if (withSecrets) {
    writeFileSync(join(root, 'cafes/brew/secrets/google-services.json'), '{"android":true}');
    writeFileSync(join(root, 'cafes/brew/secrets/GoogleService-Info.plist'), '<plist/>');
  }
  return root;
}
const read = (root, f) => readFileSync(join(root, f), 'utf8');

test('applies id, name and version to Android and iOS', () => {
  const root = scratch({ appId: 'kz.brew.app', appName: 'Brew & Co', versionName: '2.1.0', versionCode: 7 });
  const { changes } = applyCafe({ root, slug: 'brew' });
  assert.deepEqual(changes.sort(), native.slice().sort());
  assert.match(read(root, native[0]), /applicationId "kz\.brew\.app"/);
  assert.match(read(root, native[0]), /versionCode 7/);
  assert.match(read(root, native[0]), /versionName "2\.1\.0"/);
  assert.match(read(root, native[0]), /namespace "com\.yurt\.app"/); // the Java package stays put
  assert.match(read(root, native[1]), /<string name="app_name">Brew &amp; Co</);
  assert.match(read(root, native[1]), /<string name="custom_url_scheme">kz\.brew\.app</);
  assert.match(read(root, native[2]), /<string>Brew &amp; Co<\/string>/);
  assert.equal((read(root, native[3]).match(/PRODUCT_BUNDLE_IDENTIFIER = kz\.brew\.app;/g) ?? []).length >= 2, true);
  assert.match(read(root, native[3]), /MARKETING_VERSION = 2\.1\.0;/);
});

test('is idempotent and a dry run changes nothing', () => {
  const root = scratch({ appId: 'kz.brew.app', appName: 'Brew' });
  const before = read(root, native[0]);
  assert.ok(applyCafe({ root, slug: 'brew', dryRun: true }).changes.length > 0);
  assert.equal(read(root, native[0]), before);
  applyCafe({ root, slug: 'brew' });
  assert.deepEqual(applyCafe({ root, slug: 'brew' }).changes, []);
});

test('copies the cafe Firebase files and reports missing ones', () => {
  const withSecrets = scratch({ appId: 'kz.brew.app', appName: 'Brew' }, true);
  const ok = applyCafe({ root: withSecrets, slug: 'brew' });
  assert.deepEqual(ok.missingSecrets, []);
  assert.equal(read(withSecrets, 'frontend/android/app/google-services.json'), '{"android":true}');
  const without = scratch({ appId: 'kz.brew.app', appName: 'Brew' });
  assert.deepEqual(applyCafe({ root: without, slug: 'brew' }).missingSecrets.sort(), ['GoogleService-Info.plist', 'google-services.json']);
  assert.equal(existsSync(join(without, 'frontend/android/app/google-services.json')), false);
});

test('rejects bad config', () => {
  for (const bad of [
    { appId: 'Kz.Brew', appName: 'X' }, { appId: 'brew', appName: 'X' }, { appId: 'kz.1brew', appName: 'X' },
    { appId: 'kz.brew', appName: '' }, { appId: 'kz.brew', appName: 'x'.repeat(31) },
    { appId: 'kz.brew', appName: 'X', versionName: '1.2' }, { appId: 'kz.brew', appName: 'X', versionCode: 0 },
  ]) assert.throws(() => readCafe(scratch(bad), 'brew'), undefined, JSON.stringify(bad));
  assert.throws(() => readCafe(scratch({ appId: 'kz.brew', appName: 'X' }), '../etc'));
});

test('the sample cafe in the repo is valid', () => {
  assert.equal(readCafe(repo, 'sample-cafe').appId, 'kz.samplecafe.app');
});
