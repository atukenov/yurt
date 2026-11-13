import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { ToastService } from 'shared-ui';
import { Location, MenuCategory, MenuItem, MenuSchedule, SaveMenuSchedule, TimePrice } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';
import { ConfirmService } from '../../shared/confirm-dialog/confirm.service';
import { MenuSectionsComponent } from '../../shared/menu-sections/menu-sections.component';

/** One price row in the editor. `target` is `c:<categoryId>` or `i:<dishId>`. */
interface PriceRow {
  target: string;
  mode: 'price' | 'percent';
  value: number | null;
}

interface Form {
  id: string | null;
  name: string;
  days: number;
  start: string;
  end: string;
  locationId: string;
  isActive: boolean;
  categoryIds: string[];
  itemIds: string[];
  prices: PriceRow[];
}

const ALL_DAYS = 127;
const DAYS = [1, 2, 3, 4, 5, 6, 7];

/** Bit of a weekday number (1 = Monday ... 7 = Sunday) in the `days` mask. */
export const dayBit = (day: number): number => 1 << (day - 1);

/** Server time text: the end of the day is "24:00", which a time field cannot show, so it is shown as 00:00. */
export const endForField = (end: string): string => (end === '24:00' ? '00:00' : end);
export const endForServer = (end: string, start: string): string => (end === '00:00' && start !== '00:00' ? '24:00' : end);

/**
 * Time menus: windows of the week such as "Breakfast 07:00-11:00" or "Happy hour Mon-Fri 16:00-18:00". Dishes and
 * categories ticked for a window are on the menu only inside it; prices set for a window apply only inside it. The
 * window follows each branch's own clock (set in Locations).
 */
