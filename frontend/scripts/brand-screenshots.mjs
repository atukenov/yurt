#!/usr/bin/env node
// Screenshots the customer app's screens for a few sample brands, to eyeball white-label changes.
// The brand is seeded through the same localStorage cache the app uses (key "yurt.brand"), so no backend is needed:
// the menu, cart and order status screens get a signed-in user, a cart and fake API answers (MOCK_API below).
//   cd frontend && npx ng build yurt-customer --configuration production
//   node scripts/brand-screenshots.mjs [outDir] [--a11y] [--dark] [--fail]
// --dark seeds the dark theme (localStorage key "yurt.theme") and adds "-dark" to the file names.
// --a11y also runs axe-core's colour-contrast and target-size (24px) rules on every screen and prints the failures (needs `axe-core`
// resolvable like playwright, e.g. NODE_PATH to a folder that has both).
// --fail exits with code 1 when any a11y failure was found (CI runs it this way).
import { createServer } from 'node:http';
import { existsSync, mkdirSync, readFileSync, statSync } from 'node:fs';
import { extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const root = join(fileURLToPath(new URL('.', import.meta.url)), '..', 'dist', 'yurt-customer', 'browser');
const args = process.argv.slice(2);
const a11y = args.includes('--a11y');
const dark = args.includes('--dark');
const failOnIssues = args.includes('--fail');
const out = args.find((a) => !a.startsWith('--')) ?? join(process.cwd(), 'brand-screenshots');
/** Public screens open signed out; the others need the signed-in user and fake API below. */
const PUBLIC_SCREENS = ['/auth/login', '/auth/register'];
const SIGNED_IN_SCREENS = ['/menu', '/cart', '/orders/o-1', '/orders/o-2'];
let failures = 0;

const LOCATION = { id: 'loc-1', name: 'Main Street', address: '1 Main Street', workingHours: '', contactPhone: '+7 700 000 00 01', isActive: true };
const ITEM = (id, name, price, extra = {}) => ({
  id, categoryId: 'cat-1', categoryName: 'Coffee', name, description: 'Double shot with steamed milk', price,
  isAvailable: true, isNew: false, ...extra,
});
/** Fake answers for the API calls these screens make, keyed by path after /api/v1. Unknown calls are aborted. */
const MOCK_API = {
  '/locations': [LOCATION],
  '/menu/categories': [{ id: 'cat-1', name: 'Coffee', sortOrder: 1 }, { id: 'cat-2', name: 'Bakery', sortOrder: 2 }],
  '/menu': [
    ITEM('m-1', 'Cappuccino', 1200, { isNew: true }),
    ITEM('m-2', 'Flat white', 1400),
    ITEM('m-3', 'Raf', 1600, { isAvailable: false }),
    ITEM('m-4', 'Croissant', 900, { categoryId: 'cat-2', categoryName: 'Bakery', description: 'Butter croissant' }),
  ],
  '/favorites': [],
  '/promotions/active': [],
  '/loyalty/me': { enabled: true, available: true, linked: true, balance: 350, earnPercent: 5 },
  '/orders/o-1': {
    id: 'o-1', customerUserId: 'u-1', customerName: 'Aru', locationId: 'loc-1', locationName: 'Main Street',
    status: 'Preparing', etaMinutes: 10, createdAt: '2026-10-21T09:00:00Z', acceptedAt: '2026-10-21T09:01:00Z',
    paymentStatus: 'Paid', paymentMethod: 'Card', subtotal: 3800, discountAmount: 200, total: 3600, discountCode: 'HELLO',
    items: [
      { id: 'oi-1', menuItemId: 'm-1', menuItemName: 'Cappuccino', quantity: 2, unitPrice: 1200, lineTotal: 2400, toppings: [], variantLabel: 'Large' },
      { id: 'oi-2', menuItemId: 'm-2', menuItemName: 'Flat white', quantity: 1, unitPrice: 1400, lineTotal: 1400, toppings: [] },
    ],
  },
};
/** A second order status: just placed, not paid yet, paid partly with bonuses (shows the cancel button and waiting notes). */
MOCK_API['/orders/o-2'] = {
  ...MOCK_API['/orders/o-1'], id: 'o-2', status: 'Created', etaMinutes: undefined, acceptedAt: undefined,
  paymentStatus: 'Unpaid', paymentMethod: 'KaspiBank', discountCode: undefined, discountAmount: 0, total: 3500,
  loyaltyPointsSpent: 300, loyaltyPointsEarned: 175,
};
const SIGNED_IN_STORAGE = {
  yurt_user: JSON.stringify({ accessToken: 'a11y', refreshToken: '', userId: 'u-1', displayName: 'Aru', userType: 'Customer' }),
  yurt_location_id: LOCATION.id,
  yurt_location_name: LOCATION.name,
  yurt_cart: JSON.stringify([
    { menuItemId: 'm-1', name: 'Cappuccino', price: 1200, quantity: 2, variantId: 'v-1', variantLabel: 'Large' },
    { menuItemId: 'm-4', name: 'Croissant', price: 900, quantity: 1, notes: 'Warm it up' },
  ]),
};

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
  failures += contrast.length + small.length;
  console.log(`  ${label}: ${contrast.length} contrast failure(s), ${small.length} tap target(s) under 24px`);
  for (const n of contrast) {
    const d = n.any[0]?.data ?? {};
    console.log(`    ${d.contrastRatio ?? '?'} ${d.fgColor ?? '?'} on ${d.bgColor ?? '?'}: ${(d.messageKey ?? '') || n.target.join(' ')}`);
  }
  for (const n of small) console.log(`    small target: ${n.target.join(' ')}`);
}

