import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { Currency2Pipe, LocalNumberPipe, ToastService } from 'shared-ui';
import {
  CafeExpense,
  CafeExpenseCategory,
  ItemProfit,
  ItemProfitReport,
  Location,
  ProfitMonth,
} from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';
import { currentMonth } from '../payroll/payroll.component';

export const EXPENSE_CATEGORIES: CafeExpenseCategory[] = ['rent', 'utilities', 'marketing', 'repairs', 'taxes', 'other'];

export type ItemSort = 'profit' | 'perItem' | 'foodCost' | 'sold';

/** Sorts the dishes: unknown values (no recipe) always go last. */
export function sortItems(items: ItemProfit[], by: ItemSort): ItemProfit[] {
  const key = (i: ItemProfit): number | null =>
    (by === 'profit' ? i.profit : by === 'perItem' ? i.profitPerItem : by === 'foodCost' ? i.foodCostPercent : i.sold) ?? null;
  return [...items].sort((a, b) => {
    const x = key(a);
    const y = key(b);
    if (x == null && y == null) return b.revenue - a.revenue;
    if (x == null) return 1;
    if (y == null) return -1;
    // Food cost: lowest first (cheapest to make for its price); everything else: highest first.
    return by === 'foodCost' ? x - y : y - x;
  });
}

/** Change against last month in percent, or null when last month was 0. */
export function changePercent(now: number, before: number): number | null {
  if (!before) return null;
  return Math.round(((now - before) / Math.abs(before)) * 1000) / 10;
}

/**
 * Profit (CEO only): the month's profit after ingredients, stock lost, wages and other costs, and which dishes earn the
 * most after their ingredients.
 */
