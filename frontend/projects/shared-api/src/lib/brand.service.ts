import { Injectable, computed, inject, signal } from '@angular/core';
import { BrandConfig, BrandLink } from 'shared-models';
import { CAFE_FONT_FAMILY, RADIUS_SCALE, brandFontStack, setFontFace, buildScale, parseHex, readableOn, readableOnDark, readableOnLight } from './brand-palette';
import { ThemeService } from './theme.service';

/** Steps of the accent scale used for gradient ends and soft tints. */
const ACCENT_STEPS = ['50', '400', '500'] as const;
const CACHE_KEY = 'yurt.brand';
/** The built-in Yurt colours live in shared-ui theme/brand.css; a brand with these values changes nothing. */
const DEFAULT_PRIMARY = '#b83a2b';
const DEFAULT_ACCENT = '#4a2f1e';
/** The platform's own look: the Yurt name, colours, logo, font and corners. */
const NEUTRAL_BRAND: BrandConfig = { name: 'Yurt', primaryColor: DEFAULT_PRIMARY, accentColor: DEFAULT_ACCENT };
const STEPS = ['50', '100', '200', '300', '400', '500', '600', '700', '800', '900', '950'];

/**
 * Applies a cafe's brand by overriding the `--brand-*` CSS variables on <html>.
 *
 * The last known brand is cached in localStorage and applied as soon as this service is created
 * (stale-while-revalidate), so a returning customer sees their cafe's colours on the first paint
 * instead of a flash of the default. The Yurt default brand removes every override.
 */
function httpsOrNull(url: string | null | undefined): string | null {
  return url && /^https:\/\//i.test(url) ? url : null;
}

@Injectable({ providedIn: 'root' })
export class BrandService {
  /** The cafe's name, logo and support contact for headings, receipts and the like. */
  readonly name = signal('Yurt');
  /** Absolute https URL of the cafe's logo, or null to use the bundled logo.png. */
  readonly logoUrl = signal<string | null>(null);
  /** Absolute https URL of the logo made for dark backgrounds, or null when the cafe has none. */
  readonly logoDarkUrl = signal<string | null>(null);
  private readonly theme = inject(ThemeService);
  /** True when the dark mode logo is the one showing (so it needs no light tile behind it). */
  readonly darkLogoShowing = computed(() => this.theme.dark() && !!this.logoDarkUrl());
  /** The logo to show now: the dark one in dark mode when set, otherwise the normal one; null means the bundled logo.png. */
  readonly logo = computed(() => (this.darkLogoShowing() ? this.logoDarkUrl() : this.logoUrl()));
  readonly supportContact = signal<string | null>(null);
  /** Absolute https URL of the splash image shown while the app loads, or null to show the logo. */
  readonly splashUrl = signal<string | null>(null);
  /** Short line shown under the name on the sign-in screen; null uses the built-in tagline. */
  readonly welcomeText = signal<string | null>(null);
  /** Social / website links (https only) for the profile screen. */
  readonly links = signal<BrandLink[]>([]);
  /** True while the neutral Yurt look is forced (admin panel signed out or used by the platform operator). */
  readonly neutral = signal(false);
  private defaultIconHref: string | null = null;
  /** The last brand from the server or cache, re-applied when the neutral look is switched off. */
  private latest: BrandConfig | null = null;

  constructor() {
    const cached = this.readCache();
    if (cached) {
      this.latest = cached;
      this.apply(cached, false);
    }
  }

  /** Applies the brand from the server and remembers it for the next start (kept aside while the neutral look is on). */
  set(brand: BrandConfig | undefined | null): void {
    if (!brand) return;
    this.latest = brand;
    if (this.neutral()) return;
    this.apply(brand, true);
  }

