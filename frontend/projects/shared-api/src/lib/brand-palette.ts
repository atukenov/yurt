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

/** The whole scale as `{ '50': '#...', ..., '950': '#...' }`, or null for an invalid colour. */
export function buildScale(primaryHex: string): Record<string, string> | null {
  const base = parseHex(primaryHex);
  if (!base) return null;
  const scale: Record<string, string> = { '500': toHex(base) };
  for (const [step, t] of TINTS) scale[String(step)] = toHex(mix(base, [255, 255, 255], t));
  for (const [step, t] of SHADES) scale[String(step)] = toHex(mix(base, [0, 0, 0], t));
  return scale;
}
