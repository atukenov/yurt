import { Component, computed, effect, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { YurtApiService, RADIUS_SCALE, brandFontStack, fontFileUrl, setFontFace, buildScale, parseHex, readableOn, paletteWarning as paletteWarningFor } from 'shared-api';
import { ToastService, LocalDatePipe } from 'shared-ui';
import { CafeAccess, FiscalSettings, PaymentSettings, PlatformBilling, LoyaltyCompareResult, LoyaltyImportResult, LoyaltyLedgerInfo, BrandConfig, Cafe, CafeSecretStatus, BrandLink, IntegrationStatus, PlatformModule } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { PictureUploadComponent } from '../../shared/picture-upload/picture-upload.component';
import { AdminLangService } from '../../core/lang.service';

interface ModuleEdit {
  enabled: boolean;
  validUntil: string; // yyyy-MM-dd or ''
  source: 'Manual' | 'Trial' | 'Paid';
  notes: string;
}

@Component({
  selector: 'app-platform',
  standalone: true,
  imports: [LocalDatePipe, CommonModule, FormsModule, RouterLink, AdminTranslatePipe, PictureUploadComponent],
  templateUrl: './platform.component.html',
})
export class PlatformComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);

  modules = signal<PlatformModule[]>([]);
  integrations = signal<IntegrationStatus[]>([]);
  loading = signal(true);
  savingKey = signal<string | null>(null);
  edits: Record<string, ModuleEdit> = {};

  maintenanceEnabled = false;
  maintenanceMessage = '';
  savingMaintenance = signal(false);

  readonly sources: ModuleEdit['source'][] = ['Manual', 'Trial', 'Paid'];

  // ── Cafes ──
  /** Slug of the cafe the module, maintenance and brand sections act on; '' = the cafe of this address. */
  managedCafe = '';

  selectManagedCafe(slug: string): void {
    this.managedCafe = slug;
    this.api.platformCafe = slug || null;
    this.loading.set(true);
    this.api.getPlatformModules().subscribe({
      next: (m) => { this.setModules(m); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
    this.api.getMaintenance().subscribe({ next: (m) => { this.maintenanceEnabled = m.enabled; this.maintenanceMessage = m.message; } });
    this.api.getBrand().subscribe({ next: (b) => this.brand.set(b) });
    this.loadLedger();
    this.loadFiscal();
    this.loadPayments();
    this.loadBilling();
    this.loadAccess();
  }

  // ── Loyalty wallet (iiko → own wallet) ──
  ledger = signal<LoyaltyLedgerInfo | null>(null);
  importResult = signal<LoyaltyImportResult | null>(null);
  compareResult = signal<LoyaltyCompareResult | null>(null);
  loyaltyBusy = signal(false);

  private loadLedger(): void {
    this.importResult.set(null);
    this.compareResult.set(null);
    this.api.getLoyaltyLedger().subscribe({ next: (l) => this.ledger.set(l), error: () => this.ledger.set(null) });
  }

  private loyaltyCall<T>(call: () => import('rxjs').Observable<T>, done: (r: T) => void): void {
    this.loyaltyBusy.set(true);
    call().subscribe({
      next: (r) => { done(r); this.loyaltyBusy.set(false); },
      error: () => this.loyaltyBusy.set(false),
    });
  }

  importLoyalty(): void {
    if (!confirm(this.lang.t('plat.loyalty.confirmImport'))) return;
    this.loyaltyCall(() => this.api.importLoyalty(), (r) => this.importResult.set(r));
  }

  compareLoyalty(): void {
    this.loyaltyCall(() => this.api.compareLoyalty(), (r) => this.compareResult.set(r));
  }

  switchLedger(mode: 'iiko' | 'native'): void {
    if (!confirm(this.lang.t(mode === 'native' ? 'plat.loyalty.confirmNative' : 'plat.loyalty.confirmIiko'))) return;
    this.loyaltyCall(() => this.api.setLoyaltyLedger(mode), (l) => { this.ledger.set(l); this.toast.success(this.lang.t('common.saved')); });
  }

  // ── Official tax receipts (cloud cash register) ──
  fiscal = signal<FiscalSettings | null>(null);
  fiscalProvider = '';
  fiscalVat: number | null = null;
  fiscalSkip: Record<string, boolean> = {};
  fiscalRegisters: Record<string, string> = {};
  savingFiscal = signal(false);
  readonly fiscalMethods = ['Cash', 'Card', 'KaspiBank', 'HalykBank', 'FreedomBank', 'ApplePay', 'Other'];

  // ── Limited mode (cafe stopped paying) ──
  access = signal<CafeAccess | null>(null);
  /** "YYYY-MM-DD" for the date input; '' = not tracked. */
  accessPaidUntil = '';
  accessByHand = false;
  savingAccess = signal(false);

  private loadAccess(): void {
    this.access.set(null);
    this.api.getCafeAccess().subscribe({ next: (a) => this.setAccess(a), error: () => this.access.set(null) });
  }

  private setAccess(a: CafeAccess): void {
    this.access.set(a);
    this.accessPaidUntil = a.paidUntil ? a.paidUntil.slice(0, 10) : '';
    this.accessByHand = a.limitedByOperator;
  }

  saveAccess(): void {
    this.savingAccess.set(true);
    // The paid day counts in full: the period ends at the end of that day (UTC).
    const paidUntil = this.accessPaidUntil ? `${this.accessPaidUntil}T23:59:59Z` : null;
    this.api.setCafeAccess({ paidUntil, limitedByOperator: this.accessByHand }).subscribe({
      next: (saved) => { this.setAccess(saved); this.savingAccess.set(false); this.toast.success(this.lang.t('common.saved')); },
      error: () => this.savingAccess.set(false),
    });
  }

  accessBadge(state: CafeAccess['state']): string {
    switch (state) {
      case 'full': return 'bg-emerald-100 text-emerald-700';
      case 'grace': return 'bg-amber-100 text-amber-700';
      default: return 'bg-red-100 text-red-700';
    }
  }

  // ── Online payments (Stripe) ──
  payments = signal<PaymentSettings | null>(null);
  paymentProvider = '';
  savingPayments = signal(false);

  private loadPayments(): void {
    this.payments.set(null);
    this.api.getPaymentSettings().subscribe({ next: (p) => this.setPayments(p), error: () => this.payments.set(null) });
  }

  private setPayments(p: PaymentSettings): void {
    this.payments.set(p);
    this.paymentProvider = p.provider ?? '';
  }

  savePayments(): void {
    this.savingPayments.set(true);
    this.api.setPaymentSettings(this.paymentProvider || null).subscribe({
      next: (saved) => { this.setPayments(saved); this.savingPayments.set(false); this.toast.success(this.lang.t('common.saved')); },
      error: () => this.savingPayments.set(false),
    });
  }

  // ── Monthly billing (platform owner's Stripe account) ──
  billing = signal<PlatformBilling | null>(null);
  billingPrice: number | null = null;
  billingCurrency = 'usd';
  billingModules: Record<string, boolean> = {};
  savingBilling = signal(false);

  private loadBilling(): void {
    this.billing.set(null);
    this.api.getPlatformBilling().subscribe({ next: (b) => this.setBilling(b), error: () => this.billing.set(null) });
  }

  private setBilling(b: PlatformBilling): void {
    this.billing.set(b);
    this.billingPrice = b.monthlyPrice;
    this.billingCurrency = b.currency;
    this.billingModules = Object.fromEntries(b.availableModules.map((k) => [k, b.modules.includes(k)]));
  }

  saveBilling(): void {
    const b = this.billing();
    if (!b) return;
    this.savingBilling.set(true);
    this.api.setPlatformBilling({
      monthlyPrice: this.billingPrice === null || (this.billingPrice as unknown) === '' ? null : Number(this.billingPrice),
      currency: this.billingCurrency,
      modules: b.availableModules.filter((k) => this.billingModules[k]),
    }).subscribe({
      next: (saved) => { this.setBilling(saved); this.savingBilling.set(false); this.toast.success(this.lang.t('common.saved')); },
      error: () => this.savingBilling.set(false),
    });
  }

  copyWebhookUrl(url: string): void {
    navigator.clipboard?.writeText(url).then(
      () => this.toast.success(this.lang.t('plat.pay.copied')),
      () => {},
    );
  }

  private loadFiscal(): void {
    this.fiscal.set(null);
    this.api.getFiscalSettings().subscribe({ next: (f) => this.setFiscal(f), error: () => this.fiscal.set(null) });
  }

  private setFiscal(f: FiscalSettings): void {
    this.fiscal.set(f);
    this.fiscalProvider = f.provider ?? '';
    this.fiscalVat = f.vatPercent ?? null;
    this.fiscalSkip = Object.fromEntries(this.fiscalMethods.map((m) => [m, f.skipMethods.includes(m)]));
    this.fiscalRegisters = Object.fromEntries(f.branches.map((b) => [b.locationId, b.registerNumber ?? '']));
  }

  saveFiscal(): void {
    const f = this.fiscal();
    if (!f) return;
    // Turning receipts on is the cut-over step: iiko or the old register must stop making them the same day.
    if (!f.provider && this.fiscalProvider && !confirm(this.lang.t('plat.fiscal.confirmOn'))) return;
    this.savingFiscal.set(true);
    this.api.setFiscalSettings({
      provider: this.fiscalProvider || null,
      skipMethods: this.fiscalMethods.filter((m) => this.fiscalSkip[m]),
      vatPercent: this.fiscalVat === null || (this.fiscalVat as unknown) === '' ? null : Number(this.fiscalVat),
      branches: f.branches.map((b) => ({ locationId: b.locationId, registerNumber: (this.fiscalRegisters[b.locationId] ?? '').trim() || null })),
    }).subscribe({
      next: (saved) => { this.setFiscal(saved); this.savingFiscal.set(false); this.toast.success(this.lang.t('common.saved')); },
      error: () => this.savingFiscal.set(false),
    });
  }

  cafes = signal<Cafe[]>([]);
  newCafe = { slug: '', name: '', domain: '', adminUsername: '', adminPassword: '' };
  creatingCafe = signal(false);

  canCreateCafe(): boolean {
    const c = this.newCafe;
    return !!c.slug.trim() && !!c.name.trim() && c.adminUsername.trim().length >= 3 && c.adminPassword.length >= 8;
  }

  createCafe(): void {
    const c = this.newCafe;
    this.creatingCafe.set(true);
    this.api.createCafe({
      slug: c.slug.trim(), name: c.name.trim(), domain: c.domain.trim() || null,
      adminUsername: c.adminUsername.trim(), adminPassword: c.adminPassword,
    }).subscribe({
      next: (cafe) => {
        this.cafes.update((l) => [...l, cafe].sort((a, b) => a.name.localeCompare(b.name)));
        this.newCafe = { slug: '', name: '', domain: '', adminUsername: '', adminPassword: '' };
        this.creatingCafe.set(false);
        this.toast.success(this.lang.t('plat.cafeCreated'));
      },
      error: () => this.creatingCafe.set(false),
    });
  }

  secretsCafeId = signal<string | null>(null);
  secrets = signal<CafeSecretStatus[]>([]);
  secretValues: Record<string, string> = {};

  openSecrets(cafe: Cafe): void {
    if (this.secretsCafeId() === cafe.id) { this.secretsCafeId.set(null); return; }
    this.secretValues = {};
    this.secrets.set([]);
    this.secretsCafeId.set(cafe.id);
    this.api.getCafeSecrets(cafe.id).subscribe({ next: (s) => this.secrets.set(s) });
  }

  private patchSecret(saved: CafeSecretStatus): void {
    this.secrets.update((l) => l.map((s) => (s.key === saved.key ? saved : s)));
    this.secretValues[saved.key] = '';
  }

  saveSecret(key: string): void {
    const id = this.secretsCafeId();
    const value = (this.secretValues[key] ?? '').trim();
    if (!id || !value) return;
    this.api.setCafeSecret(id, key, value).subscribe({ next: (s) => { this.patchSecret(s); this.toast.success(this.lang.t('common.saved')); } });
  }

  clearSecret(key: string): void {
    const id = this.secretsCafeId();
    if (!id) return;
    this.api.clearCafeSecret(id, key).subscribe({ next: (s) => this.patchSecret(s) });
  }

  toggleCafe(cafe: Cafe): void {
    this.api.setCafeActive(cafe.id, !cafe.isActive).subscribe({
      next: (saved) => this.cafes.update((l) => l.map((x) => (x.id === saved.id ? saved : x))),
    });
  }

  exportingCafeId = signal<string | null>(null);

  /** Downloads everything the cafe owns as a ZIP (menu, orders, customers, stock, settings), e.g. when it leaves Yurt. */
  exportCafe(cafe: Cafe): void {
    if (!confirm(this.lang.t('plat.cafeExportConfirm', { name: cafe.name }))) return;
    this.exportingCafeId.set(cafe.id);
    this.api.exportCafe(cafe.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${cafe.slug}-data-${new Date().toISOString().slice(0, 10)}.zip`;
        a.click();
        URL.revokeObjectURL(url);
        this.exportingCafeId.set(null);
      },
      error: () => this.exportingCafeId.set(null),
    });
  }

  // ── Brand ──
  private static readonly DEFAULT_BRAND: BrandConfig = {
    name: 'Yurt', primaryColor: '#ffd119', accentColor: '#ea580c', logoUrl: null, logoDarkUrl: null, supportContact: null, iconUrl: null, splashUrl: null, welcomeText: null, links: [], radius: null, font: null, fontUrl: null,
  };
  brand = signal<BrandConfig>({ ...PlatformComponent.DEFAULT_BRAND });
  savingBrand = signal(false);
  /** The preview loads the typed font file under its own name, so the live admin font is untouched until saved. */
  private static readonly PREVIEW_FONT = 'Yurt Brand Preview';
  private readonly previewFontFace = effect((onCleanup) => {
    setFontFace(document, 'brand-preview-font-face', PlatformComponent.PREVIEW_FONT, this.brand().fontUrl);
    onCleanup(() => document.getElementById('brand-preview-font-face')?.remove());
  });

  /** Scoped CSS variables for the preview only; the real app is not touched until the brand is saved. */
  previewVars = computed(() => {
    const b = this.brand();
    const scale = buildScale(b.primaryColor) ?? buildScale(PlatformComponent.DEFAULT_BRAND.primaryColor)!;
    const base = parseHex(b.primaryColor) ?? parseHex(PlatformComponent.DEFAULT_BRAND.primaryColor)!;
    const vars: Record<string, string> = { '--p-on': readableOn(base) };
    for (const [step, hex] of Object.entries(scale)) vars[`--p-${step}`] = hex;
    vars['--p-accent'] = parseHex(b.accentColor) ? b.accentColor : PlatformComponent.DEFAULT_BRAND.accentColor;
    const scale2 = RADIUS_SCALE[b.radius ?? ''] ?? 1;
    for (const [name, rem] of [['lg', 0.5], ['xl', 0.75], ['2xl', 1], ['3xl', 1.5]] as const) vars[`--radius-${name}`] = `${rem * scale2}rem`;
    vars['font-family'] = brandFontStack(b.font, b.fontUrl, PlatformComponent.PREVIEW_FONT) ?? "'Inter', system-ui, sans-serif";
    return vars;
  });
  paletteWarning = computed(() => paletteWarningFor(this.brand().primaryColor));
  /** True when the font file box has text that is not a usable https font link. */
  fontUrlInvalid = computed(() => !!this.brand().fontUrl?.trim() && !fontFileUrl(this.brand().fontUrl));
  logoOk = computed(() => /^(https:\/\/|http:\/\/(localhost|127\.0\.0\.1)[:/])/i.test(this.brand().logoUrl ?? ''));
  colorsValid = computed(() => !!parseHex(this.brand().primaryColor) && !!parseHex(this.brand().accentColor));

  patchBrand(patch: Partial<BrandConfig>): void {
    this.brand.update((b) => ({ ...b, ...patch }));
  }

  addBrandLink(): void {
    this.brand.update((b) => ({ ...b, links: [...(b.links ?? []), { label: '', url: '' }] }));
  }

  patchBrandLink(index: number, patch: Partial<BrandLink>): void {
    this.brand.update((b) => ({ ...b, links: (b.links ?? []).map((l, i) => (i === index ? { ...l, ...patch } : l)) }));
  }

  removeBrandLink(index: number): void {
    this.brand.update((b) => ({ ...b, links: (b.links ?? []).filter((_, i) => i !== index) }));
  }

  resetBrand(): void {
    this.brand.set({ ...PlatformComponent.DEFAULT_BRAND });
  }

  saveBrand(): void {
    const b = this.brand();
    this.savingBrand.set(true);
    this.api.setBrand({
      name: b.name.trim(),
      primaryColor: b.primaryColor.trim(),
      accentColor: b.accentColor.trim(),
      logoUrl: b.logoUrl?.trim() || null,
      logoDarkUrl: b.logoDarkUrl?.trim() || null,
      supportContact: b.supportContact?.trim() || null,
      iconUrl: b.iconUrl?.trim() || null,
      splashUrl: b.splashUrl?.trim() || null,
      welcomeText: b.welcomeText?.trim() || null,
      radius: b.radius || null,
      font: b.font || null,
      fontUrl: b.fontUrl?.trim() || null,
      links: (b.links ?? []).filter((l) => l.label.trim() || l.url.trim()).map((l) => ({ label: l.label.trim(), url: l.url.trim() })),
    }).subscribe({
      next: (saved) => {
        this.brand.set(saved);
        this.savingBrand.set(false);
        this.toast.success(this.lang.t('plat.brandSaved'));
      },
      error: () => this.savingBrand.set(false),
    });
  }

  ngOnInit(): void {
    this.api.platformCafe = null;
    this.api.getPlatformModules().subscribe({
      next: (m) => { this.setModules(m); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
    this.api.getMaintenance().subscribe({
      next: (m) => { this.maintenanceEnabled = m.enabled; this.maintenanceMessage = m.message; },
    });
    this.api.getCafes().subscribe({ next: (c) => this.cafes.set(c) });
    this.api.getBrand().subscribe({ next: (b) => this.brand.set(b) });
    this.loadLedger();
    this.loadFiscal();
    this.loadPayments();
    this.loadBilling();
    this.loadAccess();
    this.api.getIntegrationStatus().subscribe({ next: (i) => this.integrations.set(i) });
  }

  private setModules(list: PlatformModule[]): void {
    this.modules.set(list);
    for (const m of list) this.edits[m.key] = this.toEdit(m);
  }

  private toEdit(m: PlatformModule): ModuleEdit {
    return {
      enabled: m.enabled || m.state === 'grace',
      validUntil: m.validUntil ? m.validUntil.slice(0, 10) : '',
      source: m.source === 'Trial' || m.source === 'Paid' ? m.source : 'Manual',
      notes: m.notes ?? '',
    };
  }

  isDirty(m: PlatformModule): boolean {
    const e = this.edits[m.key];
    const o = this.toEdit(m);
    return !!e && (e.enabled !== o.enabled || e.validUntil !== o.validUntil || e.source !== o.source || e.notes !== o.notes);
  }

  saveModule(m: PlatformModule): void {
    const e = this.edits[m.key];
    this.savingKey.set(m.key);
    this.api.updatePlatformModule(m.key, {
      enabled: e.enabled,
      validUntil: e.validUntil ? `${e.validUntil}T23:59:59Z` : null,
      source: e.source,
      notes: e.notes.trim() || null,
    }).subscribe({
      next: () => {
        // Re-read the whole list so dependency warnings for other modules refresh too.
        this.api.getPlatformModules().subscribe((list) => this.setModules(list));
        this.savingKey.set(null);
        this.toast.success(this.lang.t('common.saved'));
      },
      error: () => this.savingKey.set(null),
    });
  }

  saveMaintenance(): void {
    this.savingMaintenance.set(true);
    this.api.setMaintenance({ enabled: this.maintenanceEnabled, message: this.maintenanceMessage.trim() }).subscribe({
      next: (m) => {
        this.maintenanceEnabled = m.enabled;
        this.maintenanceMessage = m.message;
        this.savingMaintenance.set(false);
        this.toast.success(m.enabled ? this.lang.t('plat.maintenanceOn') : this.lang.t('plat.maintenanceOff'));
      },
      error: () => this.savingMaintenance.set(false),
    });
  }

  badgeClass(m: PlatformModule): string {
    switch (m.state) {
      case 'active': return 'bg-emerald-100 text-emerald-700';
      case 'grace': return 'bg-brand-50 text-brand-ink';
      default: return 'bg-slate-100 text-slate-600';
    }
  }
}
