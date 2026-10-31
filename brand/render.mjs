#!/usr/bin/env node
// Renders the Yurt logo SVGs in this folder into every PNG/ICO the project ships:
//   brand/png/                      logo pictures for the web, docs and stores
//   brand/app-assets/               sources for @capacitor/assets (app icons and splash screens)
//   frontend/projects/*/public/     logo.png and favicon.ico of the customer and admin apps
//   landing/                        favicon.svg + apple-touch-icon.png
// Needs Playwright (Chromium) resolvable, e.g. NODE_PATH="$(npm root -g)" node brand/render.mjs
// Then regenerate the native icons:  cp brand/app-assets/*.png frontend/assets/ && cd frontend && npx @capacitor/assets@3.0.5 generate --android --ios
import { copyFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..');
const { chromium } = createRequire(import.meta.url)('playwright');

const FELT = '#F5EFE4';
const NIGHT = '#1C1917';
const RED = '#B83A2B';
const svg = (name) => readFileSync(join(here, name), 'utf8');
const dataUrl = (text) => 'data:image/svg+xml;base64,' + Buffer.from(text).toString('base64');

/** One picture: an SVG drawn at `scale` of a w×h canvas (centred), on a colour or transparent. */
const jobs = [
  { out: 'png/yurt-mark-512.png', src: 'yurt-mark.svg', w: 512, h: 512 },
  { out: 'png/yurt-logo-light-1200.png', src: 'yurt-logo-light.svg', w: 1200, h: 432 },
  { out: 'png/yurt-logo-dark-1200.png', src: 'yurt-logo-dark.svg', w: 1200, h: 432, bg: NIGHT },
  { out: 'png/yurt-app-icon-1024.png', src: 'yurt-app-icon.svg', w: 1024, h: 1024 },
  { out: 'app-assets/icon-only.png', src: 'yurt-app-icon.svg', w: 1024, h: 1024 },
  // Android adaptive icon: the felt mark on a transparent layer (@capacitor/assets insets it into the safe zone), plus a plain red layer.
  { out: 'app-assets/icon-foreground.png', src: 'yurt-mark-felt.svg', w: 1024, h: 1024, scale: 0.7 },
  { out: 'app-assets/icon-background.png', w: 1024, h: 1024, bg: RED },
  { out: 'app-assets/splash.png', src: 'yurt-mark.svg', w: 2732, h: 2732, scale: 0.22, bg: FELT },
  { out: 'app-assets/splash-dark.png', src: 'yurt-mark.svg', w: 2732, h: 2732, scale: 0.22, bg: NIGHT },
  { out: 'png/favicon-16.png', src: 'yurt-mark.svg', w: 16, h: 16 },
  { out: 'png/favicon-32.png', src: 'yurt-mark.svg', w: 32, h: 32 },
  { out: 'png/favicon-48.png', src: 'yurt-mark.svg', w: 48, h: 48 },
  { out: 'png/apple-touch-icon.png', src: 'yurt-app-icon.svg', w: 180, h: 180 },
];

/** A .ico file holding PNG images (supported by every current browser). */
function ico(pngs) {
  const header = Buffer.alloc(6 + 16 * pngs.length);
  header.writeUInt16LE(0, 0); header.writeUInt16LE(1, 2); header.writeUInt16LE(pngs.length, 4);
  let offset = header.length;
  pngs.forEach(({ size, data }, i) => {
    const e = 6 + 16 * i;
    header.writeUInt8(size >= 256 ? 0 : size, e); header.writeUInt8(size >= 256 ? 0 : size, e + 1);
    header.writeUInt16LE(1, e + 4); header.writeUInt16LE(32, e + 6);
    header.writeUInt32LE(data.length, e + 8); header.writeUInt32LE(offset, e + 12);
    offset += data.length;
  });
  return Buffer.concat([header, ...pngs.map((p) => p.data)]);
}

const browser = await chromium.launch();
try {
  const page = await browser.newPage();
  for (const job of jobs) {
    const scale = job.scale ?? 1;
    const img = job.src ? `<img src="${dataUrl(svg(job.src))}" style="width:${job.w * scale}px;height:${job.h * scale}px;object-fit:contain">` : '';
    await page.setViewportSize({ width: job.w, height: job.h });
    await page.setContent(`<body style="margin:0;width:${job.w}px;height:${job.h}px;display:grid;place-items:center;background:${job.bg ?? 'transparent'}">${img}</body>`);
    await page.waitForLoadState('load');
    mkdirSync(dirname(join(here, job.out)), { recursive: true });
    await page.screenshot({ path: join(here, job.out), omitBackground: !job.bg });
    console.log('wrote brand/' + job.out);
  }
} finally {
  await browser.close();
}

const favicon = ico([16, 32, 48].map((size) => ({ size, data: readFileSync(join(here, `png/favicon-${size}.png`)) })));
for (const app of ['yurt-customer', 'yurt-admin']) {
  const pub = join(root, 'frontend/projects', app, 'public');
  writeFileSync(join(pub, 'favicon.ico'), favicon);
  copyFileSync(join(here, 'png/yurt-mark-512.png'), join(pub, 'logo.png'));
  console.log(`wrote frontend/projects/${app}/public/favicon.ico and logo.png`);
}
copyFileSync(join(here, 'yurt-mark.svg'), join(root, 'landing/favicon.svg'));
copyFileSync(join(here, 'png/apple-touch-icon.png'), join(root, 'landing/apple-touch-icon.png'));
console.log('wrote landing/favicon.svg and apple-touch-icon.png');
