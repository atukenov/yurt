import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthStateService, YurtApiService } from 'shared-api';
import { Currency2Pipe, LocalNumberPipe, ToastService } from 'shared-ui';
import { Combo, ComboReport, Location, MenuCategory, MenuItem, SaveCombo } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';
import { MenuSectionsComponent } from '../../shared/menu-sections/menu-sections.component';
import { currentMonth } from '../payroll/payroll.component';

/** One row of the combo form: a dish, its size (when it has sizes) and how many. */
export interface PartRow {
  itemId: string;
  variantId: string | null;
  quantity: number;
}

/** What the dishes of a form cost bought separately; a dish with a chosen size costs that size's price. */
export function regularTotal(rows: PartRow[], items: MenuItem[]): number {
  return rows.reduce((sum, r) => {
    const item = items.find((i) => i.id === r.itemId);
    if (!item) return sum;
    const price = item.variants?.find((v) => v.id === r.variantId)?.price ?? item.price;
    return sum + price * Math.max(0, r.quantity);
  }, 0);
}

/** Why the form can't be saved yet, as a translation key, or null. */
export function formProblem(name: string, price: number | null, categoryId: string, rows: PartRow[]): string | null {
  if (!name.trim()) return 'combos.errName';
  if (!categoryId) return 'combos.errCategory';
  if (price == null || price < 0) return 'combos.errPrice';
  const filled = rows.filter((r) => r.itemId);
  if (filled.length === 0 || filled.reduce((s, r) => s + r.quantity, 0) < 2) return 'combos.errParts';
  return null;
}

/**
 * Combos: dishes sold together at one set price (coffee + pastry). Staff who manage the menu make them here; the till and
 * the guest app list them like any dish. The money report below is for the CEO only.
 */
