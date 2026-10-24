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
const adminApp = join(repo, 'frontend/projects/yurt-admin/src/app');

for (const [app, dir] of [['customer', customerApp], ['admin', adminApp]]) test(`${app}: every key used in a template or component exists`, () => {
  const en = keysByLang(app).en;
  const unknown = [];
  for (const f of htmlFiles(dir)) {
    for (const m of readFileSync(f, 'utf8').matchAll(/'([a-zA-Z0-9]+\.[a-zA-Z0-9.]+)'\s*\|\s*t\b/g)) {
      if (!en.has(m[1]) && !en.has(`${m[1]}.other`)) unknown.push(`${f.slice(dir.length + 1)}: ${m[1]}`);
    }
  }
  for (const f of tsFiles(dir)) {
    for (const m of readFileSync(f, 'utf8').matchAll(/\.t\('([a-zA-Z0-9]+\.[a-zA-Z0-9.]+)'/g)) {
      if (!en.has(m[1]) && !en.has(`${m[1]}.other`)) unknown.push(`${f.slice(dir.length + 1)}: ${m[1]}`);
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
    // A value compared with === (like kind === 'Return') is data, not text.
    for (const q of expr[1].matchAll(/(?<![=!]==\s*)'([A-Z][a-z][^']*)'(?!\s*\|\s*t\b)/g)) out.push(q[1]);
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

for (const [app, dir] of [['customer', customerApp], ['admin', adminApp]]) {
  test(`${app}: templates have no hard-coded English text`, () => {
    assert.deepEqual(hardCodedEnglish(dir), []);
  });
}

/** Toasts, form errors, fallbacks and confirm() questions in an app's .ts files that are plain English. */
function untranslatedMessages(dir) {
  const found = [];
  for (const f of tsFiles(dir)) {
    const src = readFileSync(f, 'utf8');
    // toast.x('Text'), error.set('Text'), x ?? 'Some text' (a translation key like 'group.x' is fine).
    for (const m of src.matchAll(/(?:toast\.\w+\(|[eE]rror\.set\(|confirm\(|(\?\?))\s*(['`])((?:[A-Za-z]|\$\{)[^'`]*)\2/g)) {
      const [, fallback, , text] = m;
      // CSS classes like 'bg-slate-100 text-slate-700' are not text either.
      if (/^[a-z]+\.[a-zA-Z.]+$/.test(text) || (fallback && !text.includes(' ')) || /^([a-z]+-[\w-]+\s*)+$/.test(text)) continue;
      found.push(`${f.slice(dir.length + 1)}: ${text}`);
    }
  }
  return found;
}

for (const [app, dir] of [['customer', customerApp], ['admin', adminApp]]) {
  test(`${app}: toasts, form errors and confirm questions are translated`, () => {
    assert.deepEqual(untranslatedMessages(dir), []);
  });
}

const groupOrder = join(customerApp, 'features/group-order');

test('customer: every key the group order screens use exists', () => {
  const en = keysByLang('customer').en;
  const used = readdirSync(groupOrder).flatMap((f) =>
    [...readFileSync(join(groupOrder, f), 'utf8').matchAll(/'(group\.[a-zA-Z]+)'/g)].map((m) => [f, m[1]]));
  assert.ok(used.length > 0);
  assert.deepEqual(used.filter(([, k]) => !en.has(k)).map(([f, k]) => `${f}: ${k}`), []);
});

test('customer: group order templates have no hard-coded English words', () => {
  const found = [];
  for (const f of htmlFiles(groupOrder)) {
    const name = f.slice(groupOrder.length + 1);
    const src = readFileSync(f, 'utf8');
    const text = src.replace(/<!--[\s\S]*?-->/g, '').replace(/\{\{[\s\S]*?\}\}/g, '').replace(/<[^>]*>/g, '\n');
    for (const line of text.split('\n')) {
      // Skip Angular block syntax like "@if (x) {" and "} @else {".
      const words = line.replace(/^\s*\}?\s*@[\s\S]*$/, '');
      if (/\p{L}{2,}/u.test(words)) found.push(`${name}: ${line.trim()}`);
    }
    // The join code example "ABC123" is a format sample, not a word to translate.
    for (const m of src.matchAll(/\splaceholder="([^"]*)"/g)) if (m[1] !== 'ABC123') found.push(`${name}: placeholder "${m[1]}"`);
  }
  assert.deepEqual(found, []);
});
