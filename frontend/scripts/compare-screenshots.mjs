#!/usr/bin/env node
// Compares two folders of screenshots (made by brand-screenshots.mjs) picture by picture and says which screens changed.
//   node scripts/compare-screenshots.mjs <beforeDir> <afterDir> [outDir] [--fail]
// For every changed picture, outDir (default ./screenshot-changes) gets the before, the after and a "diff" picture
// (the after picture faded to grey with the changed pixels in red), plus index.html to look at them side by side and
// summary.md. In GitHub Actions the summary is also added to the run's summary page (GITHUB_STEP_SUMMARY).
// A pixel counts as changed when a colour channel moved by more than TOLERANCE, so tiny anti-aliasing noise is ignored.
// --fail exits with code 1 when any picture changed, was added or was removed.
// Needs playwright (the pixels are read in headless Chromium, so no image library is needed).
import { appendFileSync, copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';

/** Channel difference (0-255) below which a pixel counts as unchanged. */
export const TOLERANCE = 24;

/** Every .png under `dir`, as paths relative to it with forward slashes, sorted. */
export function listPngs(dir) {
  if (!existsSync(dir)) return [];
  const found = [];
  const walk = (d) => {
    for (const e of readdirSync(d, { withFileTypes: true })) {
      const p = join(d, e.name);
      if (e.isDirectory()) walk(p);
      else if (e.name.endsWith('.png')) found.push(relative(dir, p).split('\\').join('/'));
    }
  };
  walk(dir);
  return found.sort();
}

/** Pairs up the two folders: pictures only in before (removed), only in after (added), and in both (to compare). */
export function pairUp(before, after) {
  const b = new Set(before);
  const a = new Set(after);
  return {
    removed: before.filter((p) => !a.has(p)),
    added: after.filter((p) => !b.has(p)),
    both: after.filter((p) => b.has(p)),
  };
}

/** The Markdown summary: counts, then one line per changed, added or removed picture. */
export function summaryMarkdown({ changed, added, removed, same }) {
  const lines = ['## Screenshot changes', ''];
  if (!changed.length && !added.length && !removed.length) {
    lines.push(`No screen looks different (${same} pictures compared).`);
    return lines.join('\n') + '\n';
  }
  lines.push(`${changed.length} changed, ${added.length} new, ${removed.length} gone, ${same} the same.`,
    'Download the **screenshot-changes** file below and open `index.html` to see before, after and the difference.', '');
  if (changed.length) {
    lines.push('| Changed screen | Pixels changed |', '| --- | --- |');
    for (const c of changed) lines.push(`| ${c.path} | ${c.note ?? `${c.percent.toFixed(2)}%`} |`);
    lines.push('');
  }
  if (added.length) lines.push(`New: ${added.join(', ')}`, '');
  if (removed.length) lines.push(`Gone: ${removed.join(', ')}`, '');
  return lines.join('\n') + '\n';
}

const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

/** A self-contained page that shows before, after and difference next to each other for every changed picture. */
export function reportHtml({ changed, added, removed, same }) {
  const pic = (src, label) => `<figure><figcaption>${label}</figcaption>${src ? `<a href="${esc(src)}"><img src="${esc(src)}" alt="${label}"></a>` : '<p>none</p>'}</figure>`;
  const rows = [
    ...changed.map((c) => `<section><h2>${esc(c.path)} <small>${esc(c.note ?? `${c.percent.toFixed(2)}% of pixels changed`)}</small></h2>
      <div class="row">${pic(`before/${c.path}`, 'Before')}${pic(`after/${c.path}`, 'After')}${pic(c.note ? '' : `diff/${c.path}`, 'Difference (red)')}</div></section>`),
    ...added.map((p) => `<section><h2>${esc(p)} <small>new</small></h2><div class="row">${pic(`after/${p}`, 'After')}</div></section>`),
    ...removed.map((p) => `<section><h2>${esc(p)} <small>gone</small></h2><div class="row">${pic(`before/${p}`, 'Before')}</div></section>`),
  ];
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Screenshot changes</title><style>
body { font: 14px/1.4 system-ui, sans-serif; margin: 16px; background: #f5f5f4; color: #1c1917; }
section { background: #fff; border-radius: 8px; padding: 12px; margin: 0 0 16px; }
h2 { font-size: 15px; margin: 0 0 8px; word-break: break-all; } small { font-weight: normal; color: #57534e; }
.row { display: flex; gap: 12px; overflow-x: auto; } figure { margin: 0; flex: 0 0 auto; }
figcaption { font-weight: 600; margin-bottom: 4px; } img { max-height: 640px; max-width: 360px; border: 1px solid #d6d3d1; }
</style></head><body><h1>Screenshot changes</h1>
<p>${changed.length} changed, ${added.length} new, ${removed.length} gone, ${same} the same. Click a picture to see it full size.</p>
${rows.join('\n')}</body></html>\n`;
}

/** Compares two PNG files in the browser; returns the share of changed pixels and a diff PNG, or a note when sizes differ. */
async function compare(page, beforeFile, afterFile) {
  const toUrl = (f) => `data:image/png;base64,${readFileSync(f).toString('base64')}`;
  return page.evaluate(async ([b, a, tolerance]) => {
    const load = async (src) => { const img = new Image(); img.src = src; await img.decode(); return img; };
    const [before, after] = await Promise.all([load(b), load(a)]);
    const { width: w, height: h } = after;
    if (before.width !== w || before.height !== h) return { note: `size changed from ${before.width}x${before.height} to ${w}x${h}` };
    const pixels = (img) => {
      const ctx = Object.assign(document.createElement('canvas'), { width: w, height: h }).getContext('2d', { willReadFrequently: true });
      ctx.drawImage(img, 0, 0);
      return ctx.getImageData(0, 0, w, h);
    };
    const pb = pixels(before).data;
    const pa = pixels(after);
    const d = pa.data;
    let changed = 0;
    for (let i = 0; i < d.length; i += 4) {
      const moved = Math.max(Math.abs(d[i] - pb[i]), Math.abs(d[i + 1] - pb[i + 1]), Math.abs(d[i + 2] - pb[i + 2]), Math.abs(d[i + 3] - pb[i + 3]));
      if (moved > tolerance) {
        changed++;
        d[i] = 230; d[i + 1] = 0; d[i + 2] = 0; d[i + 3] = 255;
      } else {
        const grey = 255 - (255 - (d[i] * 0.3 + d[i + 1] * 0.59 + d[i + 2] * 0.11)) * 0.25; // faded, so the red stands out
        d[i] = d[i + 1] = d[i + 2] = grey; d[i + 3] = 255;
      }
    }
    if (!changed) return { percent: 0 };
    const canvas = Object.assign(document.createElement('canvas'), { width: w, height: h });
    canvas.getContext('2d').putImageData(pa, 0, 0);
    return { percent: (changed / (w * h)) * 100, diff: canvas.toDataURL('image/png') };
  }, [toUrl(beforeFile), toUrl(afterFile), TOLERANCE]);
}

async function main() {
  const args = process.argv.slice(2);
  const [beforeDir, afterDir, outDir = join(process.cwd(), 'screenshot-changes')] = args.filter((a) => !a.startsWith('--'));
  if (!beforeDir || !afterDir) throw new Error('Usage: compare-screenshots.mjs <beforeDir> <afterDir> [outDir] [--fail]');
  if (!existsSync(afterDir)) throw new Error(`${afterDir} not found.`);
  if (!existsSync(beforeDir)) console.warn(`  warning: ${beforeDir} not found; every picture counts as new.`);
  const { removed, added, both } = pairUp(listPngs(beforeDir), listPngs(afterDir));
  const changed = [];
  let same = 0;

  const { chromium } = createRequire(import.meta.url)('playwright');
  const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || undefined });
  try {
    const page = await browser.newPage();
    for (const path of both) {
      const result = await compare(page, join(beforeDir, path), join(afterDir, path));
      if (!result.note && !result.percent) { same++; continue; }
      changed.push({ path, ...result });
      console.log(`changed: ${path} (${result.note ?? `${result.percent.toFixed(2)}% of pixels`})`);
    }
  } finally {
    await browser.close();
  }
  for (const p of added) console.log(`new: ${p}`);
  for (const p of removed) console.log(`gone: ${p}`);

  const result = { changed, added, removed, same };
  rmSync(outDir, { recursive: true, force: true });
  mkdirSync(outDir, { recursive: true });
  const put = (from, to) => { mkdirSync(dirname(to), { recursive: true }); copyFileSync(from, to); };
  for (const c of changed) {
    put(join(beforeDir, c.path), join(outDir, 'before', c.path));
    put(join(afterDir, c.path), join(outDir, 'after', c.path));
    if (c.diff) {
      mkdirSync(dirname(join(outDir, 'diff', c.path)), { recursive: true });
      writeFileSync(join(outDir, 'diff', c.path), Buffer.from(c.diff.split(',')[1], 'base64'));
      delete c.diff;
    }
  }
  for (const p of added) put(join(afterDir, p), join(outDir, 'after', p));
  for (const p of removed) put(join(beforeDir, p), join(outDir, 'before', p));
  const md = summaryMarkdown(result);
  writeFileSync(join(outDir, 'summary.md'), md);
  writeFileSync(join(outDir, 'index.html'), reportHtml(result));
  if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, md);
  console.log(`${changed.length} changed, ${added.length} new, ${removed.length} gone, ${same} the same; report in ${join(outDir, 'index.html')}`);
  if (args.includes('--fail') && (changed.length || added.length || removed.length)) process.exit(1);
}

// Run only as a script, so the test file can import the helpers above.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((e) => { console.error(`compare-screenshots: ${e.message}`); process.exit(1); });
}
