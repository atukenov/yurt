#!/usr/bin/env node
// Screenshots the customer or admin app's screens for a few sample brands, to eyeball white-label changes.
// The brand is seeded through the same localStorage cache the app uses (key "yurt.brand"), so no backend is needed:
// the signed-in screens get a signed-in user, a cart and fake API answers (APPS below).
//   cd frontend && npx ng build yurt-customer --configuration production
//   node scripts/brand-screenshots.mjs [outDir] [--app=admin] [--a11y] [--dark] [--fail]
// --app=admin checks the admin app instead (build it with `ng build yurt-admin`), on a tablet-sized screen; file names start with "admin-".
// --dark seeds the dark theme (localStorage key "yurt.theme") and adds "-dark" to the file names.
// --a11y also runs axe-core's colour-contrast and target-size (24px) rules on every screen and prints the failures (needs `axe-core`
// resolvable like playwright, e.g. NODE_PATH to a folder that has both).
// --fail exits with code 1 when any a11y failure was found (CI runs it this way).
// LOG_API=1 prints the API calls that have no fake answer, to find what a new screen needs.
//
// Store mode makes App Store and Google Play screenshots for one cafe (cafes/<slug>/cafe.json), light mode, in one language:
//   node scripts/brand-screenshots.mjs [outDir] --store=<slug> [--lang=ru|kk|en] [--api=https://your-server]
// The brand comes from the live server's app config when --api is given (sent with the cafe's `tenant`), otherwise from
// the optional `brand` object in cafe.json (same fields as the Brand screen). Files land in
// <outDir>/<slug>/<lang>/<device>/01-menu.png and so on, at the exact pixel sizes the stores accept (STORE_DEVICES).
// Each store picture has a short caption on top, on the brand colour, with the app screen below it (STORE_CAPTIONS has the
// texts; a cafe can replace any of them with `storeCaptions` in cafe.json). --no-captions makes plain screens instead.
//
// The browser clock is fixed (FIXED_NOW), so the same app makes the same pictures every day and compare-screenshots.mjs
// only reports real changes.
import { createServer } from 'node:http';
import { existsSync, mkdirSync, readFileSync, statSync } from 'node:fs';
import { dirname, extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const args = process.argv.slice(2);
const appName = args.find((a) => a.startsWith('--app='))?.slice(6) ?? 'customer';
const a11y = args.includes('--a11y');
const dark = args.includes('--dark');
const failOnIssues = args.includes('--fail');
const storeSlug = args.find((a) => a.startsWith('--store='))?.slice(8);
const lang = args.find((a) => a.startsWith('--lang='))?.slice(7) ?? 'ru';
const apiBase = args.find((a) => a.startsWith('--api='))?.slice(6);
const captions = !args.includes('--no-captions');
const out = args.find((a) => !a.startsWith('--')) ?? join(process.cwd(), storeSlug ? 'store-screenshots' : 'brand-screenshots');
let failures = 0;

/** "Now" for the browser: a few minutes after the sample order was placed, so its timer shows a believable wait. */
const FIXED_NOW = new Date('2026-10-21T09:05:00Z');

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
  // The cafe takes online payments (Stripe), so the cart and an unpaid card order show "Pay online".
  '/payments/config': { provider: 'stripe' },
  '/loyalty/me': { enabled: true, available: true, linked: true, balance: 350, earnPercent: 5 },
  '/auth/me': { id: 'u-1', mobileNumber: '+77001234567', firstName: 'Aru', lastName: 'Sadykova', createdAt: '2026-01-15T10:00:00Z', dateOfBirth: '1997-05-04' },
  '/auth/me/stats': {
    totalOrders: 24, totalSpent: 52800, totalDrinks: 31, distinctLocations: 2, redeemedOrders: 3, earlyOrders: 5, lateOrders: 1,
    maxDailyDrinks: 3, maxDailyStreak: 4, maxWeeklyStreak: 3, maxWeekendStreak: 2, maxRepeatItemCount: 9,
  },
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
/** A third: chosen "Pay online" but not paid yet (shows the "Pay online now" button). */
MOCK_API['/orders/o-3'] = { ...MOCK_API['/orders/o-2'], id: 'o-3', paymentMethod: 'Card', loyaltyPointsSpent: undefined };
/** A fourth: ordered to table 5 by its QR code, ready (staff bring it over, no 2GIS route). */
MOCK_API['/orders/o-4'] = { ...MOCK_API['/orders/o-1'], id: 'o-4', status: 'Ready', tableLabel: '5' };
/** The guest scanned the QR code of table 5 (table_ordering), so the cart says the order goes to that table. */
const TABLE = { code: 'ABCDEFGH23', label: '5', locationId: LOCATION.id, locationName: LOCATION.name };
const SIGNED_IN_STORAGE = {
  yurt_user: JSON.stringify({ accessToken: 'a11y', refreshToken: '', userId: 'u-1', displayName: 'Aru', userType: 'Customer' }),
  yurt_location_id: LOCATION.id,
  yurt_location_name: LOCATION.name,
  yurt_cart: JSON.stringify([
    { menuItemId: 'm-1', name: 'Cappuccino', price: 1200, quantity: 2, variantId: 'v-1', variantLabel: 'Large' },
    { menuItemId: 'm-4', name: 'Croissant', price: 900, quantity: 1, notes: 'Warm it up' },
  ]),
};
// Store screenshots show a plain pickup order; the brand and a11y checks also cover the table chip in the cart.
const storeMode = args.some((a) => a.startsWith('--store='));
if (!storeMode) SIGNED_IN_STORAGE.yurt_table = JSON.stringify({ ...TABLE, at: Date.now() });

/** Fake answers for the admin screens, keyed like MOCK_API. */
const ADMIN_ORDER = (id, status, extra = {}) => ({
  ...MOCK_API['/orders/o-1'], id, status, customerPhone: '+77001234567', createdAt: '2026-10-21T09:00:00Z', ...extra,
});
const SHIFT = { id: 's-1', locationId: 'loc-1', locationName: 'Main Street', openedAt: '2026-10-21T08:00:00Z', openedBy: 'Dana', openingCash: 20000 };
const ADMIN_MOCK_API = {
  '/admin/locations': [LOCATION, { ...LOCATION, id: 'loc-2', name: 'Riverside', address: '5 River Road' }],
  '/admin/orders': {
    total: 4, page: 1, pageSize: 50, items: [
      ADMIN_ORDER('o-11', 'Created', { paymentStatus: 'Unpaid', etaMinutes: undefined, acceptedAt: undefined, tableLabel: '5' }),
      ADMIN_ORDER('o-12', 'Preparing', { items: [{ ...MOCK_API['/orders/o-1'].items[0], notes: 'Oat milk' }] }),
      ADMIN_ORDER('o-13', 'Ready', { readyStationIds: ['st-1'] }),
      ADMIN_ORDER('o-14', 'Completed', { completedAt: '2026-10-21T09:20:00Z' }),
    ],
  },
  '/admin/menu/categories': MOCK_API['/menu/categories'],
  '/admin/menu/items': MOCK_API['/menu'].map((m) => ({ ...m, locationIds: ['loc-1'] })),
  '/admin/menu/toppings': [{ id: 't-1', name: 'Oat milk', price: 300, isAvailable: true, categoryIds: ['cat-1'], group: 'Milk' }],
  '/admin/stations/routing': {
    stations: [{ id: 'st-1', name: 'Bar', categories: 1 }, { id: 'st-2', name: 'Kitchen', categories: 1 }],
    categories: [], itemStations: {},
  },
  '/admin/stock': [
    { menuItemId: 'm-1', name: 'Cappuccino', categoryName: 'Coffee', quantity: 40, lowStockAt: 10, isLow: false, isSoldOut: false },
    { menuItemId: 'm-2', name: 'Flat white', categoryName: 'Coffee', quantity: 6, lowStockAt: 10, isLow: true, isSoldOut: false },
    { menuItemId: 'm-4', name: 'Croissant', categoryName: 'Bakery', quantity: 0, lowStockAt: 5, isLow: true, isSoldOut: true },
    { menuItemId: 'm-3', name: 'Raf', categoryName: 'Coffee', quantity: null, lowStockAt: null, isLow: false, isSoldOut: false },
  ],
  '/admin/stock/alerts': {
    low: 1, out: 1, items: [
      { kind: 'dish', id: 'm-2', name: 'Flat white', locationId: 'loc-1', locationName: 'Main Street', quantity: 6, isOut: false },
      { kind: 'dish', id: 'm-4', name: 'Croissant', locationId: 'loc-1', locationName: 'Main Street', quantity: 0, isOut: true },
    ],
  },
  '/admin/stock/alert-phones': [{ phone: '+77001234567', hasApp: true }],
  '/admin/shifts/current': SHIFT,
  '/admin/shifts': [SHIFT, { ...SHIFT, id: 's-0', openedAt: '2026-10-20T08:00:00Z', closedAt: '2026-10-20T20:00:00Z', closedBy: 'Dana', countedCash: 41000 }],
  '/admin/shifts/s-1/report': {
    shift: SHIFT, ordersCompleted: 12, ordersDeclined: 1, subtotal: 43000, discounts: 1200, pointsSpent: 300, takings: 41500,
    byMethod: [{ method: 'Cash', orders: 5, amount: 15000 }, { method: 'Card', orders: 7, amount: 26500 }],
    cashSales: 15000, cashIn: 0, cashOut: 2000, expectedCash: 33000, difference: null, movements: [],
  },
  '/admin/iiko/nomenclature': [],
  '/admin/tables': ['1', '2', 'Terrace 1'].map((label, i) => ({
    id: `tb-${i}`, locationId: 'loc-1', label, code: `CODE${i}ABCDE`, link: `https://order.example.kz/t/CODE${i}ABCDE`,
    // A stand-in picture: the real QR code comes from the server.
    qrSvg: `data:image/svg+xml;base64,${Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 4 4"><path d="M0 0h2v2H0zM2 2h2v2H2z"/></svg>').toString('base64')}`,
  })),
  '/admin/customers': {
    total: 2, page: 1, pageSize: 50, items: [
      { id: 'u-1', phone: '+77001234567', displayName: 'Aru Sadykova', registeredAt: '2026-01-15T10:00:00Z', orderCount: 24, totalSpent: 52800 },
      { id: 'u-2', phone: '+77007654321', displayName: '', registeredAt: '2026-09-02T10:00:00Z', orderCount: 1, totalSpent: 1200 },
    ],
  },
};
const ADMIN_STORAGE = {
  yurt_user: JSON.stringify({ accessToken: 'a11y', refreshToken: '', userId: 'a-1', displayName: 'Dana', userType: 'admin', role: 'Admin' }),
};

/** The sample data's English texts in the other app languages, so store screenshots are in one language throughout. */
const SAMPLE_TEXT = {
  ru: {
    Coffee: 'Кофе', Bakery: 'Выпечка', Cappuccino: 'Капучино', 'Flat white': 'Флэт уайт', Raf: 'Раф', Croissant: 'Круассан',
    'Double shot with steamed milk': 'Двойной эспрессо со взбитым молоком', 'Butter croissant': 'Сливочный круассан',
    'Main Street': 'Центральная', '1 Main Street': 'ул. Центральная, 1', Large: 'Большой', 'Warm it up': 'Подогрейте, пожалуйста',
    Aru: 'Ару', Sadykova: 'Садыкова',
  },
  kk: {
    Coffee: 'Кофе', Bakery: 'Тоқаштар', Cappuccino: 'Капучино', 'Flat white': 'Флэт уайт', Raf: 'Раф', Croissant: 'Круассан',
    'Double shot with steamed milk': 'Көпіршітілген сүті бар қос эспрессо', 'Butter croissant': 'Майлы круассан',
    'Main Street': 'Орталық', '1 Main Street': 'Орталық к-сі, 1', Large: 'Үлкен', 'Warm it up': 'Жылытып беріңізші',
    Aru: 'Ару', Sadykova: 'Садықова',
  },
};
/** Deep copy of `value` with every string that has a translation in `lang` replaced. */
const localize = (value, lang) => JSON.parse(JSON.stringify(value), (_, v) => (typeof v === 'string' && SAMPLE_TEXT[lang]?.[v]) || v);

/**
 * Screen sizes the stores take, as CSS size x scale = pixels. Apple needs the 6.9" set (6.5" is accepted as well);
 * the iOS app is iPhone-only (TARGETED_DEVICE_FAMILY = 1), so no iPad set. Google Play takes 9:16 phone shots up to 3840px.
 */
export const STORE_DEVICES = {
  'ios-6.9in': { viewport: { width: 440, height: 956 }, deviceScaleFactor: 3 }, // 1320 x 2868
  'ios-6.5in': { viewport: { width: 414, height: 896 }, deviceScaleFactor: 3 }, // 1242 x 2688
  'android-phone': { viewport: { width: 360, height: 640 }, deviceScaleFactor: 3 }, // 1080 x 1920
};
/** Caption on each store picture, per screen and app language. */
export const STORE_CAPTIONS = {
  en: {
    menu: 'Order ahead from the menu', cart: 'Pay the way you like', 'order-status': 'See when your order is ready',
    profile: 'Earn bonuses with every order', 'sign-in': 'Sign in with your phone number',
  },
  ru: {
    menu: 'Закажите заранее из меню', cart: 'Оплачивайте как удобно', 'order-status': 'Узнайте, когда заказ готов',
    profile: 'Копите бонусы с каждым заказом', 'sign-in': 'Вход по номеру телефона',
  },
  kk: {
    menu: 'Мәзірден алдын ала тапсырыс беріңіз', cart: 'Ыңғайлы тәсілмен төлеңіз', 'order-status': 'Тапсырысыңыз қашан дайын екенін біліңіз',
    profile: 'Әр тапсырыстан бонус жинаңыз', 'sign-in': 'Телефон нөмірімен кіру',
  },
};
/** Store mode screens in listing order; the number becomes the file name prefix. */
const STORE_SCREENS = [
  { name: 'menu', path: '/menu', signedIn: true },
  // A picked payment method, so the checkout button is not greyed out.
  { name: 'cart', path: '/cart', signedIn: true, prepare: (page) => page.click('button:has(img[src="kaspi.png"])') },
  { name: 'order-status', path: '/orders/o-1', signedIn: true },
  { name: 'profile', path: '/profile', signedIn: true },
  { name: 'sign-in', path: '/auth/login', signedIn: false },
];

/** Per app: where the build is, which screens to open signed out and signed in, the fake API and the screen size. */
const APPS = {
  customer: {
    project: 'yurt-customer', prefix: '', viewport: { width: 390, height: 844 }, deviceScaleFactor: 2,
    publicScreens: ['/auth/login', '/auth/register'],
    signedInScreens: ['/menu', '/cart', '/orders/o-1', '/orders/o-2', '/orders/o-3', '/orders/o-4', '/profile'],
    api: MOCK_API, storage: SIGNED_IN_STORAGE,
  },
  admin: {
    project: 'yurt-admin', prefix: 'admin-', viewport: { width: 1024, height: 768 }, deviceScaleFactor: 1,
    publicScreens: ['/auth/login'],
    signedInScreens: ['/orders', '/kitchen', '/cashier', '/menu', '/stock', '/customers', '/tables'],
    api: ADMIN_MOCK_API, storage: ADMIN_STORAGE,
  },
};
const app = APPS[storeSlug ? 'customer' : appName];
if (!app) throw new Error(`Unknown --app=${appName}; use one of: ${Object.keys(APPS).join(', ')}.`);
if (storeSlug && !['ru', 'kk', 'en'].includes(lang)) throw new Error(`Unknown --lang=${lang}; use ru, kk or en.`);
const root = join(fileURLToPath(new URL('.', import.meta.url)), '..', 'dist', app.project, 'browser');

/** Sample brands: the Yurt default, a deep blue, a dark green with a long name, links and a welcome line, and a pale one. */
export const BRANDS = {
  yurt: { name: 'Yurt', primaryColor: '#b83a2b', accentColor: '#4a2f1e' },
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

/**
 * The cafe's brand and captions for store mode. The brand comes from the live server with --api, otherwise `brand` in
 * cafe.json; captions are STORE_CAPTIONS with the cafe's own `storeCaptions` ({ "ru": { "menu": "..." } }) on top.
 */
async function storeCafe(slug) {
  const { readCafe } = await import('../../scripts/apply-cafe.mjs');
  const repoRoot = join(fileURLToPath(new URL('.', import.meta.url)), '..', '..');
  const cafe = readCafe(repoRoot, slug);
  let brand = cafe.brand;
  if (apiBase) {
    const url = `${apiBase.replace(/\/+$/, '')}/api/v1/app/config`;
    const res = await fetch(url, { headers: cafe.tenant ? { 'X-Tenant': cafe.tenant } : {} });
    if (!res.ok) throw new Error(`${url} answered ${res.status}.`);
    brand = (await res.json()).brand;
    if (!brand) throw new Error(`${url} has no brand; switch the branding module on for this cafe and save its Brand screen.`);
  }
  if (!brand) {
    console.warn(`  warning: no brand for ${slug} (add "brand" to cafe.json or pass --api); using the Yurt colours with "${cafe.appName}"`);
    brand = { ...BRANDS.yurt, name: cafe.appName };
  }
  if (typeof brand.name !== 'string' || !/^#[0-9a-f]{6}$/i.test(brand.primaryColor ?? '') || !/^#[0-9a-f]{6}$/i.test(brand.accentColor ?? ''))
    throw new Error('brand needs a name and primaryColor / accentColor like #1d4ed8.');
  const own = cafe.storeCaptions?.[lang] ?? {};
  for (const [screen, text] of Object.entries(own)) {
    if (!STORE_SCREENS.some((s) => s.name === screen)) throw new Error(`storeCaptions.${lang}.${screen}: unknown screen; use ${STORE_SCREENS.map((s) => s.name).join(', ')}.`);
    if (typeof text !== 'string' || !text.trim() || text.length > 60) throw new Error(`storeCaptions.${lang}.${screen} must be text of up to 60 characters.`);
  }
  return { brand, captions: { ...STORE_CAPTIONS[lang], ...own } };
}

/** What to shoot: per brand, a signed-out and a signed-in pass, each with its screens, screen size and file names. */
async function plan() {
  if (!storeSlug) {
    return Object.entries(BRANDS).flatMap(([slug, brand]) => [false, true].map((signedIn) => ({
      slug, brand, signedIn, lang: 'en', theme: dark ? 'dark' : 'light', viewport: app.viewport, deviceScaleFactor: app.deviceScaleFactor,
      fullPage: true, api: app.api, storage: app.storage,
      screens: (signedIn ? app.signedInScreens : app.publicScreens)
        .map((path) => ({ path, file: join(out, `${app.prefix}${slug}${path.replace(/\//g, '-')}${dark ? '-dark' : ''}.png`) })),
    })));
  }
  const { brand, captions: texts } = await storeCafe(storeSlug);
  const api = localize(app.api, lang);
  api['/orders/o-1'].paymentMethod = 'KaspiBank'; // shows the bank's name and logo instead of the plain "Card"
  // Storage values are JSON text themselves, so each is parsed, translated and written back.
  const storage = Object.fromEntries(Object.entries(app.storage).map(([k, v]) => {
    try { return [k, JSON.stringify(localize(JSON.parse(v), lang))]; } catch { return [k, localize(v, lang)]; }
  }));
  return Object.entries(STORE_DEVICES).flatMap(([device, size]) => [false, true].map((signedIn) => ({
    slug: storeSlug, brand, signedIn, lang, theme: 'light', ...size, fullPage: false, api, storage,
    screens: STORE_SCREENS.map((s, i) => ({ ...s, n: i + 1 })).filter((s) => s.signedIn === signedIn).map((s) => ({
      path: s.path, prepare: s.prepare, caption: captions ? texts[s.name] : undefined,
      file: join(out, storeSlug, lang, device, `${String(s.n).padStart(2, '0')}-${s.name}.png`),
    })),
  })));
}

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

/**
 * Turns the current screen into a store picture: the caption at the top on the brand gradient, the screen below it in a
 * dark rounded frame running off the bottom edge. It is drawn over the app page itself, so it has the brand's own
 * colours and font, and it keeps the screen size, so the picture keeps the exact store pixel size.
 */
async function showCaption(page, caption) {
  const shot = `data:image/png;base64,${(await page.screenshot({ animations: 'disabled' })).toString('base64')}`;
  await page.evaluate(async ([src, text]) => {
    const root = getComputedStyle(document.documentElement);
    const ctx = Object.assign(document.createElement('canvas'), { width: 1, height: 1 }).getContext('2d', { willReadFrequently: true });
    ctx.fillStyle = root.getPropertyValue('--brand-500').trim();
    ctx.fillRect(0, 0, 1, 1);
    const lum = [...ctx.getImageData(0, 0, 1, 1).data.slice(0, 3)].map((v) => v / 255)
      .map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4)).reduce((sum, v, i) => sum + v * [0.2126, 0.7152, 0.0722][i], 0);
    // White or near-black, whichever reads better on the brand colour (the header's own text colour may not, on Yurt yellow).
    const ink = 1.05 / (lum + 0.05) >= (lum + 0.05) / (0.0098 + 0.05) ? '#ffffff' : '#1c1917';
    const box = document.createElement('div');
    box.style.cssText = `position: fixed; inset: 0; z-index: 2147483647; overflow: hidden; display: flex; flex-direction: column;
      align-items: center; background: linear-gradient(160deg, var(--brand-500), var(--brand-600));
      font-family: ${getComputedStyle(document.body).fontFamily};`;
    const title = document.createElement('h1');
    title.textContent = text;
    title.style.cssText = `box-sizing: border-box; height: 23%; margin: 0; padding: 0 7%; display: flex; align-items: center;
      justify-content: center; text-align: center; color: ${ink}; font-size: 7.6vw; font-weight: 800; line-height: 1.15;`;
    const img = new Image();
    img.src = src;
    img.style.cssText = `width: 80%; flex: none; border: 2.2vw solid #1c1917; border-radius: 9vw;
      box-shadow: 0 3vw 10vw rgb(0 0 0 / 0.35);`;
    await img.decode();
    box.append(title, img);
    document.body.append(box);
  }, [shot, caption]);
}

async function main() {
  if (!existsSync(join(root, 'index.html'))) throw new Error(`Build first: ${root} not found (ng build ${app.project} --configuration production).`);
  const { chromium } = createRequire(import.meta.url)('playwright');
  const server = await serve();
  const base = `http://127.0.0.1:${server.address().port}`;
  const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || undefined });
  try {
    for (const job of await plan()) {
      const context = await browser.newContext({ viewport: job.viewport, deviceScaleFactor: job.deviceScaleFactor });
      await context.clock.setFixedTime(FIXED_NOW);
      await context.addInitScript(([b, language, theme, extra]) => {
        localStorage.setItem('yurt.brand', JSON.stringify(b));
        localStorage.setItem('yurt_lang', language);
        localStorage.setItem('yurt.theme', theme);
        for (const [k, v] of Object.entries(extra)) localStorage.setItem(k, v);
      }, [job.brand, job.lang, job.theme, job.signedIn ? job.storage : {}]);
      const page = await context.newPage();
      // Backend is not running: answer the known calls with fake data and fail the rest fast instead of waiting.
      await page.route('**/api/**', (r) => {
        const path = new URL(r.request().url()).pathname.replace(/^.*\/api\/v1/, '');
        const body = r.request().method() === 'GET' ? job.api[path] : undefined;
        if (body === undefined && process.env.LOG_API) console.log(`    unmocked ${r.request().method()} ${path}`);
        return body === undefined ? r.abort() : r.fulfill({ json: body });
      });
      for (const { path, file, prepare, caption } of job.screens) {
        await page.goto(base + path, { waitUntil: 'load' });
        await page.waitForTimeout(2500); // intro video falls back, splash dismisses
        if (prepare) {
          await prepare(page);
          // The click scrolls the button into view; go back to the top so the shot starts where the screen does.
          await page.evaluate(() => { for (const el of [document.scrollingElement, ...document.querySelectorAll('*')]) if (el?.scrollTop) el.scrollTop = 0; });
          await page.waitForTimeout(300);
        }
        mkdirSync(dirname(file), { recursive: true });
        if (caption) await showCaption(page, caption);
        // Animations (the pulsing "online" dot) are stopped, so two runs give the same picture.
        await page.screenshot({ path: file, fullPage: job.fullPage, animations: 'disabled' });
        console.log(file);
        if (a11y && !caption) {
          await checkContrast(page, `${app.prefix}${job.slug}${path}`);
          await checkHeroText(page, `${app.prefix}${job.slug}${path}`, job.slug === 'yurt');
        }
      }
      await context.close();
    }
  } finally {
    await browser.close();
    server.close();
  }
  if (failOnIssues && failures) throw new Error(`${failures} accessibility failure(s), see above.`);
}

main().catch((e) => { console.error(`brand-screenshots: ${e.message}`); process.exit(1); });
