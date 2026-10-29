import { Component, inject, OnDestroy, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { YurtApiService, parseHex } from 'shared-api';
import { BrandConfig, Cafe } from 'shared-models';
import { ToastService } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';
import { MenuImportComponent } from '../menu/menu-import.component';

type Step = 'cafe' | 'look' | 'branch' | 'menu' | 'done';

/** Latin letters for Russian and Kazakh ones, so a cafe called "Кофейня Арай" gets the key "kofeynya-aray". */
const TRANSLIT: Record<string, string> = {
  а: 'a', б: 'b', в: 'v', г: 'g', д: 'd', е: 'e', ё: 'e', ж: 'zh', з: 'z', и: 'i', й: 'y', к: 'k', л: 'l', м: 'm',
  н: 'n', о: 'o', п: 'p', р: 'r', с: 's', т: 't', у: 'u', ф: 'f', х: 'h', ц: 'ts', ч: 'ch', ш: 'sh', щ: 'sch',
  ъ: '', ы: 'y', ь: '', э: 'e', ю: 'yu', я: 'ya', ә: 'a', ғ: 'g', қ: 'q', ң: 'n', ө: 'o', ұ: 'u', ү: 'u', һ: 'h', і: 'i',
};

export function cafeKeyFrom(name: string): string {
  return [...name.toLowerCase()]
    .map((ch) => TRANSLIT[ch] ?? ch)
    .join('')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 40)
    .replace(/-+$/g, '');
}

/** A temporary password that is easy to read out over the phone: no 0/O or 1/l look-alikes. */
export function temporaryPassword(): string {
  const chars = 'abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  const bytes = crypto.getRandomValues(new Uint8Array(12));
  return [...bytes].map((b) => chars[b % chars.length]).join('');
}

/**
 * Opens a new cafe step by step: the cafe and its first admin, its colours and logo, its first branch and its menu
 * from an Excel file. Only the first step is required; every later step acts on the new cafe and can be skipped and
 * done later on the usual screens.
 */
@Component({
  selector: 'app-cafe-wizard',
  standalone: true,
  imports: [FormsModule, RouterLink, AdminTranslatePipe, MenuImportComponent],
  templateUrl: './cafe-wizard.component.html',
})
export class CafeWizardComponent implements OnDestroy {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);

  readonly steps: Step[] = ['cafe', 'look', 'branch', 'menu', 'done'];
  readonly optionalSteps = ['look', 'branch', 'menu'] as const;
  step = signal<Step>('cafe');
  busy = signal(false);
  cafe = signal<Cafe | null>(null);
  /** Which optional steps were saved, for the summary. */
  done = signal<Record<'look' | 'branch' | 'menu', boolean>>({ look: false, branch: false, menu: false });

  // Step 1
  form = { name: '', slug: '', domain: '', adminUsername: '', adminPassword: temporaryPassword() };
  private slugEdited = false;
  private userEdited = false;

  // Step 2
  brand = signal<BrandConfig | null>(null);

  // Step 3
  branch = { name: '', address: '', contactPhone: '', opens: '08:00', closes: '22:00' };

  stepIndex(s: Step): number {
    return this.steps.indexOf(s);
  }

  nameChanged(name: string): void {
    this.form.name = name;
    if (!this.slugEdited) this.form.slug = cafeKeyFrom(name);
    if (!this.userEdited) this.form.adminUsername = this.form.slug ? `${this.form.slug}-admin`.slice(0, 50) : '';
  }

  slugChanged(slug: string): void {
    this.form.slug = slug;
    this.slugEdited = true;
    if (!this.userEdited) this.form.adminUsername = slug ? `${slug}-admin`.slice(0, 50) : '';
  }

  userChanged(username: string): void {
    this.form.adminUsername = username;
    this.userEdited = true;
  }

  newPassword(): void {
    this.form.adminPassword = temporaryPassword();
  }

  canCreate(): boolean {
    const f = this.form;
    return /^[a-z0-9][a-z0-9-]{0,38}[a-z0-9]$/.test(f.slug.trim()) && !!f.name.trim()
      && f.adminUsername.trim().length >= 3 && f.adminPassword.length >= 8;
  }

  createCafe(): void {
    const f = this.form;
    this.busy.set(true);
    this.api.createCafe({
      slug: f.slug.trim(), name: f.name.trim(), domain: f.domain.trim() || null,
      adminUsername: f.adminUsername.trim(), adminPassword: f.adminPassword,
    }).subscribe({
      next: (cafe) => {
        this.cafe.set(cafe);
        // The brand step reads and saves the new cafe's brand through the operator's cafe switch.
        this.api.platformCafe = cafe.slug;
        this.api.getBrand().subscribe({ next: (b) => this.brand.set({ ...b }) });
        this.branch.name = cafe.name;
        this.busy.set(false);
        this.toast.success(this.lang.t('plat.cafeCreated'));
        this.step.set('look');
      },
      error: () => this.busy.set(false),
    });
  }

  colorsValid(): boolean {
    const b = this.brand();
    return !!b && !!parseHex(b.primaryColor) && !!parseHex(b.accentColor);
  }

  logoInvalid(): boolean {
    const url = this.brand()?.logoUrl?.trim();
    return !!url && !/^https:\/\//i.test(url);
  }

  saveLook(): void {
    const b = this.brand();
    if (!b) return;
    this.busy.set(true);
    this.api.setBrand({ ...b, logoUrl: b.logoUrl?.trim() || null }).subscribe({
      next: (saved) => {
        this.brand.set({ ...saved });
        this.done.update((d) => ({ ...d, look: true }));
        this.busy.set(false);
        this.step.set('branch');
      },
      error: () => this.busy.set(false),
    });
  }

  canSaveBranch(): boolean {
    const b = this.branch;
    return !!b.name.trim() && !!b.address.trim() && !!b.opens && !!b.closes;
  }

  saveBranch(): void {
    const cafe = this.cafe();
    if (!cafe) return;
    const b = this.branch;
    // Same format as the Locations screen: one slot per weekday; the wizard opens every day with the same hours.
    const slots = [1, 2, 3, 4, 5, 6, 7].map((day) => ({ day, enabled: true, from: b.opens, to: b.closes }));
    this.busy.set(true);
    this.api.createLocation({
      name: b.name.trim(), address: b.address.trim(), contactPhone: b.contactPhone.trim(), workingHours: JSON.stringify(slots),
    }, cafe.slug).subscribe({
      next: () => {
        this.done.update((d) => ({ ...d, branch: true }));
        this.busy.set(false);
        this.step.set('menu');
      },
      error: () => this.busy.set(false),
    });
  }

  menuImported(): void {
    this.done.update((d) => ({ ...d, menu: true }));
  }

  skip(next: Step): void {
    this.step.set(next);
  }

  signInText(): string {
    const cafe = this.cafe();
    const t = (key: string, v: string) => this.lang.t(key, { v });
    return [
      t('wiz.si.cafe', cafe?.name ?? ''),
      // Without its own web address the cafe is reached by its key (the cafe's own app, or the X-Tenant header).
      cafe?.domain ? t('wiz.si.address', cafe.domain) : t('wiz.si.key', cafe?.slug ?? ''),
      t('wiz.si.user', this.form.adminUsername.trim()),
      t('wiz.si.pass', this.form.adminPassword),
      this.lang.t('wiz.si.note'),
    ].join('\n');
  }

  copySignIn(): void {
    navigator.clipboard?.writeText(this.signInText()).then(
      () => this.toast.success(this.lang.t('wiz.copied')),
      () => {},
    );
  }

  ngOnDestroy(): void {
    this.api.platformCafe = null;
  }
}
