import { Injectable, signal } from '@angular/core';
import { BrandConfig } from 'shared-models';
import { buildScale, parseHex, readableOn } from './brand-palette';

const CACHE_KEY = 'yurt.brand';
/** The built-in Yurt colours live in shared-ui theme/brand.css; a brand with these values changes nothing. */
const DEFAULT_PRIMARY = '#ffd119';
const DEFAULT_ACCENT = '#ea580c';
const STEPS = ['50', '100', '200', '300', '400', '500', '600', '700', '800', '900', '950'];

/**
 * Applies a cafe's brand by overriding the `--brand-*` CSS variables on <html>.
 *
 * The last known brand is cached in localStorage and applied as soon as this service is created
 * (stale-while-revalidate), so a returning customer sees their cafe's colours on the first paint
 * instead of a flash of the default. The Yurt default brand removes every override.
 */
@Injectable({ providedIn: 'root' })
export class BrandService {
  /** The cafe's name, logo and support contact for headings, receipts and the like. */
  readonly name = signal('Yurt');
  /** Absolute https URL of the cafe's logo, or null to use the bundled logo.png. */
  readonly logoUrl = signal<string | null>(null);
  readonly supportContact = signal<string | null>(null);

  constructor() {
    const cached = this.readCache();
    if (cached) this.apply(cached, false);
  }

  /** Applies the brand from the server and remembers it for the next start. */
  set(brand: BrandConfig | undefined | null): void {
    if (!brand) return;
    this.apply(brand, true);
  }

  private apply(brand: BrandConfig, persist: boolean): void {
    this.name.set(brand.name?.trim() || 'Yurt');
    this.logoUrl.set(brand.logoUrl && /^https:\/\//i.test(brand.logoUrl) ? brand.logoUrl : null);
    this.supportContact.set(brand.supportContact?.trim() || null);

    const root = typeof document === 'undefined' ? null : document.documentElement;
    if (!root) return;

    const scale = buildScale(brand.primaryColor);
    const primary = parseHex(brand.primaryColor);
    const isDefault = !scale || !primary ||
      (brand.primaryColor.toLowerCase() === DEFAULT_PRIMARY && brand.accentColor.toLowerCase() === DEFAULT_ACCENT);

    if (isDefault) {
      this.clear(root);
    } else {
      for (const step of STEPS) root.style.setProperty(`--brand-${step}`, scale![step]);
      root.style.setProperty('--brand-500-rgb', primary!.join(' '));
      root.style.setProperty('--brand-on-primary', readableOn(primary!));
      if (parseHex(brand.accentColor)) {
        root.style.setProperty('--brand-accent', brand.accentColor);
        root.style.setProperty('--brand-accent-soft', brand.accentColor);
      }
    }

    if (persist) this.writeCache(brand);
  }

  private clear(root: HTMLElement): void {
    for (const step of STEPS) root.style.removeProperty(`--brand-${step}`);
    for (const name of ['--brand-500-rgb', '--brand-on-primary', '--brand-accent', '--brand-accent-soft'])
      root.style.removeProperty(name);
  }

  private readCache(): BrandConfig | null {
    try {
      const raw = localStorage.getItem(CACHE_KEY);
      if (!raw) return null;
      const parsed = JSON.parse(raw);
      return parsed && typeof parsed.name === 'string' && typeof parsed.primaryColor === 'string' && typeof parsed.accentColor === 'string'
        ? (parsed as BrandConfig) : null;
    } catch {
      return null;
    }
  }

  private writeCache(brand: BrandConfig): void {
    try {
      localStorage.setItem(CACHE_KEY, JSON.stringify(brand));
    } catch { /* storage unavailable */ }
  }
}