@Component({
  selector: 'app-profit',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe, LocalNumberPipe, Currency2Pipe],
  template: `
    <div class="p-4 md:p-6 max-w-6xl mx-auto space-y-5">
      <div>
        <h1 class="text-2xl font-bold text-slate-800">{{ 'profit.title' | t }}</h1>
        <p class="text-slate-500 text-sm mt-0.5">{{ 'profit.hint' | t }}</p>
      </div>

      <div class="flex flex-wrap items-center gap-3">
        <label class="text-sm font-semibold text-slate-700" for="profit-month">{{ 'payroll.month' | t }}</label>
        <input id="profit-month" type="month" [ngModel]="month()" (ngModelChange)="setMonth($event)"
          class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm" />
        @if (locations().length > 1) {
          <select [ngModel]="locationId()" (ngModelChange)="setLocation($event)" [attr.aria-label]="'profit.branch' | t"
            class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm">
            <option value="">{{ 'profit.allBranches' | t }}</option>
            @for (l of locations(); track l.id) {
              <option [value]="l.id">{{ l.name }}</option>
            }
          </select>
        }
        <button type="button" (click)="download()"
          class="ml-auto px-4 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-700 text-sm font-semibold">
          {{ 'profit.download' | t }}
        </button>
      </div>

      <div class="flex gap-2 border-b border-slate-200" role="tablist">
        @for (t of tabs; track t) {
          <button type="button" role="tab" [attr.aria-selected]="tab() === t" (click)="tab.set(t)"
            [class]="'px-4 py-2 text-sm font-semibold -mb-px border-b-2 ' +
              (tab() === t ? 'border-brand-500 text-slate-800' : 'border-transparent text-slate-500 hover:text-slate-700')">
            {{ 'profit.tab.' + t | t }}
          </button>
        }
      </div>

      @if (tab() === 'month') {
        @if (!report()) {
          <div class="h-48 bg-slate-100 rounded-2xl animate-pulse"></div>
        } @else {
          @let r = report()!;
          <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5">
            <div class="grid gap-3 sm:grid-cols-3 mb-5">
              <div class="rounded-xl bg-slate-50 p-3">
                <p class="text-xs text-slate-500">{{ 'profit.netSales' | t }}</p>
                <p class="text-xl font-bold text-slate-800">{{ r.netSales | currency2 }}</p>
              </div>
              <div class="rounded-xl bg-slate-50 p-3">
                <p class="text-xs text-slate-500">{{ 'profit.costs' | t }}</p>
                <p class="text-xl font-bold text-slate-800">{{ r.netSales - r.profit | currency2 }}</p>
              </div>
              <div class="rounded-xl p-3" [class]="r.profit < 0 ? 'bg-red-50' : 'bg-green-50'">
                <p class="text-xs text-slate-500">{{ 'profit.profit' | t }}</p>
                <p class="text-xl font-bold" [class]="r.profit < 0 ? 'text-tone-red' : 'text-green-700'">{{ r.profit | currency2 }}</p>
                @if (change(r.profit, r.previous.profit); as c) {
                  <p class="text-xs text-slate-500">{{ c > 0 ? '+' : '' }}{{ c | lnumber: '1.0-1' }}% {{ 'profit.vsLastMonth' | t }}</p>
                }
              </div>
            </div>

            <div class="overflow-x-auto">
              <table class="w-full text-sm">
                <thead>
                  <tr class="border-b border-slate-100 text-slate-500 text-xs uppercase tracking-wide">
                    <th class="text-left px-3 py-2"></th>
                    <th class="text-right px-3 py-2">{{ r.month }}</th>
                    <th class="text-right px-3 py-2">{{ 'profit.lastMonth' | t }}</th>
                  </tr>
                </thead>
                <tbody>
                  <tr class="border-b border-slate-50">
                    <td class="px-3 py-2">{{ 'profit.sales' | t }} <span class="text-xs text-slate-500">· {{ 'profit.orders' | t: { count: r.orders } }}</span></td>
                    <td class="px-3 py-2 text-right">{{ r.sales | currency2 }}</td><td></td>
                  </tr>
                  @if (r.discounts) {
                    <tr class="border-b border-slate-50">
                      <td class="px-3 py-2 pl-6 text-slate-600">{{ 'profit.discounts' | t }}</td>
                      <td class="px-3 py-2 text-right text-slate-600">−{{ r.discounts | currency2 }}</td><td></td>
                    </tr>
                  }
                  @if (r.refunds) {
                    <tr class="border-b border-slate-50">
                      <td class="px-3 py-2 pl-6 text-slate-600">{{ 'profit.refunds' | t }}</td>
                      <td class="px-3 py-2 text-right text-slate-600">−{{ r.refunds | currency2 }}</td><td></td>
                    </tr>
                  }
                  <tr class="border-b border-slate-100 font-semibold">
                    <td class="px-3 py-2">{{ 'profit.netSales' | t }}</td>
                    <td class="px-3 py-2 text-right">{{ r.netSales | currency2 }}</td>
                    <td class="px-3 py-2 text-right text-slate-500">{{ r.previous.netSales | currency2 }}</td>
                  </tr>
                  <tr class="border-b border-slate-50">
                    <td class="px-3 py-2">{{ 'profit.ingredients' | t }}</td>
                    <td class="px-3 py-2 text-right">−{{ r.ingredientCost | currency2 }}</td>
                    <td class="px-3 py-2 text-right text-slate-500">−{{ r.previous.ingredientCost | currency2 }}</td>
                  </tr>
                  <tr class="border-b border-slate-100 font-semibold">
                    <td class="px-3 py-2">{{ 'profit.gross' | t }}</td>
                    <td class="px-3 py-2 text-right">{{ r.grossProfit | currency2 }}</td>
                    <td class="px-3 py-2 text-right text-slate-500">{{ r.previous.grossProfit | currency2 }}</td>
                  </tr>
                  @for (w of r.writeOffsByReason; track w.reason) {
                    <tr class="border-b border-slate-50">
                      <td class="px-3 py-2">{{ 'profit.writeOff' | t }}: {{ 'moves.reason.' + w.reason | t }}</td>
                      <td class="px-3 py-2 text-right">−{{ w.cost | currency2 }}</td><td></td>
                    </tr>
                  }
                  @if (r.stocktakeLoss) {
                    <tr class="border-b border-slate-50">
                      <td class="px-3 py-2">{{ (r.stocktakeLoss > 0 ? 'profit.stocktakeLoss' : 'profit.stocktakeGain') | t }}</td>
                      <td class="px-3 py-2 text-right">{{ r.stocktakeLoss > 0 ? '−' : '+' }}{{ abs(r.stocktakeLoss) | currency2 }}</td><td></td>
                    </tr>
                  }
                  @if (r.payroll != null) {
                    <tr class="border-b border-slate-50">
                      <td class="px-3 py-2">{{ 'profit.wages' | t }}
                        @if (!r.payrollClosed) { <span class="text-xs text-slate-500">· {{ 'profit.wagesOpen' | t }}</span> }
                      </td>
                      <td class="px-3 py-2 text-right">−{{ r.payroll | currency2 }}</td><td></td>
                    </tr>
                  }
                  @for (e of r.expenses; track e.id) {
                    <tr class="border-b border-slate-50">
                      <td class="px-3 py-2">{{ e.name }}
                        <span class="text-xs text-slate-500">· {{ 'profit.cat.' + e.category | t }}@if (e.locationName) { · {{ e.locationName }} }</span>
                      </td>
                      <td class="px-3 py-2 text-right">−{{ e.amount | currency2 }}</td><td></td>
                    </tr>
                  }
                </tbody>
                <tfoot>
                  <tr class="font-bold text-slate-800">
                    <td class="px-3 py-2">{{ 'profit.profit' | t }}</td>
                    <td class="px-3 py-2 text-right" [class.text-tone-red]="r.profit < 0">{{ r.profit | currency2 }}</td>
                    <td class="px-3 py-2 text-right text-slate-500">{{ r.previous.profit | currency2 }}</td>
                  </tr>
                </tfoot>
              </table>
            </div>

            <div class="mt-4 space-y-1 text-xs text-slate-500">
              @if (r.estimatedLines) { <p>{{ 'profit.estimatedNote' | t: { count: r.estimatedLines } }}</p> }
              @if (r.linesWithoutRecipe) { <p>{{ 'profit.noRecipeNote' | t: { count: r.linesWithoutRecipe } }}</p> }
              @if (locationId()) { <p>{{ 'profit.branchNote' | t }}</p> }
            </div>
          </section>

          <!-- Other costs of the month -->
          <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5">
            <h2 class="font-semibold text-slate-800">{{ 'profit.expenses' | t }}</h2>
            <p class="text-xs text-slate-500 mb-3">{{ 'profit.expensesHint' | t }}</p>

            @for (e of expenses(); track e.id) {
              <div class="flex flex-wrap items-center gap-2 py-1.5 border-b border-slate-50 text-sm">
                <span class="font-medium text-slate-800">{{ e.name }}</span>
                <span class="text-xs text-slate-500">{{ 'profit.cat.' + e.category | t }} · {{ e.locationName ?? ('profit.wholeCafe' | t) }}</span>
                <span class="ml-auto font-semibold">{{ e.amount | currency2 }}</span>
                <button type="button" (click)="edit(e)" class="text-xs underline text-slate-500 hover:text-slate-700">{{ 'action.edit' | t }}</button>
                <button type="button" (click)="remove(e)" class="text-xs underline text-slate-500 hover:text-slate-700">{{ 'action.delete' | t }}</button>
              </div>
            } @empty {
              <div class="flex flex-wrap items-center gap-3 py-2">
                <p class="text-sm text-slate-500">{{ 'profit.noExpenses' | t }}</p>
                <button type="button" (click)="copyPrevious()" [disabled]="busy()"
                  class="px-3 py-1.5 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold disabled:opacity-50">
                  {{ 'profit.copyPrevious' | t }}
                </button>
              </div>
            }

            <form class="grid gap-3 md:grid-cols-6 mt-4" (ngSubmit)="save()">
              <select name="category" [(ngModel)]="formCategory" [attr.aria-label]="'profit.category' | t"
                class="px-3 py-2 rounded-xl border border-slate-200 text-sm">
                @for (c of categories; track c) {
                  <option [value]="c">{{ 'profit.cat.' + c | t }}</option>
                }
              </select>
              <input name="name" type="text" maxlength="100" [(ngModel)]="formName" [attr.aria-label]="'profit.costName' | t"
                [placeholder]="'profit.costNameHint' | t" class="px-3 py-2 rounded-xl border border-slate-200 text-sm md:col-span-2" />
              <input name="amount" type="number" min="0" step="any" [(ngModel)]="formAmount" [attr.aria-label]="'payroll.amount' | t"
                [placeholder]="'payroll.amount' | t" class="px-3 py-2 rounded-xl border border-slate-200 text-sm" />
              <select name="location" [(ngModel)]="formLocation" [attr.aria-label]="'profit.branch' | t"
                class="px-3 py-2 rounded-xl border border-slate-200 text-sm">
                <option value="">{{ 'profit.wholeCafe' | t }}</option>
                @for (l of locations(); track l.id) {
                  <option [value]="l.id">{{ l.name }}</option>
                }
              </select>
              <div class="flex gap-2">
                <button type="submit" [disabled]="busy() || !formName.trim() || !formAmount"
                  class="flex-1 px-4 py-2 rounded-xl bg-brand-500 hover:bg-brand-600 text-on-brand text-sm font-semibold disabled:opacity-50">
                  {{ (editingId ? 'action.save' : 'action.add') | t }}
                </button>
                @if (editingId) {
                  <button type="button" (click)="resetForm()" class="px-3 py-2 rounded-xl bg-slate-100 text-slate-700 text-sm">
                    {{ 'action.cancel' | t }}
                  </button>
                }
              </div>
            </form>
          </section>
        }
      }

      @if (tab() === 'items') {
        <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5">
          <div class="flex flex-wrap items-center gap-3 mb-3">
            <label class="text-sm text-slate-600" for="profit-sort">{{ 'profit.sortBy' | t }}</label>
            <select id="profit-sort" [ngModel]="sort()" (ngModelChange)="sort.set($event)"
              class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm">
              <option value="profit">{{ 'profit.sort.profit' | t }}</option>
              <option value="perItem">{{ 'profit.sort.perItem' | t }}</option>
              <option value="foodCost">{{ 'profit.sort.foodCost' | t }}</option>
              <option value="sold">{{ 'profit.sort.sold' | t }}</option>
            </select>
          </div>
          @if (!items()) {
            <div class="h-48 bg-slate-100 rounded-2xl animate-pulse"></div>
          } @else if (!sortedItems().length) {
            <p class="text-slate-500 text-sm py-6 text-center">{{ 'profit.noSales' | t }}</p>
          } @else {
            <div class="overflow-x-auto">
              <table class="w-full text-sm">
                <thead>
                  <tr class="border-b border-slate-100 text-slate-500 text-xs uppercase tracking-wide">
                    <th class="text-left px-3 py-2">{{ 'profit.dish' | t }}</th>
                    <th class="text-right px-3 py-2">{{ 'profit.sold' | t }}</th>
                    <th class="text-right px-3 py-2">{{ 'profit.revenue' | t }}</th>
                    <th class="text-right px-3 py-2">{{ 'profit.ingredients' | t }}</th>
                    <th class="text-right px-3 py-2">{{ 'profit.profit' | t }}</th>
                    <th class="text-right px-3 py-2">{{ 'profit.perItem' | t }}</th>
                    <th class="text-right px-3 py-2">{{ 'profit.foodCost' | t }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (i of sortedItems(); track i.menuItemId + (i.variantId ?? '')) {
                    <tr class="border-b border-slate-50 align-top">
                      <td class="px-3 py-2">
                        <p class="font-medium text-slate-800">{{ i.name }}@if (i.size) { <span class="text-slate-500"> · {{ i.size }}</span> }</p>
                        @if (i.cost == null) { <p class="text-xs text-tone-red">{{ 'profit.noRecipe' | t }}</p> }
                        @if (i.missingPrice) { <p class="text-xs text-amber-700">{{ 'profit.missingPrice' | t }}</p> }
                        @if (i.estimatedCount) { <p class="text-xs text-slate-500">{{ 'profit.estimatedItem' | t: { count: i.estimatedCount } }}</p> }
                      </td>
                      <td class="px-3 py-2 text-right">{{ i.sold | lnumber }}</td>
                      <td class="px-3 py-2 text-right">{{ i.revenue | currency2 }}</td>
                      <td class="px-3 py-2 text-right">{{ i.cost == null ? '—' : (i.cost | currency2) }}</td>
                      <td class="px-3 py-2 text-right font-semibold" [class.text-tone-red]="(i.profit ?? 0) < 0">
                        {{ i.profit == null ? '—' : (i.profit | currency2) }}
                      </td>
                      <td class="px-3 py-2 text-right">{{ i.profitPerItem == null ? '—' : (i.profitPerItem | currency2) }}</td>
                      <td class="px-3 py-2 text-right" [class.text-tone-red]="(i.foodCostPercent ?? 0) > 35">
                        {{ i.foodCostPercent == null ? '—' : (i.foodCostPercent | lnumber: '1.0-1') + '%' }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
            <p class="mt-3 text-xs text-slate-500">{{ 'profit.itemsNote' | t }}</p>
          }
        </section>
      }
    </div>
  `,
})
export class ProfitComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);

  readonly tabs = ['month', 'items'] as const;
  readonly categories = EXPENSE_CATEGORIES;

  tab = signal<'month' | 'items'>('month');
  month = signal(currentMonth());
  locationId = signal('');
  locations = signal<Location[]>([]);
  report = signal<ProfitMonth | null>(null);
  expenses = signal<CafeExpense[]>([]);
  items = signal<ItemProfitReport | null>(null);
  sort = signal<ItemSort>('profit');
  busy = signal(false);
  sortedItems = computed(() => sortItems(this.items()?.items ?? [], this.sort()));

  editingId: string | null = null;
  formCategory: CafeExpenseCategory = 'rent';
  formName = '';
  formAmount: number | null = null;
  formLocation = '';

  readonly change = changePercent;
  readonly abs = Math.abs;

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe({ next: (l) => this.locations.set(l) });
    this.load();
  }

  setMonth(value: string): void {
    if (!/^\d{4}-\d{2}$/.test(value)) return;
    this.month.set(value);
    this.load();
  }

  setLocation(value: string): void {
    this.locationId.set(value);
    this.load();
  }

  private load(): void {
    const loc = this.locationId() || null;
    this.report.set(null);
    this.items.set(null);
    this.api.getProfitMonth(this.month(), loc).subscribe({ next: (r) => this.report.set(r) });
    this.api.getProfitItems(this.month(), loc).subscribe({ next: (r) => this.items.set(r) });
    this.api.getCafeExpenses(this.month()).subscribe({ next: (e) => this.expenses.set(e) });
  }

  download(): void {
    const kind = this.tab() === 'month' ? 'month' : 'items';
    const lang = this.lang.lang();
    this.api.exportProfit(kind, this.month(), this.locationId() || null, lang).subscribe((blob) => {
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = `${kind === 'month' ? 'profit' : 'profit-by-dish'}-${this.month()}.xlsx`;
      a.click();
      setTimeout(() => URL.revokeObjectURL(a.href), 1000);
    });
  }

  edit(e: CafeExpense): void {
    this.editingId = e.id;
    this.formCategory = e.category;
    this.formName = e.name;
    this.formAmount = e.amount;
    this.formLocation = e.locationId ?? '';
  }

  resetForm(): void {
    this.editingId = null;
    this.formName = '';
    this.formAmount = null;
    this.formLocation = '';
  }

  save(): void {
    if (!this.formName.trim() || !this.formAmount) return;
    const data = {
      month: this.month(),
      locationId: this.formLocation || null,
      category: this.formCategory,
      name: this.formName.trim(),
      amount: Number(this.formAmount),
    };
    this.busy.set(true);
    const call = this.editingId ? this.api.updateCafeExpense(this.editingId, data) : this.api.addCafeExpense(data);
    call.subscribe({
      next: () => {
        this.busy.set(false);
        this.resetForm();
        this.toast.success(this.lang.t('profit.savedToast'));
        this.load();
      },
      error: () => this.busy.set(false),
    });
  }

  remove(e: CafeExpense): void {
    this.api.deleteCafeExpense(e.id).subscribe({
      next: () => {
        if (this.editingId === e.id) this.resetForm();
        this.load();
      },
    });
  }

  copyPrevious(): void {
    this.busy.set(true);
    this.api.copyPreviousCafeExpenses(this.month()).subscribe({
      next: () => {
        this.busy.set(false);
        this.toast.success(this.lang.t('profit.copiedToast'));
        this.load();
      },
      error: () => this.busy.set(false),
    });
  }
}
