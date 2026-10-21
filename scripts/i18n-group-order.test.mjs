import test from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const repo = join(dirname(fileURLToPath(import.meta.url)), '..');
const app = join(repo, 'frontend/projects/yurt-customer/src/app');
const dir = join(app, 'features/group-order');
const langs = ['en', 'ru', 'kk'];

/** The translation keys of each language block in the customer lang.service.ts. */
function keysByLang() {
  const src = readFileSync(join(app, 'core/lang.service.ts'), 'utf8');
  const out = {};
  for (const lang of langs) {
    const start = src.indexOf(`\n  ${lang}: {\n`);
    assert.ok(start >= 0, `no "${lang}" block`);
    const block = src.slice(start, src.indexOf('\n  },', start));
    out[lang] = new Set([...block.matchAll(/(?:^\s*|,\s*)'([^']+)':/gm)].map((m) => m[1]));
  }
  return out;
}

const files = readdirSync(dir).map((f) => ({ name: f, src: readFileSync(join(dir, f), 'utf8') }));
const html = files.filter((f) => f.name.endsWith('.html'));

test('group order: every group.* key exists in en, ru and kk', () => {
  const keys = keysByLang();
  const group = [...keys.en].filter((k) => k.startsWith('group.'));
  assert.ok(group.length > 0);
  for (const lang of langs) {
    const all = new Set([...keys.en, ...keys.ru, ...keys.kk].filter((k) => k.startsWith('group.')));
    assert.deepEqual([...all].filter((k) => !keys[lang].has(k)), [], `"${lang}" is missing group keys`);
  }
});

test('group order: every key the screens use exists', () => {
  const en = keysByLang().en;
  const used = files.flatMap((f) => [...f.src.matchAll(/'(group\.[a-zA-Z]+)'/g)].map((m) => `${f.name}: ${m[1]}`));
  assert.ok(used.length > 0);
  assert.deepEqual(used.filter((u) => !en.has(u.split(': ')[1])), []);
});

test('group order: templates have no hard-coded words', () => {
  const found = [];
  for (const f of html) {
    const text = f.src
      .replace(/<!--[\s\S]*?-->/g, '')
      .replace(/\{\{[\s\S]*?\}\}/g, '')
      .replace(/<[^>]*>/g, '\n');
    for (const line of text.split('\n')) {
      // Skip Angular block syntax like "@if (x) {" and "} @else {".
      const words = line.replace(/^\s*\}?\s*@[\s\S]*$/, '');
      if (/\p{L}{2,}/u.test(words)) found.push(`${f.name}: ${line.trim()}`);
    }
    // The join code example "ABC123" is a format sample, not a word to translate.
    for (const m of f.src.matchAll(/\splaceholder="([^"]*)"/g)) if (m[1] !== 'ABC123') found.push(`${f.name}: placeholder "${m[1]}"`);
  }
  assert.deepEqual(found, []);
});