  /**
   * Shows the plain Yurt look instead of the cafe's brand (on), or brings the cafe's brand back (off). The admin panel
   * uses it: it belongs to the platform, so only a signed-in cafe admin sees their cafe's colours there.
   */
  useNeutral(on: boolean): void {
    if (this.neutral() === on) return;
    this.neutral.set(on);
    if (on) this.apply(NEUTRAL_BRAND, false);
    else if (this.latest) this.apply(this.latest, false);
  }

  private apply(brand: BrandConfig, persist: boolean): void {
    this.name.set(brand.name?.trim() || 'Yurt');
    this.logoUrl.set(httpsOrNull(brand.logoUrl));
    this.logoDarkUrl.set(httpsOrNull(brand.logoDarkUrl));
    this.supportContact.set(brand.supportContact?.trim() || null);
    this.splashUrl.set(httpsOrNull(brand.splashUrl));
    this.welcomeText.set(brand.welcomeText?.trim() || null);
    this.links.set((Array.isArray(brand.links) ? brand.links : [])
      .filter((l) => l && typeof l.label === 'string' && l.label.trim() && httpsOrNull(l.url))
      .map((l) => ({ label: l.label.trim(), url: l.url })));

    const root = typeof document === 'undefined' ? null : document.documentElement;
    if (!root) return;

    this.applyIcon(root, httpsOrNull(brand.iconUrl));
    this.applyShape(root, brand);

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
      root.style.setProperty('--brand-on-dark', readableOnDark(primary!));
      root.style.setProperty('--brand-ink-light', readableOnLight(primary!));
      root.style.setProperty('--brand-hero-text', readableOn(primary!));
      const accentScale = buildScale(brand.accentColor);
      if (accentScale) {
        for (const step of ACCENT_STEPS) root.style.setProperty(`--accent-${step}`, accentScale[step]);
      }
      if (parseHex(brand.accentColor)) {
        root.style.setProperty('--brand-accent', brand.accentColor);
        root.style.setProperty('--brand-accent-soft', brand.accentColor);
      }
    }

    if (persist) this.writeCache(brand);
  }

  /** Corner roundness and font family (the cafe's own font file first); unknown or default values remove the override. */
  private applyShape(root: HTMLElement, brand: BrandConfig): void {
    const scale = RADIUS_SCALE[brand.radius ?? ''];
    if (scale === undefined) root.style.removeProperty('--radius-scale');
    else root.style.setProperty('--radius-scale', String(scale));
    setFontFace(root.ownerDocument, 'brand-font-face', CAFE_FONT_FAMILY, brand.fontUrl);
    const font = brandFontStack(brand.font, brand.fontUrl);
    if (!font) root.style.removeProperty('--brand-font');
    else root.style.setProperty('--brand-font', font);
  }

  /** Swaps the browser-tab / home-screen icon; null restores the bundled one. */
  private applyIcon(root: HTMLElement, url: string | null): void {
    const links = Array.from(root.ownerDocument.querySelectorAll<HTMLLinkElement>('link[rel="icon"], link[rel="apple-touch-icon"]'));
    const main = links.find((l) => l.rel === 'icon');
    if (main && this.defaultIconHref === null) this.defaultIconHref = main.getAttribute('href') ?? '';
    if (url) {
      if (main) { main.removeAttribute('type'); main.href = url; }
      else {
        const link = root.ownerDocument.createElement('link');
        link.rel = 'icon';
        link.href = url;
        root.ownerDocument.head.appendChild(link);
      }
      for (const l of links) if (l.rel === 'apple-touch-icon') l.href = url;
    } else if (main && this.defaultIconHref) {
      main.setAttribute('href', this.defaultIconHref);
    }
  }

  private clear(root: HTMLElement): void {
    for (const step of STEPS) root.style.removeProperty(`--brand-${step}`);
    for (const name of ['--brand-500-rgb', '--brand-on-primary', '--brand-on-dark', '--brand-ink-light', '--brand-hero-text', '--brand-accent', '--brand-accent-soft', ...ACCENT_STEPS.map((s) => `--accent-${s}`)])
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