@Component({
  selector: 'app-combos',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe, Currency2Pipe, LocalNumberPipe, MenuSectionsComponent],
  template: `
    <div class="p-4 md:p-6 max-w-5xl mx-auto space-y-5">
      <app-menu-sections />
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-slate-800">{{ 'combos.title' | t }}</h1>
          <p class="text-slate-500 text-sm mt-0.5">{{ 'combos.hint' | t }}</p>
        </div>
        @if (!editing()) {
          <button type="button" (click)="startNew()" class="px-4 py-2 rounded-xl bg-slate-900 text-white text-sm font-semibold">
            {{ 'combos.new' | t }}
          </button>
        }
      </div>

      @if (editing()) {
        <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5 space-y-4">
          <h2 class="text-lg font-semibold text-slate-800">{{ (editingId() ? 'combos.edit' : 'combos.new') | t }}</h2>
          <div class="grid gap-3 sm:grid-cols-2">
            <label class="text-sm text-slate-600">{{ 'combos.name' | t }}
              <input [(ngModel)]="name" maxlength="200" class="mt-1 w-full px-3 py-2 rounded-xl border border-slate-200 text-sm" />
            </label>
            <label class="text-sm text-slate-600">{{ 'combos.category' | t }}
              <select [(ngModel)]="categoryId" class="mt-1 w-full px-3 py-2 rounded-xl border border-slate-200 text-sm">
                <option value="">—</option>
                @for (c of categories(); track c.id) { <option [value]="c.id">{{ c.name }}</option> }
              </select>
            </label>
            <label class="text-sm text-slate-600">{{ 'combos.nameRu' | t }}
              <input [(ngModel)]="nameRu" maxlength="200" class="mt-1 w-full px-3 py-2 rounded-xl border border-slate-200 text-sm" />
            </label>
            <label class="text-sm text-slate-600">{{ 'combos.nameKk' | t }}
              <input [(ngModel)]="nameKk" maxlength="200" class="mt-1 w-full px-3 py-2 rounded-xl border border-slate-200 text-sm" />
            </label>
            <label class="text-sm text-slate-600 sm:col-span-2">{{ 'combos.description' | t }}
              <input [(ngModel)]="description" maxlength="1000" class="mt-1 w-full px-3 py-2 rounded-xl border border-slate-200 text-sm" />
            </label>
          </div>

          <div class="space-y-2">
            <h3 class="text-sm font-semibold text-slate-700">{{ 'combos.parts' | t }}</h3>
            @for (row of rows(); track $index; let i = $index) {
              <div class="flex flex-wrap items-center gap-2">
                <select [ngModel]="row.itemId" (ngModelChange)="setItem(i, $event)" [attr.aria-label]="'combos.dish' | t"
                  class="flex-1 min-w-48 px-3 py-2 rounded-xl border border-slate-200 text-sm">
                  <option value="">{{ 'combos.pickDish' | t }}</option>
                  @for (d of dishes(); track d.id) { <option [value]="d.id">{{ d.name }} · {{ d.price | currency2 }}</option> }
                </select>
                @if (variantsOf(row.itemId).length > 0) {
                  <select [ngModel]="row.variantId" (ngModelChange)="setVariant(i, $event)" [attr.aria-label]="'combos.size' | t"
                    class="px-3 py-2 rounded-xl border border-slate-200 text-sm">
                    <option [ngValue]="null">{{ 'combos.anySize' | t }}</option>
                    @for (v of variantsOf(row.itemId); track v.id) { <option [ngValue]="v.id">{{ v.label }}</option> }
                  </select>
                }
                <input type="number" min="1" max="20" [ngModel]="row.quantity" (ngModelChange)="setQuantity(i, $event)"
                  [attr.aria-label]="'combos.quantity' | t" class="w-20 px-3 py-2 rounded-xl border border-slate-200 text-sm" />
                <button type="button" (click)="removeRow(i)" class="px-3 py-2 rounded-xl text-sm text-red-600 hover:bg-red-50">
                  {{ 'combos.remove' | t }}
                </button>
              </div>
            }
            <button type="button" (click)="addRow()" class="text-sm font-semibold text-slate-700 underline">+ {{ 'combos.addDish' | t }}</button>
          </div>

          <div class="grid gap-3 sm:grid-cols-3 items-end">
            <label class="text-sm text-slate-600">{{ 'combos.price' | t }}
              <input type="number" min="0" [(ngModel)]="price" class="mt-1 w-full px-3 py-2 rounded-xl border border-slate-200 text-sm" />
            </label>
            <p class="text-sm text-slate-600">{{ 'combos.separately' | t }}: <strong>{{ separately() | currency2 }}</strong></p>
            <p class="text-sm" [class.text-emerald-700]="saving() > 0" [class.text-red-600]="saving() < 0">
              {{ (saving() >= 0 ? 'combos.guestSaves' : 'combos.dearer') | t }}: <strong>{{ absSaving() | currency2 }}</strong>
            </p>
          </div>

          @if (locations().length > 1) {
            <fieldset class="space-y-1">
              <legend class="text-sm font-semibold text-slate-700">{{ 'combos.branches' | t }}</legend>
              <p class="text-xs text-slate-500">{{ 'combos.branchesHint' | t }}</p>
              <div class="flex flex-wrap gap-3">
                @for (l of locations(); track l.id) {
                  <label class="flex items-center gap-1.5 text-sm text-slate-700">
                    <input type="checkbox" [checked]="locationIds().includes(l.id)" (change)="toggleLocation(l.id)" /> {{ l.name }}
                  </label>
                }
              </div>
            </fieldset>
          }

          <label class="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" [(ngModel)]="isAvailable" /> {{ 'combos.available' | t }}
          </label>

          @if (problem(); as p) { <p class="text-sm text-amber-700">{{ p | t }}</p> }
          <div class="flex gap-2">
            <button type="button" (click)="save()" [disabled]="!!problem() || saving$()"
              class="px-4 py-2 rounded-xl bg-slate-900 text-white text-sm font-semibold disabled:opacity-40">{{ 'combos.save' | t }}</button>
            <button type="button" (click)="cancel()" class="px-4 py-2 rounded-xl border border-slate-200 text-sm">{{ 'combos.cancel' | t }}</button>
          </div>
        </section>
      }

      @if (loading()) {
        <div class="h-32 bg-slate-100 rounded-2xl animate-pulse"></div>
      } @else if (combos().length === 0 && !editing()) {
        <p class="text-sm text-slate-500">{{ 'combos.none' | t }}</p>
      } @else {
        <div class="grid gap-3 md:grid-cols-2">
          @for (c of combos(); track c.id) {
            <article class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 space-y-2">
              <div class="flex items-start justify-between gap-2">
                <div>
                  <h2 class="font-semibold text-slate-800">{{ c.name }}</h2>
                  <p class="text-xs text-slate-500">{{ c.categoryName }}</p>
                </div>
                <p class="text-lg font-bold text-slate-800">{{ c.price | currency2 }}</p>
              </div>
              <ul class="text-sm text-slate-600 list-disc pl-5">
                @for (p of c.parts; track p.itemId + (p.variantId ?? '')) {
                  <li>{{ p.quantity }} × {{ p.itemName }}@if (p.variantLabel) { ({{ p.variantLabel }}) }</li>
                }
              </ul>
              <p class="text-xs text-slate-500">
                {{ 'combos.separately' | t }}: {{ c.regularTotal | currency2 }} ·
                {{ 'combos.guestSaves' | t }}: {{ c.saving | currency2 }} · {{ 'combos.sold' | t }}: {{ c.timesSold }}
              </p>
              @if (!c.isAvailable) { <p class="text-xs font-semibold text-slate-500">{{ 'combos.off' | t }}</p> }
              @else if (c.blocked) { <p class="text-xs font-semibold text-amber-700">{{ 'combos.blocked' | t }}</p> }
              <div class="flex gap-2 pt-1">
                <button type="button" (click)="startEdit(c)" class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm">{{ 'combos.edit' | t }}</button>
                <button type="button" (click)="remove(c)" class="px-3 py-1.5 rounded-xl text-sm text-red-600 hover:bg-red-50">{{ 'combos.delete' | t }}</button>
              </div>
            </article>
          }
        </div>
      }

      @if (isCeo) {
        <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5 space-y-3">
          <div class="flex flex-wrap items-center gap-3">
            <h2 class="text-lg font-semibold text-slate-800">{{ 'combos.report' | t }}</h2>
            <input type="month" [ngModel]="month()" (ngModelChange)="setMonth($event)" [attr.aria-label]="'payroll.month' | t"
              class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm" />
          </div>
          @if (report(); as r) {
            @if (r.items.length === 0) {
              <p class="text-sm text-slate-500">{{ 'combos.reportNone' | t }}</p>
            } @else {
              <div class="grid gap-3 sm:grid-cols-4">
                <div class="rounded-xl bg-slate-50 p-3"><p class="text-xs text-slate-500">{{ 'combos.sold' | t }}</p><p class="text-xl font-bold text-slate-800">{{ r.totals.sold | lnumber }}</p></div>
                <div class="rounded-xl bg-slate-50 p-3"><p class="text-xs text-slate-500">{{ 'combos.revenue' | t }}</p><p class="text-xl font-bold text-slate-800">{{ r.totals.revenue | currency2 }}</p></div>
                <div class="rounded-xl bg-slate-50 p-3"><p class="text-xs text-slate-500">{{ 'combos.guestSaves' | t }}</p><p class="text-xl font-bold text-slate-800">{{ r.totals.saved | currency2 }}</p></div>
                <div class="rounded-xl bg-slate-50 p-3"><p class="text-xs text-slate-500">{{ 'combos.share' | t }}</p><p class="text-xl font-bold text-slate-800">{{ r.totals.shareOfSales | lnumber }}%</p></div>
              </div>
              <div class="overflow-x-auto">
                <table class="w-full text-sm">
                  <thead>
                    <tr class="border-b border-slate-100 text-slate-500 text-xs uppercase tracking-wide">
                      <th class="text-left px-3 py-2">{{ 'combos.name' | t }}</th>
                      <th class="text-right px-3 py-2">{{ 'combos.sold' | t }}</th>
                      <th class="text-right px-3 py-2">{{ 'combos.revenue' | t }}</th>
                      <th class="text-right px-3 py-2">{{ 'combos.guestSaves' | t }}</th>
                      <th class="text-right px-3 py-2">{{ 'combos.cost' | t }}</th>
                      <th class="text-right px-3 py-2">{{ 'combos.profit' | t }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (i of r.items; track i.comboId) {
                      <tr class="border-b border-slate-50">
                        <td class="px-3 py-2">{{ i.name }}</td>
                        <td class="px-3 py-2 text-right">{{ i.sold | lnumber }}</td>
                        <td class="px-3 py-2 text-right">{{ i.revenue | currency2 }}</td>
                        <td class="px-3 py-2 text-right">{{ i.saved | currency2 }}</td>
                        <td class="px-3 py-2 text-right">@if (i.cost !== null) { {{ i.cost | currency2 }} } @else { — }</td>
                        <td class="px-3 py-2 text-right font-semibold">@if (i.profit !== null) { {{ i.profit | currency2 }} } @else { — }</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
              <p class="text-xs text-slate-500">{{ 'combos.reportNote' | t }}</p>
            }
          }
        </section>
      }
    </div>
  `,
})
export class CombosComponent implements OnInit {
  private readonly api = inject(YurtApiService);
  private readonly toast = inject(ToastService);
  private readonly lang = inject(AdminLangService);
  readonly isCeo = inject(AuthStateService).isCeo;

