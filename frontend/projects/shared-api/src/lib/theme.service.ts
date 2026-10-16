import { Injectable, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'yurt.theme';

/**
 * Light / dark / follow-the-device theme. It only sets `data-theme` on <html>; the colours live in
 * shared-ui theme/brand.css. The default is light, so nothing changes until a customer picks another mode.
 * index.html applies the saved choice before the first paint to avoid a flash.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly mode = signal<ThemeMode>('light');
  private media: MediaQueryList | null = null;

  constructor() {
    this.mode.set(this.read());
    this.apply();
    if (typeof matchMedia === 'function') {
      this.media = matchMedia('(prefers-color-scheme: dark)');
      this.media.addEventListener?.('change', () => this.apply());
    }
  }

  set(mode: ThemeMode): void {
    this.mode.set(mode);
    try { localStorage.setItem(STORAGE_KEY, mode); } catch { /* private mode: the choice lasts until reload */ }
    this.apply();
  }

  /** Whether the dark colours are showing right now (system mode follows the device). */
  isDark(): boolean {
    const mode = this.mode();
    return mode === 'dark' || (mode === 'system' && !!this.media?.matches);
  }

  private apply(): void {
    if (typeof document === 'undefined') return;
    document.documentElement.setAttribute('data-theme', this.isDark() ? 'dark' : 'light');
  }

  private read(): ThemeMode {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      return saved === 'dark' || saved === 'system' ? saved : 'light';
    } catch {
      return 'light';
    }
  }
}
