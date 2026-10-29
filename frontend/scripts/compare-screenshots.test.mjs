import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { listPngs, pairUp, reportHtml, summaryMarkdown } from './compare-screenshots.mjs';

test('listPngs finds pictures in sub-folders, with forward slashes, sorted', () => {
  const dir = mkdtempSync(join(tmpdir(), 'shots-'));
  mkdirSync(join(dir, 'store', 'ru'), { recursive: true });
  for (const f of ['b.png', 'a.png', 'notes.txt', 'store/ru/01-menu.png']) writeFileSync(join(dir, f), '');
  assert.deepEqual(listPngs(dir), ['a.png', 'b.png', 'store/ru/01-menu.png']);
  assert.deepEqual(listPngs(join(dir, 'missing')), []);
});

test('pairUp splits pictures into gone, new and both', () => {
  assert.deepEqual(pairUp(['a.png', 'b.png'], ['b.png', 'c.png']), { removed: ['a.png'], added: ['c.png'], both: ['b.png'] });
});

test('summary says nothing changed when nothing did', () => {
  const md = summaryMarkdown({ changed: [], added: [], removed: [], same: 12 });
  assert.match(md, /No screen looks different \(12 pictures compared\)/);
});

test('summary lists changed screens with their share or size note', () => {
  const md = summaryMarkdown({
    changed: [{ path: 'yurt-menu.png', percent: 1.234 }, { path: 'yurt-cart.png', note: 'size changed from 1x1 to 1x2' }],
    added: ['store/x.png'], removed: [], same: 3,
  });
  assert.match(md, /2 changed, 1 new, 0 gone, 3 the same/);
  assert.match(md, /\| yurt-menu.png \| 1.23% \|/);
  assert.match(md, /\| yurt-cart.png \| size changed from 1x1 to 1x2 \|/);
  assert.match(md, /New: store\/x.png/);
});

test('report escapes file names and links before, after and diff pictures', () => {
  const html = reportHtml({ changed: [{ path: 'a<b>.png', percent: 5 }], added: [], removed: [], same: 0 });
  assert.match(html, /a&lt;b&gt;\.png/);
  assert.match(html, /before\/a&lt;b&gt;\.png/);
  assert.match(html, /diff\/a&lt;b&gt;\.png/);
});
