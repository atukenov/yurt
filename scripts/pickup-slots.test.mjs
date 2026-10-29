import test from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

// The customer app's schedule helpers, compiled on the fly with the frontend's TypeScript.
const repo = join(dirname(fileURLToPath(import.meta.url)), '..');
const ts = createRequire(join(repo, 'frontend/package.json'))('typescript');
const source = readFileSync(join(repo, 'frontend/projects/yurt-customer/src/app/core/schedule.ts'), 'utf8');
const js = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022 } }).outputText;
const { pickupSlots } = await import(`data:text/javascript;base64,${Buffer.from(js).toString('base64')}`);

// Every day 08:00–22:00, in the JSON shape the admin app saves.
const daily = JSON.stringify(Array.from({ length: 7 }, (_, i) => ({ day: i + 1, enabled: true, from: '08:00', to: '22:00' })));
const hhmm = (d) => `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
// Wednesday 28 Oct 2026, local time.
const at = (h, m) => new Date(2026, 9, 28, h, m);

test('pickup times start 20 minutes ahead, on the quarter hour, and end at closing', () => {
  const slots = pickupSlots(daily, at(13, 2)).map(hhmm);
  assert.equal(slots[0], '13:30');
  assert.equal(slots[1], '13:45');
  assert.equal(slots.at(-1), '22:00');
});

test('before opening, the first pickup time is the opening time', () => {
  assert.equal(hhmm(pickupSlots(daily, at(6, 0))[0]), '08:00');
});

test('no pickup times are left close to closing, or on a closed day', () => {
  assert.deepEqual(pickupSlots(daily, at(21, 50)), []);
  const closedWednesday = JSON.parse(daily);
  closedWednesday[2].enabled = false;
  assert.deepEqual(pickupSlots(JSON.stringify(closedWednesday), at(12, 0)), []);
});

test('hours past midnight keep going after midnight', () => {
  const late = JSON.stringify(Array.from({ length: 7 }, (_, i) => ({ day: i + 1, enabled: true, from: '18:00', to: '02:00' })));
  const slots = pickupSlots(late, at(23, 0));
  assert.equal(hhmm(slots.at(-1)), '02:00');
  assert.equal(slots.at(-1).getDate(), 29);
});

test('hours written as plain text that cannot be read give a few hours, until midnight', () => {
  const slots = pickupSlots('ask the barista', at(20, 0)).map(hhmm);
  assert.equal(slots[0], '20:30');
  assert.equal(slots.at(-1), '00:00');
});