@Component({
  selector: 'app-time-menus',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe, MenuSectionsComponent],
  template: `
    <div class="p-4 md:p-6 max-w-4xl mx-auto space-y-5">
      <app-menu-sections />
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold text-slate-800">{{ 'timeMenus.title' | t }}</h1>
          <p class="text-slate-500 text-sm mt-0.5">{{ 'timeMenus.hint' | t }}</p>
        </div>
        @if (!form()) {
          <button type="button" (click)="startNew()"
            class="inline-flex items-center gap-1.5 bg-brand-500 hover:bg-brand-600 text-on-brand font-semibold text-sm px-4 py-2 rounded-xl shadow-sm">
            <span class="text-base leading-none font-bold">+</span> {{ 'timeMenus.add' | t }}
          </button>
        }
      </div>

      @if (form(); as f) {
        <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5 space-y-4">
          <div class="grid gap-3 sm:grid-cols-2">
            <div class="sm:col-span-2">
              <label for="tm-name" class="block text-xs font-medium text-slate-600 mb-1">{{ 'timeMenus.name' | t }}</label>
              <input id="tm-name" type="text" maxlength="100" [ngModel]="f.name" (ngModelChange)="patch({ name: $event })"
                [placeholder]="'timeMenus.namePlaceholder' | t"
                class="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:ring-2 focus:ring-brand-400 outline-none" />
            </div>
            <div>
              <label for="tm-start" class="block text-xs font-medium text-slate-600 mb-1">{{ 'timeMenus.from' | t }}</label>
              <input id="tm-start" type="time" [ngModel]="f.start" (ngModelChange)="patch({ start: $event })"
                class="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm" />
            </div>
            <div>
              <label for="tm-end" class="block text-xs font-medium text-slate-600 mb-1">{{ 'timeMenus.to' | t }}</label>
              <input id="tm-end" type="time" [ngModel]="f.end" (ngModelChange)="patch({ end: $event })"
                class="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm" />
            </div>
          </div>
          <p class="text-xs text-slate-500 -mt-2">{{ 'timeMenus.endHint' | t }}</p>

          <div>
            <p class="block text-xs font-medium text-slate-600 mb-1">{{ 'timeMenus.days' | t }}</p>
            <div class="flex flex-wrap gap-1.5">
              @for (d of days; track d) {
                <button type="button" role="switch" [attr.aria-checked]="hasDay(f, d)" (click)="toggleDay(d)"
                  class="px-3 py-1.5 rounded-xl border text-sm font-semibold"
                  [class]="hasDay(f, d) ? 'bg-brand-500 text-on-brand border-brand-500' : 'bg-white text-slate-600 border-slate-200'">
                  {{ 'day.' + d | t }}
                </button>
              }
            </div>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            @if (locations().length > 1) {
              <div>
                <label for="tm-branch" class="block text-xs font-medium text-slate-600 mb-1">{{ 'timeMenus.branch' | t }}</label>
                <select id="tm-branch" [ngModel]="f.locationId" (ngModelChange)="patch({ locationId: $event })"
                  class="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm bg-white">
                  <option value="">{{ 'timeMenus.allBranches' | t }}</option>
                  @for (l of locations(); track l.id) {
                    <option [value]="l.id">{{ l.name }}</option>
                  }
                </select>
              </div>
            }
            <label class="flex items-center gap-2 text-sm text-slate-700 sm:self-end">
              <input type="checkbox" [ngModel]="f.isActive" (ngModelChange)="patch({ isActive: $event })" />
              {{ 'timeMenus.active' | t }}
            </label>
          </div>

          <div>
            <h2 class="text-sm font-semibold text-slate-700">{{ 'timeMenus.onlyTitle' | t }}</h2>
            <p class="text-xs text-slate-500 mb-2">{{ 'timeMenus.onlyHint' | t }}</p>
            <p class="text-xs font-medium text-slate-600 mb-1">{{ 'timeMenus.categories' | t }}</p>
            <div class="flex flex-wrap gap-1.5 mb-3">
              @for (c of categories(); track c.id) {
                <button type="button" role="switch" [attr.aria-checked]="f.categoryIds.includes(c.id)" (click)="toggleCategory(c.id)"
                  class="px-3 py-1 rounded-full border text-sm"
                  [class]="f.categoryIds.includes(c.id) ? 'bg-brand-500 text-on-brand border-brand-500' : 'bg-white text-slate-600 border-slate-200'">
                  {{ c.name }}
                </button>
              }
            </div>
            <p class="text-xs font-medium text-slate-600 mb-1">{{ 'timeMenus.dishes' | t }}</p>
            <input type="search" [ngModel]="dishFilter()" (ngModelChange)="dishFilter.set($event)" [placeholder]="'timeMenus.findDish' | t"
              [attr.aria-label]="'timeMenus.findDish' | t"
              class="w-full border border-slate-200 rounded-xl px-3 py-1.5 text-sm mb-2" />
            <div class="max-h-48 overflow-y-auto border border-slate-100 rounded-xl divide-y divide-slate-50">
              @for (i of shownDishes(); track i.id) {
                <label class="flex items-center gap-2 px-3 py-1.5 text-sm cursor-pointer hover:bg-slate-50">
                  <input type="checkbox" [checked]="f.itemIds.includes(i.id)" (change)="toggleDish(i.id)" />
                  <span class="flex-1 truncate">{{ i.name }}</span>
                  <span class="text-xs text-slate-400">{{ i.categoryName }}</span>
                </label>
              } @empty {
                <p class="px-3 py-2 text-sm text-slate-500">{{ 'timeMenus.noDishes' | t }}</p>
              }
            </div>
          </div>

          <div>
            <h2 class="text-sm font-semibold text-slate-700">{{ 'timeMenus.pricesTitle' | t }}</h2>
            <p class="text-xs text-slate-500 mb-2">{{ 'timeMenus.pricesHint' | t }}</p>
            <div class="space-y-2">
              @for (row of f.prices; track $index) {
                <div class="flex flex-wrap items-center gap-2">
                  <select [ngModel]="row.target" (ngModelChange)="patchPrice($index, { target: $event })"
                    [attr.aria-label]="'timeMenus.priceFor' | t"
                    class="flex-1 min-w-40 border border-slate-200 rounded-xl px-3 py-1.5 text-sm bg-white">
                    <option value="">{{ 'timeMenus.pickTarget' | t }}</option>
                    <optgroup [label]="'timeMenus.categories' | t">
                      @for (c of categories(); track c.id) { <option [value]="'c:' + c.id">{{ c.name }}</option> }
                    </optgroup>
                    <optgroup [label]="'timeMenus.dishes' | t">
                      @for (i of items(); track i.id) { <option [value]="'i:' + i.id">{{ i.name }}</option> }
                    </optgroup>
                  </select>
                  <select [ngModel]="row.mode" (ngModelChange)="patchPrice($index, { mode: $event })"
                    [attr.aria-label]="'timeMenus.priceKind' | t"
                    class="border border-slate-200 rounded-xl px-3 py-1.5 text-sm bg-white">
                    <option value="percent">{{ 'timeMenus.percentOff' | t }}</option>
                    <option value="price">{{ 'timeMenus.newPrice' | t }}</option>
                  </select>
                  <input type="number" min="0" step="any" [ngModel]="row.value" (ngModelChange)="patchPrice($index, { value: $event })"
                    [attr.aria-label]="'timeMenus.priceValue' | t"
                    class="w-28 border border-slate-200 rounded-xl px-3 py-1.5 text-sm" />
                  <button type="button" (click)="removePrice($index)" [attr.aria-label]="'action.delete' | t"
                    class="text-tone-red text-sm px-2 py-1 rounded hover:bg-red-50">✕</button>
                </div>
              }
            </div>
            <button type="button" (click)="addPrice()" class="mt-2 text-sm font-semibold text-brand-ink hover:underline">
              + {{ 'timeMenus.addPrice' | t }}
            </button>
            <p class="text-xs text-slate-500 mt-1">{{ 'timeMenus.sizesNote' | t }}</p>
          </div>

          <div class="flex gap-2 justify-end pt-1">
            <button type="button" (click)="cancel()" class="px-4 py-2 rounded-xl text-sm text-slate-600 hover:bg-slate-100">{{ 'action.cancel' | t }}</button>
            <button type="button" (click)="save()" [disabled]="saving()"
              class="px-4 py-2 rounded-xl text-sm font-semibold bg-brand-500 hover:bg-brand-600 text-on-brand disabled:opacity-50">
              {{ 'action.save' | t }}
            </button>
          </div>
        </section>
      }

      @if (loading()) {
        <div class="h-32 bg-slate-100 rounded-2xl animate-pulse"></div>
      } @else {
        <div class="space-y-3">
          @for (s of schedules(); track s.id) {
            <article class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 flex flex-wrap items-start gap-3">
              <div class="flex-1 min-w-48">
                <div class="flex items-center gap-2">
                  <h2 class="font-semibold text-slate-800">{{ s.name }}</h2>
                  @if (!s.isActive) {
                    <span class="text-xs px-2 py-0.5 rounded-full bg-slate-100 text-slate-500">{{ 'timeMenus.off' | t }}</span>
                  } @else if (s.runningNow) {
                    <span class="text-xs px-2 py-0.5 rounded-full bg-green-100 text-green-700">{{ 'timeMenus.runningNow' | t }}</span>
                  }
                </div>
                <p class="text-sm text-slate-600">{{ daysText(s) }} · {{ s.start }}–{{ s.end }} · {{ branchName(s) }}</p>
                <p class="text-xs text-slate-500 mt-1">
                  {{ 'timeMenus.summary' | t: { dishes: s.itemIds.length, categories: s.categoryIds.length, prices: s.prices.length } }}
                </p>
              </div>
              <div class="flex gap-1">
                <button type="button" (click)="edit(s)" class="text-xs text-slate-600 hover:text-brand-ink px-2 py-1 rounded hover:bg-brand-50">{{ 'action.edit' | t }}</button>
                <button type="button" (click)="remove(s)" class="text-xs text-tone-red hover:text-red-700 px-2 py-1 rounded hover:bg-red-50">{{ 'action.delete' | t }}</button>
              </div>
            </article>
          } @empty {
            @if (!form()) {
              <p class="text-sm text-slate-500">{{ 'timeMenus.empty' | t }}</p>
            }
          }
        </div>
      }
    </div>
  `,
})
export class TimeMenusComponent implements OnInit {
  private readonly api = inject(YurtApiService);
  private readonly toast = inject(ToastService);
  private readonly lang = inject(AdminLangService);
  private readonly confirmSvc = inject(ConfirmService);