/**
 * axe skips text on gradients, so the brand headers (text-brand-hero on a brand-500 to brand-600 gradient) are measured here:
 * each text colour is blended over both gradient ends on a canvas and the lower contrast is kept.
 * The default Yurt look keeps white text on its yellow on purpose (see --brand-hero-text), so it is reported but not counted.
 */
async function checkHeroText(page, label, isDefaultBrand) {
  const low = await page.evaluate(() => {
    const ctx = Object.assign(document.createElement('canvas'), { width: 1, height: 1 }).getContext('2d', { willReadFrequently: true });
    const paint = (bg, fg) => {
      ctx.clearRect(0, 0, 1, 1);
      for (const c of [bg, fg]) { ctx.fillStyle = c; ctx.fillRect(0, 0, 1, 1); }
      return [...ctx.getImageData(0, 0, 1, 1).data.slice(0, 3)];
    };
    const lum = (rgb) => rgb.map((v) => v / 255).map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4))
      .reduce((sum, v, i) => sum + v * [0.2126, 0.7152, 0.0722][i], 0);
    const ratio = (a, b) => { const [x, y] = [lum(a), lum(b)].sort((m, n) => n - m); return (x + 0.05) / (y + 0.05); };
    const root = getComputedStyle(document.documentElement);
    const ends = ['--brand-500', '--brand-600'].map((v) => root.getPropertyValue(v).trim());
    const out = [];
    for (const el of document.querySelectorAll('[class*="text-brand-hero"]')) {
      if (!el.textContent.trim() || !el.getClientRects().length) continue;
      const st = getComputedStyle(el);
      const size = parseFloat(st.fontSize);
      const large = size >= 24 || (size >= 18.66 && Number(st.fontWeight) >= 700);
      const worst = Math.min(...ends.map((bg) => ratio(paint(bg, st.color), paint(bg, bg))));
      if (worst < (large ? 3 : 4.5)) out.push(`${worst.toFixed(2)} ${st.color}: ${el.textContent.trim().slice(0, 40)}`);
    }
    return out;
  });
  if (!low.length) return;
  if (!isDefaultBrand) failures += low.length;
  console.log(`  ${label}: ${low.length} header text(s) low on the brand gradient${isDefaultBrand ? ' (default Yurt look, not counted)' : ''}`);
  for (const l of low) console.log(`    ${l}`);
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
      for (const signedIn of [false, true]) {
        const context = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2 });
        await context.addInitScript(([b, theme, extra]) => {
          localStorage.setItem('yurt.brand', JSON.stringify(b));
          localStorage.setItem('yurt_lang', 'en');
          localStorage.setItem('yurt.theme', theme);
          for (const [k, v] of Object.entries(extra)) localStorage.setItem(k, v);
        }, [brand, dark ? 'dark' : 'light', signedIn ? SIGNED_IN_STORAGE : {}]);
        const page = await context.newPage();
        // Backend is not running: answer the known calls with fake data and fail the rest fast instead of waiting.
        await page.route('**/api/**', (r) => {
          const path = new URL(r.request().url()).pathname.replace(/^.*\/api\/v1/, '');
          const body = r.request().method() === 'GET' ? MOCK_API[path] : undefined;
          return body === undefined ? r.abort() : r.fulfill({ json: body });
        });
        for (const screen of signedIn ? SIGNED_IN_SCREENS : PUBLIC_SCREENS) {
          await page.goto(base + screen, { waitUntil: 'load' });
          await page.waitForTimeout(2500); // intro video falls back, splash dismisses
          const file = join(out, `${slug}${screen.replace(/\//g, '-')}${dark ? '-dark' : ''}.png`);
          await page.screenshot({ path: file, fullPage: true });
          console.log(file);
          if (a11y) {
            await checkContrast(page, `${slug}${screen}`);
            await checkHeroText(page, `${slug}${screen}`, slug === 'yurt');
          }
        }
        await context.close();
      }
    }
  } finally {
    await browser.close();
    server.close();
  }
  if (failOnIssues && failures) throw new Error(`${failures} accessibility failure(s), see above.`);
}

main().catch((e) => { console.error(`brand-screenshots: ${e.message}`); process.exit(1); });
