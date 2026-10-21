import test from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const repo = join(dirname(fileURLToPath(import.meta.url)), '..');
const apps = ['customer', 'admin'];
const langs = ['en', 'ru', 'kk'];

/** The translation keys of each language block in an app's lang.service.ts. */
function keysByLang(app) {
  const src = readFileSync(join(repo, `frontend/projects/yurt-${app}/src/app/core/lang.service.ts`), 'utf8');
  const out = {};
  for (const lang of langs) {
    const start = src.indexOf(`\n  ${lang}: {\n`);
    assert.ok(start >= 0, `${app}: no "${lang}" block`);
    const block = src.slice(start, src.indexOf('\n  },', start));
    out[lang] = new Set([...block.matchAll(/(?:^\s*|,\s*)'([^']+)':/gm)].map((m) => m[1]));
  }
  return out;
}

function htmlFiles(dir) {
  return readdirSync(dir, { withFileTypes: true }).flatMap((e) =>
    e.isDirectory() ? htmlFiles(join(dir, e.name)) : e.name.endsWith('.html') ? [join(dir, e.name)] : []);
}

for (const app of apps) {
  test(`${app}: every language has the same keys`, () => {
    const keys = keysByLang(app);
    for (const a of langs) for (const b of langs) {
      const missing = [...keys[a]].filter((k) => !keys[b].has(k));
      assert.deepEqual(missing, [], `${app}: "${b}" is missing keys that "${a}" has`);
    }
  });
}

const customerApp = join(repo, 'frontend/projects/yurt-customer/src/app');

test('customer: every key used in a template exists', () => {
  const en = keysByLang('customer').en;
  const unknown = [];
  for (const f of htmlFiles(customerApp)) {
    for (const m of readFileSync(f, 'utf8').matchAll(/'([a-zA-Z0-9]+\.[a-zA-Z0-9.]+)'\s*\|\s*t\b/g)) {
      if (!en.has(m[1])) unknown.push(`${f.slice(customerApp.length + 1)}: ${m[1]}`);
    }
  }
  assert.deepEqual(unknown, []);
});

// Language names are shown in their own language on purpose, so people can find theirs.
const allowed = new Set(['Русский', 'Қазақша']);

test('customer: templates have no hard-coded Russian or Kazakh text', () => {
  const found = [];
  for (const f of htmlFiles(customerApp)) {
    const html = readFileSync(f, 'utf8').replace(/<!--[\s\S]*?-->/g, '');
    for (const m of html.matchAll(/[А-Яа-яЁёӘәҒғҚқҢңӨөҰұҮүҺһІі][А-Яа-яЁёӘәҒғҚқҢңӨөҰұҮүҺһІі\s-]*/g)) {
      if (!allowed.has(m[0].trim())) found.push(`${f.slice(customerApp.length + 1)}: ${m[0].trim()}`);
    }
  }
  assert.deepEqual(found, []);
});
