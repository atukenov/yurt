import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

// Tax receipt link on the customer order screen. Fold into scripts/i18n.test.mjs once that file is on main.
const repo = join(dirname(fileURLToPath(import.meta.url)), '..');
const app = join(repo, 'frontend/projects/yurt-customer/src/app');
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

test('tax receipt: every order.fiscal* key the order screen uses exists in en, ru and kk', () => {
  const keys = keysByLang();
  const html = readFileSync(join(app, 'features/orders/order-detail.component.html'), 'utf8');
  const used = [...new Set([...html.matchAll(/'(order\.fiscal[a-zA-Z]+)'/g)].map((m) => m[1]))];
  assert.deepEqual(used.sort(), ['order.fiscalOpen', 'order.fiscalReceipt', 'order.fiscalReturn']);
  for (const lang of langs) assert.deepEqual(used.filter((k) => !keys[lang].has(k)), [], `"${lang}" is missing keys`);
});
