/**
 * Turns one brand colour into the full `--brand-50..950` scale plus a readable text colour.
 * Pure functions, no DOM, so they are easy to reason about and to test.
 *
 * The mixing ratios were fitted to the hand-made Yurt yellow scale, so a similar colour gets a similar feel.
 */

/** Share of white mixed into the primary for steps 50, 100, 200, 300, 400. */
const TINTS: ReadonlyArray<readonly [number, number]> = [
  [50, 0.887], [100, 0.69], [200, 0.48], [300, 0.235], [400, 0.10],
];
/** Share of black mixed into the primary for steps 600, 700, 800, 900, 950. */
const SHADES: ReadonlyArray<readonly [number, number]> = [
  [600, 0.15], [700, 0.35], [800, 0.545], [900, 0.74], [950, 0.855],
];

export type Rgb = readonly [number, number, number];

export function parseHex(hex: string): Rgb | null {
  const m = /^#([0-9a-f]{6})$/i.exec(hex.trim());
  if (!m) return null;
  const n = parseInt(m[1], 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}

export function toHex([r, g, b]: Rgb): string {
  return '#' + [r, g, b].map((v) => Math.round(v).toString(16).padStart(2, '0')).join('');
}

function mix(a: Rgb, b: Rgb, t: number): Rgb {
  return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t];
}

/** WCAG relative luminance, 0 (black) to 1 (white). */
export function luminance([r, g, b]: Rgb): number {
  const lin = (v: number) => {
    const c = v / 255;
    return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b);
}

export function contrastRatio(a: Rgb, b: Rgb): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

const DARK_TEXT: Rgb = [28, 25, 23];   // #1c1917
const LIGHT_TEXT: Rgb = [255, 255, 255];

/** Whichever of dark or white text reads better on the given colour. */
export function readableOn(color: Rgb): string {
  return contrastRatio(color, DARK_TEXT) >= contrastRatio(color, LIGHT_TEXT) ? toHex(DARK_TEXT) : toHex(LIGHT_TEXT);
}

/**
 * The brand colour, lightened just enough to read on the near-black (#1c1917) buttons and panels.
 * A bright primary comes back unchanged; a dark one (navy, forest green) is mixed towards white.
 */
export function readableOnDark(color: Rgb, minContrast = 4.5): string {
  for (let t = 0; t <= 1; t += 0.05) {
    const c = mix(color, [255, 255, 255], t);
    if (contrastRatio(c, DARK_TEXT) >= minContrast) return toHex(c);
  }
  return toHex(LIGHT_TEXT);
}

/**
 * The brand colour, darkened just enough to read as text on the light page background (#fafaf9).
 * A dark primary comes back unchanged; a bright one is mixed towards black.
 */
export function readableOnLight(color: Rgb, minContrast = 4.5): string {
  const page: Rgb = [250, 250, 249];
  for (let t = 0; t <= 1; t += 0.05) {
    const c = mix(color, [0, 0, 0], t);
    if (contrastRatio(c, page) >= minContrast) return toHex(c);
  }
  return toHex(DARK_TEXT);
}

/**
 * A heads-up for primaries that make a poor palette: so light that buttons vanish on white pages
 * and the tints collapse into white, or so dark that the shades collapse into black.
 */
export function paletteWarning(primaryHex: string): 'tooLight' | 'tooDark' | null {
  const rgb = parseHex(primaryHex);
  if (!rgb) return null;
  const l = luminance(rgb);
  if (contrastRatio(rgb, LIGHT_TEXT) < 1.3) return 'tooLight'; // button barely visible on a white page
  return l < 0.01 ? 'tooDark' : null;
}

/** The whole scale as `{ '50': '#...', ..., '950': '#...' }`, or null for an invalid colour. */
export function buildScale(primaryHex: string): Record<string, string> | null {
  const base = parseHex(primaryHex);
  if (!base) return null;
  const scale: Record<string, string> = { '500': toHex(base) };
  for (const [step, t] of TINTS) scale[String(step)] = toHex(mix(base, [255, 255, 255], t));
  for (const [step, t] of SHADES) scale[String(step)] = toHex(mix(base, [0, 0, 0], t));
  return scale;
}

/** Corner roundness presets: the multiplier for `--radius-scale` (1 is the default look). */
export const RADIUS_SCALE: Record<string, number> = { square: 0, round: 1.5 };

/** Font presets that need no download. Anything else keeps the bundled Inter. */
export const FONT_STACKS: Record<string, string> = {
  system: "system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif",
  serif: "Georgia, 'Times New Roman', serif",
};

/** The bundled default, matching `--brand-font` in shared-ui theme/brand.css. */
const DEFAULT_FONT_STACK = "'Inter', system-ui, sans-serif";

/** Font family name a cafe's own font file is registered under. */
export const CAFE_FONT_FAMILY = 'Yurt Cafe Font';

/** An https link to a .woff2, .woff, .ttf or .otf file (query string ignored), else null. */
export function fontFileUrl(url: string | null | undefined): string | null {
  const value = url?.trim();
  if (!value || !/^https:\/\//i.test(value)) return null;
  return /\.(woff2?|ttf|otf)$/i.test(value.split(/[?#]/)[0]) ? value : null;
}

/**
 * The font stack for a brand: the cafe's own font file first (when given), then the chosen preset,
 * then the default. Returns null when nothing differs from the default.
 */
export function brandFontStack(font: string | null | undefined, fontUrl: string | null | undefined, family = CAFE_FONT_FAMILY): string | null {
  const preset = FONT_STACKS[font ?? ''] ?? null;
  if (!fontFileUrl(fontUrl)) return preset;
  return `'${family}', ${preset ?? DEFAULT_FONT_STACK}`;
}

/**
 * Registers (or replaces, or with a null url removes) an @font-face rule in a <style> tag with the
 * given id. `font-display: swap` shows the fallback font until the file arrives, and keeps it if
 * the file never loads.
 */
export function setFontFace(doc: Document, styleId: string, family: string, url: string | null | undefined): void {
  const safe = fontFileUrl(url);
  let style = doc.getElementById(styleId) as HTMLStyleElement | null;
  if (!safe) {
    style?.remove();
    return;
  }
  if (!style) {
    style = doc.createElement('style');
    style.id = styleId;
    doc.head.appendChild(style);
  }
  const src = safe.replace(/["\\\s]/g, (c) => encodeURIComponent(c));
  style.textContent = `@font-face { font-family: '${family}'; src: url("${src}"); font-display: swap; }`;
}