  readonly combos = signal<Combo[]>([]);
  readonly dishes = signal<MenuItem[]>([]);
  readonly categories = signal<MenuCategory[]>([]);
  readonly locations = signal<Location[]>([]);
  readonly loading = signal(true);
  readonly report = signal<ComboReport | null>(null);
  readonly month = signal(currentMonth());

  readonly editing = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly rows = signal<PartRow[]>([]);
  readonly locationIds = signal<string[]>([]);
  readonly saving$ = signal(false);
  name = '';
  nameRu = '';
  nameKk = '';
  description = '';
  categoryId = '';
  price: number | null = null;
  isAvailable = true;

  readonly separately = computed(() => regularTotal(this.rows(), this.dishes()));
  readonly saving = computed(() => this.separately() - (this.price ?? 0));
  readonly absSaving = computed(() => Math.abs(this.saving()));
  readonly problem = computed(() => formProblem(this.name, this.price, this.categoryId, this.rows()));

  ngOnInit(): void {
    this.api.adminGetMenuItems().subscribe({ next: (d) => this.dishes.set(d), error: () => undefined });
    this.api.adminGetCategories().subscribe({ next: (c) => this.categories.set(c), error: () => undefined });
    this.api.getAdminLocations().subscribe({ next: (l) => this.locations.set(l), error: () => undefined });
    this.reload();
    if (this.isCeo) this.loadReport();
  }

