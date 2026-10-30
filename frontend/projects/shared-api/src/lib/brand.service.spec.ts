import { TestBed } from '@angular/core/testing';

import { BrandService } from './brand.service';
import { ThemeService } from './theme.service';

describe('BrandService logo', () => {
  let brand: BrandService;
  let theme: ThemeService;
  const base = { name: 'Blue Cafe', primaryColor: '#1d4ed8', accentColor: '#0ea5e9' };

  beforeEach(() => {
    try { localStorage.clear(); } catch { /* no storage */ }
    TestBed.configureTestingModule({});
    brand = TestBed.inject(BrandService);
    theme = TestBed.inject(ThemeService);
    theme.set('light');
  });

  it('shows the normal logo in light mode', () => {
    brand.set({ ...base, logoUrl: 'https://cdn.example.com/logo.png', logoDarkUrl: 'https://cdn.example.com/logo-dark.png' });
    expect(brand.logo()).toBe('https://cdn.example.com/logo.png');
    expect(brand.darkLogoShowing()).toBe(false);
  });

  it('shows the dark logo in dark mode', () => {
    brand.set({ ...base, logoUrl: 'https://cdn.example.com/logo.png', logoDarkUrl: 'https://cdn.example.com/logo-dark.png' });
    theme.set('dark');
    expect(brand.logo()).toBe('https://cdn.example.com/logo-dark.png');
    expect(brand.darkLogoShowing()).toBe(true);
  });

  it('falls back to the normal logo in dark mode when no dark one is set', () => {
    brand.set({ ...base, logoUrl: 'https://cdn.example.com/logo.png' });
    theme.set('dark');
    expect(brand.logo()).toBe('https://cdn.example.com/logo.png');
    expect(brand.darkLogoShowing()).toBe(false);
  });

  it('ignores a dark logo that is not https', () => {
    brand.set({ ...base, logoUrl: null, logoDarkUrl: 'http://insecure/logo-dark.png' });
    theme.set('dark');
    expect(brand.logo()).toBeNull();
  });
});

describe('BrandService neutral look', () => {
  let brand: BrandService;
  const blue = { name: 'Blue Cafe', primaryColor: '#1d4ed8', accentColor: '#0ea5e9', logoUrl: 'https://cdn.example.com/logo.png', radius: 'square' };
  const root = () => document.documentElement.style;

  beforeEach(() => {
    try { localStorage.clear(); } catch { /* no storage */ }
    TestBed.configureTestingModule({});
    brand = TestBed.inject(BrandService);
    brand.useNeutral(false);
  });

  it('replaces the cafe brand with the Yurt look and brings it back', () => {
    brand.set(blue);
    expect(root().getPropertyValue('--brand-500')).not.toBe('');

    brand.useNeutral(true);
    expect(brand.name()).toBe('Yurt');
    expect(brand.logo()).toBeNull();
    expect(root().getPropertyValue('--brand-500')).toBe('');
    expect(root().getPropertyValue('--radius-scale')).toBe('');

    brand.useNeutral(false);
    expect(brand.name()).toBe('Blue Cafe');
    expect(brand.logo()).toBe('https://cdn.example.com/logo.png');
    expect(root().getPropertyValue('--brand-500')).not.toBe('');
  });

  it('keeps a brand that arrives while neutral for later, without showing it', () => {
    brand.useNeutral(true);
    brand.set(blue);
    expect(brand.name()).toBe('Yurt');
    expect(root().getPropertyValue('--brand-500')).toBe('');

    brand.useNeutral(false);
    expect(brand.name()).toBe('Blue Cafe');
  });
});
