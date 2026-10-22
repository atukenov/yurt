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

function tsFiles(dir) {
  return readdirSync(dir, { withFileTypes: true }).flatMap((e) =>
    e.isDirectory() ? tsFiles(join(dir, e.name))
      : e.name.endsWith('.ts') && !e.name.endsWith('.spec.ts') && e.name !== 'lang.service.ts' ? [join(dir, e.name)] : []);
}

// A key like 'orders.items.few' is one plural form of 'orders.items'.
const pluralForm = /\.(zero|one|two|few|many|other)$/;
const baseKey = (k) => k.replace(pluralForm, '');

for (const app of apps) {
  test(`${app}: every language has the same keys`, () => {
    const keys = keysByLang(app);
    for (const a of langs) for (const b of langs) {
      const have = new Set([...keys[b]].map(baseKey));
      const missing = [...new Set([...keys[a]].map(baseKey))].filter((k) => !have.has(k));
      assert.deepEqual(missing, [], `${app}: "${b}" is missing keys that "${a}" has`);
    }
  });

  test(`${app}: plural keys have every form their language needs`, () => {
    const keys = keysByLang(app);
    const missing = [];
    for (const lang of langs) {
      const forms = new Intl.PluralRules(lang).resolvedOptions().pluralCategories;
      // A key with a .one/.few/.many form is plural ('orders.other' on its own is just a word).
      const plural = new Set([...keys[lang]].filter((k) => /\.(one|few|many)$/.test(k)).map(baseKey));
      for (const k of plural) for (const f of forms) if (!keys[lang].has(`${k}.${f}`)) missing.push(`${lang}: ${k}.${f}`);
    }
    assert.deepEqual(missing, []);
  });
}

const customerApp = join(repo, 'frontend/projects/yurt-customer/src/app');

test('customer: every key used in a template or component exists', () => {
  const en = keysByLang('customer').en;
  const unknown = [];
  for (const f of htmlFiles(customerApp)) {
    for (const m of readFileSync(f, 'utf8').matchAll(/'([a-zA-Z0-9]+\.[a-zA-Z0-9.]+)'\s*\|\s*t\b/g)) {
      if (!en.has(m[1]) && !en.has(`${m[1]}.other`)) unknown.push(`${f.slice(customerApp.length + 1)}: ${m[1]}`);
    }
  }
  for (const f of tsFiles(customerApp)) {
    for (const m of readFileSync(f, 'utf8').matchAll(/\.t\('([a-zA-Z0-9]+\.[a-zA-Z0-9.]+)'/g)) {
      if (!en.has(m[1]) && !en.has(`${m[1]}.other`)) unknown.push(`${f.slice(customerApp.length + 1)}: ${m[1]}`);
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

/** Text a person would read in a template: text between tags and plain placeholder/title/aria-label/alt values. */
function readableText(html) {
  html = html
    .replace(/<!--[\s\S]*?-->/g, '')
    .replace(/<(style|script)\b[\s\S]*?<\/\1>/g, '');
  const out = [];
  // Words in quotes inside {{ }}, like {{ busy ? 'Saving…' : 'Save' }}, unless they go through | t.
  for (const expr of html.matchAll(/\{\{([\s\S]*?)\}\}/g)) {
    for (const q of expr[1].matchAll(/'([A-Z][a-z][^']*)'(?!\s*\|\s*t\b)/g)) out.push(q[1]);
  }
  html = html.replace(/\{\{[\s\S]*?\}\}/g, ' ');
  for (const tag of html.matchAll(/<(?:[^>"']|"[^"]*"|'[^']*')*>/g)) {
    for (const a of tag[0].matchAll(/\s(?:placeholder|title|aria-label|alt)="([^"]*)"/g)) out.push(a[1]);
  }
  const text = html
    .replace(/<(?:[^>"']|"[^"]*"|'[^']*')*>/g, '\n')
    // Angular control flow: @if (...) {, @for (...) {, } @else {, @let x = ...;
    .replace(/@(?:if|else if|for|switch|case)\s*\((?:[^()]|\((?:[^()]|\([^()]*\))*\))*\)\s*\{?/g, ' ')
    .replace(/@(?:else|empty|default|defer|placeholder|loading)\b\s*\{?/g, ' ')
    .replace(/@let\b[^;]*;/g, ' ')
    .replace(/&[a-z]+;/g, ' ');
  return out.concat(text.split('\n'));
}

/** The templates of an app: .html files plus inline `template:` strings in components. */
function templates(dir) {
  const html = htmlFiles(dir).map((f) => [f, readFileSync(f, 'utf8')]);
  const inline = tsFiles(dir).flatMap((f) =>
    [...readFileSync(f, 'utf8').matchAll(/template:\s*`([\s\S]*?)`/g)].map((m) => [f, m[1]]));
  return html.concat(inline);
}

// Brand and bank names, units and example input that read the same in every language.
const englishAllowed = new Set([
  'Kaspi', 'Halyk', 'Freedom', 'Kaspi Bank', 'Halyk Bank', 'Freedom Bank', 'Apple Pay',
  'Sara', 'Ali', 'ABC123', 'EN', 'RU', 'KZ', 'English', 'https://', 'https://...', 'https://2gis.kz/…',
  'https://.../font.woff2', 'SWK00000000', 'e.g. SUMMER20', 'e.g. admin', 'Admin',
]);

function hardCodedEnglish(dir) {
  const found = [];
  for (const [f, tpl] of templates(dir)) {
    for (const line of readableText(tpl)) {
      const t = line.trim().replace(/\s+/g, ' ');
      if (/[A-Za-z]{2,}/.test(t) && !englishAllowed.has(t)) found.push(`${f.slice(dir.length + 1)}: ${t}`);
    }
  }
  return found;
}

test('customer: templates have no hard-coded English text', () => {
  assert.deepEqual(hardCodedEnglish(customerApp), []);
});

test('customer: toasts and form errors are translated', () => {
  const found = [];
  for (const f of tsFiles(customerApp)) {
    const src = readFileSync(f, 'utf8');
    // toast.x('Text'), error.set('Text'), x ?? 'Some text' (a translation key like 'group.x' is fine).
    for (const m of src.matchAll(/(?:toast\.\w+\(|[eE]rror\.set\(|(\?\?))\s*(['`])((?:[A-Za-z]|\$\{)[^'`]*)\2/g)) {
      const [, fallback, , text] = m;
      if (/^[a-z]+\.[a-zA-Z.]+$/.test(text) || (fallback && !text.includes(' '))) continue;
      found.push(`${f.slice(customerApp.length + 1)}: ${text}`);
    }
  }
  assert.deepEqual(found, []);
});

// The admin app still has English left (sign-in, menu editor, iiko setup...). This number may only go down:
// when you translate more, lower it to what the failure message says.
const adminEnglishLeft = 98;

test('admin: hard-coded English in templates does not grow', () => {
  const found = hardCodedEnglish(join(repo, 'frontend/projects/yurt-admin/src/app'));
  assert.ok(found.length <= adminEnglishLeft, `${found.length} English bits in admin templates (limit ${adminEnglishLeft}):\n${found.join('\n')}`);
});
