#!/usr/bin/env node
// Screenshots the customer app's public screens for a few sample brands, to eyeball white-label changes.
// The brand is seeded through the same localStorage cache the app uses (key "yurt.brand"), so no backend is needed.
//   cd frontend && npx ng build yurt-customer --configuration production
//   node scripts/brand-screenshots.mjs [outDir] [--a11y] [--dark]
// --dark seeds the dark theme (localStorage key "yurt.theme") and adds "-dark" to the file names.
// --a11y also runs axe-core's colour-contrast and target-size (24px) rules on every screen and prints the failures (needs `axe-core`
// resolvable like playwright, e.g. NODE_PATH to a folder that has both).
import { createServer } from 'node:http';
import { existsSync, mkdirSync, readFileSync, statSync } from 'node:fs';
import { extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const root = join(fileURLToPath(new URL('.', import.meta.url)), '..', 'dist', 'yurt-customer', 'browser');
const args = process.argv.slice(2);
const a11y = args.includes('--a11y');
const dark = args.includes('--dark');
const out = args.find((a) => !a.startsWith('--')) ?? join(process.cwd(), 'brand-screenshots');
const SCREENS = ['/auth/login', '/auth/register'];

/** Sample brands: the Yurt default, a deep blue, a dark green with a long name, links and a welcome line, and a pale one. */
export const BRANDS = {
  yurt: { name: 'Yurt', primaryColor: '#ffd119', accentColor: '#ea580c' },
  'blue-cafe': { name: 'Blue Cafe', primaryColor: '#1d4ed8', accentColor: '#0ea5e9', welcomeText: 'Fresh coffee every morning', radius: 'square', font: 'serif' },
  'forest-roasters': {
    name: 'Forest Roasters & Bakery', primaryColor: '#14532d', accentColor: '#65a30d',
    welcomeText: 'Roasted in small batches', supportContact: '+7 700 000 00 00',
    links: [{ label: 'Instagram', url: 'https://instagram.com/forest' }],
  },
  'pale-bakery': { name: 'Pale Bakery', primaryColor: '#fde68a', accentColor: '#f59e0b' },
};

const TYPES = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css', '.json': 'application/json',
  '.png': 'image/png', '.jpg': 'image/jpeg', '.svg': 'image/svg+xml', '.ico': 'image/x-icon', '.woff2': 'font/woff2', '.mp4': 'video/mp4' };

function serve() {
  const server = createServer((req, res) => {
    const path = normalize(decodeURIComponent(new URL(req.url, 'http://x').pathname)).replace(/^(\.\.[/\\])+/, '');
    let file = join(root, path);
    if (!file.startsWith(root) || !existsSync(file) || statSync(file).isDirectory()) file = join(root, 'index.html'); // SPA fallback
    res.writeHead(200, { 'content-type': TYPES[extname(file)] ?? 'application/octet-stream' });
    res.end(readFileSync(file));
  });
  return new Promise((resolve) => server.listen(0, '127.0.0.1', () => resolve(server)));
}

/** Colour-contrast failures as `ratio fg on bg: text`, so a brand that breaks readability shows up in the log. */
async function checkContrast(page, label) {
  const require = createRequire(import.meta.url);
  await page.addScriptTag({ path: require.resolve('axe-core') });
  const results = await page.evaluate(() => window.axe.run(document, { runOnly: ['color-contrast', 'target-size'] }));
  const contrast = results.violations.filter((v) => v.id === 'color-contrast').flatMap((v) => v.nodes);
  const small = results.violations.filter((v) => v.id === 'target-size').flatMap((v) => v.nodes);
  console.log(`  ${label}: ${contrast.length} contrast failure(s), ${small.length} tap target(s) under 24px`);
  for (const n of contrast) {
    const d = n.any[0]?.data ?? {};
    console.log(`    ${d.contrastRatio ?? '?'} ${d.fgColor ?? '?'} on ${d.bgColor ?? '?'}: ${(d.messageKey ?? '') || n.target.join(' ')}`);
  }
  for (const n of small) console.log(`    small target: ${n.target.join(' ')}`);
}

async function main() {
  if (!existsSync(join(root, 'index.html'))) throw new Error(`Build first: ${root} not found (ng build yurt-customer --configuration production).`);
  const { chromium } = createRequire(import.meta.url)('playwright');
  mkdirSync(out, { recursive: true });
  const server = await serve();
  const base = `http://127.0.0.1:${server.address().port}`;
  const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || undefined });
  try {
    for (const [slug, brand] of Object.entries(BRANDS)) {
      const context = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2 });
      await context.addInitScript(([b, theme]) => {
        localStorage.setItem('yurt.brand', JSON.stringify(b));
        localStorage.setItem('yurt_lang', 'en');
        localStorage.setItem('yurt.theme', theme);
      }, [brand, dark ? 'dark' : 'light']);
      const page = await context.newPage();
      // Backend is not running: fail API calls fast instead of waiting.
      await page.route('**/api/**', (r) => r.abort());
      for (const screen of SCREENS) {
        await page.goto(base + screen, { waitUntil: 'load' });
        await page.waitForTimeout(2500); // intro video falls back, splash dismisses
        const file = join(out, `${slug}${screen.replace(/\//g, '-')}${dark ? '-dark' : ''}.png`);
        await page.screenshot({ path: file, fullPage: true });
        console.log(file);
        if (a11y) await checkContrast(page, `${slug}${screen}`);
      }
      await context.close();
    }
  } finally {
    await browser.close();
    server.close();
  }
}

main().catch((e) => { console.error(`brand-screenshots: ${e.message}`); process.exit(1); });