  readonly days = DAYS;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly schedules = signal<MenuSchedule[]>([]);
  readonly locations = signal<Location[]>([]);
  readonly categories = signal<MenuCategory[]>([]);
  readonly items = signal<MenuItem[]>([]);
  readonly form = signal<Form | null>(null);
  readonly dishFilter = signal('');

  readonly shownDishes = computed(() => {
    const q = this.dishFilter().trim().toLowerCase();
    return this.items().filter((i) => !q || i.name.toLowerCase().includes(q));
  });

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe({ next: (l) => this.locations.set(l), error: () => undefined });
    this.api.adminGetCategories().subscribe({ next: (c) => this.categories.set(c), error: () => undefined });
    this.api.adminGetMenuItems().subscribe({ next: (i) => this.items.set(i), error: () => undefined });
    this.reload();
  }

  private reload(): void {
    this.api.adminGetSchedules().subscribe({
      next: (s) => { this.schedules.set(s); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  hasDay(f: Form, d: number): boolean { return (f.days & dayBit(d)) !== 0; }

  daysText(s: MenuSchedule): string {
    if (s.days === ALL_DAYS) return this.lang.t('timeMenus.everyDay');
    return DAYS.filter((d) => (s.days & dayBit(d)) !== 0).map((d) => this.lang.t('day.' + d)).join(', ');
  }

  branchName(s: MenuSchedule): string {
    return this.locations().find((l) => l.id === s.locationId)?.name ?? this.lang.t('timeMenus.allBranches');
  }

  startNew(): void {
    this.dishFilter.set('');
    this.form.set({
      id: null, name: '', days: ALL_DAYS, start: '07:00', end: '11:00', locationId: '', isActive: true,
      categoryIds: [], itemIds: [], prices: [],
    });
  }

  edit(s: MenuSchedule): void {
    this.dishFilter.set('');
    this.form.set({
      id: s.id, name: s.name, days: s.days, start: s.start, end: endForField(s.end), locationId: s.locationId ?? '',
      isActive: s.isActive, categoryIds: [...s.categoryIds], itemIds: [...s.itemIds],
      prices: s.prices.map((p) => ({
        target: p.menuItemId ? 'i:' + p.menuItemId : 'c:' + p.categoryId,
        mode: p.price != null ? 'price' : 'percent',
        value: p.price ?? p.percentOff ?? null,
      })),
    });
  }

  cancel(): void { this.form.set(null); }

  patch(change: Partial<Form>): void { this.form.update((f) => (f ? { ...f, ...change } : f)); }

  toggleDay(d: number): void { this.form.update((f) => (f ? { ...f, days: f.days ^ dayBit(d) } : f)); }

  toggleCategory(id: string): void {
    this.form.update((f) => f && { ...f, categoryIds: f.categoryIds.includes(id) ? f.categoryIds.filter((x) => x !== id) : [...f.categoryIds, id] });
  }

  toggleDish(id: string): void {
    this.form.update((f) => f && { ...f, itemIds: f.itemIds.includes(id) ? f.itemIds.filter((x) => x !== id) : [...f.itemIds, id] });
  }

  addPrice(): void { this.form.update((f) => f && { ...f, prices: [...f.prices, { target: '', mode: 'percent', value: null }] }); }

  patchPrice(index: number, change: Partial<PriceRow>): void {
    this.form.update((f) => f && { ...f, prices: f.prices.map((p, i) => (i === index ? { ...p, ...change } : p)) });
  }

  removePrice(index: number): void { this.form.update((f) => f && { ...f, prices: f.prices.filter((_, i) => i !== index) }); }

  save(): void {
    const f = this.form();
    if (!f) return;
    if (!f.name.trim()) { this.toast.error(this.lang.t('timeMenus.needName')); return; }
    if (f.days === 0) { this.toast.error(this.lang.t('timeMenus.needDay')); return; }
    if (!f.start || !f.end) { this.toast.error(this.lang.t('timeMenus.needTimes')); return; }
    const prices: TimePrice[] = [];
    for (const row of f.prices) {
      if (!row.target || row.value == null || row.value < 0 || (row.mode === 'percent' && (row.value <= 0 || row.value > 100))) {
        this.toast.error(this.lang.t('timeMenus.badPrice'));
        return;
      }
      const isDish = row.target.startsWith('i:');
      const id = row.target.slice(2);
      prices.push({
        menuItemId: isDish ? id : null,
        categoryId: isDish ? null : id,
        price: row.mode === 'price' ? row.value : null,
        percentOff: row.mode === 'percent' ? row.value : null,
      });
    }
    const body: SaveMenuSchedule = {
      name: f.name.trim(), days: f.days, start: f.start, end: endForServer(f.end, f.start), locationId: f.locationId || null,
      isActive: f.isActive, categoryIds: f.categoryIds, itemIds: f.itemIds, prices,
    };
    this.saving.set(true);
    this.api.adminSaveSchedule(f.id, body).subscribe({
      next: () => {
        this.saving.set(false);
        this.form.set(null);
        this.toast.success(this.lang.t('timeMenus.saved'));
        this.reload();
      },
      error: () => this.saving.set(false),
    });
  }

  async remove(s: MenuSchedule): Promise<void> {
    if (!await this.confirmSvc.confirm(this.lang.t('timeMenus.deleteTitle'), this.lang.t('timeMenus.deleteText', { name: s.name }))) return;
    this.api.adminDeleteSchedule(s.id).subscribe({
      next: () => { this.toast.success(this.lang.t('timeMenus.deleted')); this.reload(); },
      error: () => undefined,
    });
  }
}
