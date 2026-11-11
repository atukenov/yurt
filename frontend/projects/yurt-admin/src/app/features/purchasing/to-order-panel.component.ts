import { Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BrandService, YurtApiService } from 'shared-api';
import { ToOrderGroup, ToOrderLine } from 'shared-models';
import { Currency2Pipe, LocalNumberPipe, ToastService } from 'shared-ui';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { orderText, packsFor, whatsappLink } from './purchasing-helpers';

interface Row extends ToOrderLine {
  amount: number | null;
  upTo: number | null;
}

interface Group extends ToOrderGroup {
  rows: Row[];
  busy: boolean;
}

/**
 * What a branch should order now: everything at or under its "warn at" number, grouped by the supplier that sells
 * it cheapest, with an amount that fills the shelf in whole packs. One press turns a group into an order (shown on
 * the Deliveries tab, where it becomes the invoice when the goods come) and opens WhatsApp with the order text.
 */
@Component({
  selector: 'app-to-order-panel',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe, LocalNumberPipe, Currency2Pipe],
  template: `
    <p class="mb-4 text-sm text-slate-500">{{ 'buy.toOrder.hint' | t }}</p>
    @if (loading()) {
      <p class="py-8 text-center text-slate-500">{{ 'label.loading' | t }}</p>
    } @else if (groups().length === 0) {
      <p class="rounded-2xl border border-slate-200 bg-surface p-8 text-center text-slate-500">{{ 'buy.toOrder.empty' | t }}</p>
    }
    @for (g of groups(); track g.supplierId ?? 'none') {
      <section class="mb-4 overflow-hidden rounded-2xl border border-slate-200 bg-surface">
        <header class="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 px-4 py-3">
          <div>
            <h2 class="font-semibold text-slate-800">{{ g.supplierName ?? ('buy.toOrder.noSupplier' | t) }}</h2>
            @if (g.supplierPhone) {
              <p class="text-xs text-slate-500">{{ g.supplierPhone }}</p>
            }
            @if (!g.supplierId) {
              <p class="text-xs text-slate-500">{{ 'buy.toOrder.noSupplierHint' | t }}</p>
            }
          </div>
          @if (g.supplierId) {
            <div class="flex flex-wrap items-center gap-2">
              @if (total(g) !== null) {
                <span class="text-sm text-slate-600">≈ {{ total(g)! | currency2 }}</span>
              }
              <button type="button" (click)="copy(g)" class="rounded-lg border border-slate-200 px-3 py-1.5 text-sm font-semibold text-slate-700 hover:bg-slate-50">
                {{ 'buy.copyText' | t }}
              </button>
              <button type="button" (click)="makeOrder(g)" [disabled]="g.busy || !hasAmounts(g)"
                class="rounded-lg bg-brand-500 px-3 py-1.5 text-sm font-semibold text-on-brand hover:bg-brand-600 disabled:opacity-40">
                {{ 'buy.toOrder.makeOrder' | t }}
              </button>
            </div>
          }
        </header>
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <tr>
                <th class="px-4 py-2">{{ 'stock.item' | t }}</th>
                <th class="px-4 py-2">{{ 'stock.left' | t }}</th>
                <th class="px-4 py-2">{{ 'buy.toOrder.upTo' | t }}</th>
                <th class="px-4 py-2">{{ 'buy.toOrder.amount' | t }}</th>
                <th class="px-4 py-2">{{ 'buy.toOrder.packs' | t }}</th>
              </tr>
            </thead>
            <tbody>
              @for (r of g.rows; track r.ingredientId) {
                <tr class="border-t border-slate-100">
                  <td class="px-4 py-2 font-medium text-slate-800">{{ r.name }}</td>
                  <td class="px-4 py-2 whitespace-nowrap">
                    <span [class]="r.left <= 0 ? 'font-semibold text-red-700' : 'text-amber-800'">{{ r.left | lnumber: '1.0-3' }} {{ r.unit }}</span>
                    <span class="block text-xs text-slate-500">{{ 'stock.warnAt' | t }} {{ r.lowStockAt | lnumber: '1.0-3' }}</span>
                    @if (r.alreadyOrdered > 0) {
                      <span class="block text-xs text-slate-500">{{ 'buy.toOrder.alreadyOrdered' | t: { amount: (r.alreadyOrdered | lnumber: '1.0-3') + ' ' + r.unit } }}</span>
                    }
                  </td>
                  <td class="px-4 py-2">
                    <input type="number" min="0" step="any" [(ngModel)]="r.upTo" (change)="saveUpTo(r)"
                      [placeholder]="(r.lowStockAt * 2 | lnumber: '1.0-3') ?? ''"
                      [attr.aria-label]="('buy.toOrder.upTo' | t) + ' ' + r.name"
                      class="w-24 rounded-lg border border-slate-200 px-2 py-1.5 outline-none focus:ring-2 focus:ring-brand-400" />
                  </td>
                  <td class="px-4 py-2 whitespace-nowrap">
                    <input type="number" min="0" step="any" [(ngModel)]="r.amount"
                      [attr.aria-label]="('buy.toOrder.amount' | t) + ' ' + r.name"
                      class="w-28 rounded-lg border border-slate-200 px-2 py-1.5 outline-none focus:ring-2 focus:ring-brand-400" />
                    {{ r.unit }}
                  </td>
                  <td class="px-4 py-2 text-slate-600">
                    @if (packsOf(r); as p) {
                      @if (p.packs !== null) {
                        {{ p.packs }} × {{ r.packName || (r.packSize | lnumber: '1.0-3') + ' ' + r.unit }}
                        @if (p.cost !== null) {
                          <span class="block text-xs text-slate-500">≈ {{ p.cost | currency2 }}</span>
                        }
                      } @else {
                        <span class="text-xs text-slate-400">—</span>
                      }
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
    }
  `,
})
export class ToOrderPanelComponent {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);
  private brand = inject(BrandService);

  locationId = input.required<string>();
  /** Id of the order just made, so the page can open it. */
  ordered = output<string>();

  groups = signal<Group[]>([]);
  loading = signal(true);

  constructor() {
    effect(() => this.load(this.locationId()));
  }

  load(locationId: string): void {
    if (!locationId) return;
    this.loading.set(true);
    this.api.getToOrder(locationId).subscribe({
      next: (groups) => {
        this.groups.set(groups.map((g) => ({
          ...g,
          busy: false,
          rows: g.lines.map((l) => ({ ...l, amount: l.suggested > 0 ? l.suggested : null, upTo: l.orderUpTo ?? null })),
        })));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  packsOf(r: Row): { packs: number | null; cost: number | null } {
    return packsFor(r, Number(r.amount) || 0);
  }

  total(g: Group): number | null {
    const costs = g.rows.filter((r) => (Number(r.amount) || 0) > 0).map((r) => this.packsOf(r).cost);
    return costs.some((c) => c !== null) ? costs.reduce<number>((s, c) => s + (c ?? 0), 0) : null;
  }

  hasAmounts(g: Group): boolean {
    return g.rows.some((r) => (Number(r.amount) || 0) > 0);
  }

  saveUpTo(r: Row): void {
    const value = r.upTo === null || `${r.upTo}` === '' ? null : Math.max(0, Number(r.upTo));
    this.api.setOrderUpTo(r.ingredientId, this.locationId(), value).subscribe({
      next: () => this.load(this.locationId()),
      error: () => this.toast.error(this.lang.t('common.saveFailed')),
    });
  }

  private text(g: Group): string {
    const lines = g.rows
      .filter((r) => (Number(r.amount) || 0) > 0)
      .map((r) => ({ name: r.name, unit: r.unit, quantity: Number(r.amount), packs: this.packsOf(r).packs, packName: r.packName }));
    return orderText(this.lang.t('buy.msg.hello'), lines, this.brand.name());
  }

  copy(g: Group): void {
    navigator.clipboard?.writeText(this.text(g)).then(
      () => this.toast.success(this.lang.t('buy.copied')),
      () => this.toast.error(this.lang.t('buy.copyFailed')),
    );
  }

  /** Saves the group as an order to the supplier, then opens WhatsApp with the text and the order on the Deliveries tab. */
  makeOrder(g: Group): void {
    if (!g.supplierId) return;
    g.busy = true;
    const lines = g.rows
      .filter((r) => (Number(r.amount) || 0) > 0)
      .map((r) => ({ ingredientId: r.ingredientId, quantity: Number(r.amount), total: this.packsOf(r).cost }));
    const text = this.text(g);
    // Opened before the request finishes, so the browser does not block it as a pop-up.
    window.open(whatsappLink(g.supplierPhone, text), '_blank', 'noopener');
    this.api
      .createPurchase({ supplierId: g.supplierId, locationId: this.locationId(), status: 'ordered', number: null, date: null, note: null, photoUrl: null, lines })
      .subscribe({
        next: (p) => {
          g.busy = false;
          this.toast.success(this.lang.t('buy.toOrder.done'));
          this.ordered.emit(p.id);
        },
        error: () => {
          g.busy = false;
          this.toast.error(this.lang.t('common.saveFailed'));
        },
      });
  }
}