  variantsOf(itemId: string) {
    return this.dishes().find((d) => d.id === itemId)?.variants ?? [];
  }

  startNew(): void {
    this.editingId.set(null);
    this.name = this.nameRu = this.nameKk = this.description = '';
    this.categoryId = this.categories()[0]?.id ?? '';
    this.price = null;
    this.isAvailable = true;
    this.locationIds.set([]);
    this.rows.set([{ itemId: '', variantId: null, quantity: 1 }, { itemId: '', variantId: null, quantity: 1 }]);
    this.editing.set(true);
  }

  startEdit(c: Combo): void {
    this.editingId.set(c.id);
    this.name = c.name;
    this.nameRu = c.nameRu ?? '';
    this.nameKk = c.nameKk ?? '';
    this.description = c.description;
    this.categoryId = c.categoryId;
    this.price = c.price;
    this.isAvailable = c.isAvailable;
    this.locationIds.set([...c.locationIds]);
    this.rows.set(c.parts.map((p) => ({ itemId: p.itemId, variantId: p.variantId, quantity: p.quantity })));
    this.editing.set(true);
  }

  cancel(): void { this.editing.set(false); }

  addRow(): void { this.rows.update((r) => [...r, { itemId: '', variantId: null, quantity: 1 }]); }
  removeRow(i: number): void { this.rows.update((r) => r.filter((_, k) => k !== i)); }
  setItem(i: number, itemId: string): void { this.patchRow(i, { itemId, variantId: null }); }
  setVariant(i: number, variantId: string | null): void { this.patchRow(i, { variantId }); }
  setQuantity(i: number, q: number): void { this.patchRow(i, { quantity: Math.min(20, Math.max(1, Math.floor(Number(q) || 1))) }); }
  private patchRow(i: number, patch: Partial<PartRow>): void {
    this.rows.update((r) => r.map((row, k) => (k === i ? { ...row, ...patch } : row)));
  }

  toggleLocation(id: string): void {
    this.locationIds.update((l) => (l.includes(id) ? l.filter((x) => x !== id) : [...l, id]));
  }

  save(): void {
    if (this.problem() || this.saving$()) return;
    const body: SaveCombo = {
      name: this.name.trim(),
      nameRu: this.nameRu.trim() || null,
      nameKk: this.nameKk.trim() || null,
      description: this.description.trim() || null,
      categoryId: this.categoryId,
      price: this.price ?? 0,
      isAvailable: this.isAvailable,
      locationIds: this.locationIds(),
      parts: this.rows().filter((r) => r.itemId).map((r) => ({ itemId: r.itemId, variantId: r.variantId, quantity: r.quantity })),
    };
    const id = this.editingId();
    this.saving$.set(true);
    (id ? this.api.updateCombo(id, body) : this.api.createCombo(body)).subscribe({
      next: () => {
        this.saving$.set(false);
        this.editing.set(false);
        this.toast.success(this.lang.t('common.saved'));
        this.reload();
      },
      error: () => this.saving$.set(false),
    });
  }

  remove(c: Combo): void {
    if (!confirm(this.lang.t('combos.confirmDelete', { name: c.name }))) return;
    this.api.deleteCombo(c.id).subscribe({
      next: (gone) => {
        this.toast.success(this.lang.t(gone ? 'combos.deleted' : 'combos.switchedOff'));
        this.reload();
      },
      error: () => undefined,
    });
  }

  setMonth(v: string): void {
    this.month.set(v);
    this.loadReport();
  }

  private reload(): void {
    this.api.getCombos().subscribe({
      next: (c) => { this.combos.set(c); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  private loadReport(): void {
    if (!this.month()) return;
    this.report.set(null);
    this.api.getComboReport(this.month()).subscribe({ next: (r) => this.report.set(r), error: () => undefined });
  }
}
