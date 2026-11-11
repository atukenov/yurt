import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthStateService, YurtApiService } from 'shared-api';
import { Location } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { DeliveriesPanelComponent } from './deliveries-panel.component';
import { SuppliersPanelComponent } from './suppliers-panel.component';
import { ToOrderPanelComponent } from './to-order-panel.component';

export type PurchasingTab = 'toOrder' | 'deliveries' | 'suppliers';

/**
 * Suppliers, orders and delivery invoices (own_pos). Staff see what to order and type in deliveries; cafe admins
 * also manage suppliers, see what is owed, mark invoices paid and cancel posted ones.
 */
@Component({
  selector: 'app-purchasing',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe, ToOrderPanelComponent, DeliveriesPanelComponent, SuppliersPanelComponent],
  template: `
    <div class="mx-auto max-w-5xl p-6">
      <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-xl font-bold text-slate-800">{{ 'buy.title' | t }}</h1>
          <p class="text-sm text-slate-500">{{ 'buy.hint' | t }}</p>
        </div>
        @if (tab() !== 'suppliers') {
          <select
            [(ngModel)]="locationId"
            (ngModelChange)="pickLocation()"
            [attr.aria-label]="'stock.branch' | t"
            class="rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-brand-400"
          >
            @for (loc of locations(); track loc.id) {
              <option [value]="loc.id">{{ loc.name }}</option>
            }
          </select>
        }
      </div>

      <div class="mb-4 flex gap-1 overflow-x-auto border-b border-slate-200" role="tablist">
        @for (t of tabs; track t) {
          <button
            type="button"
            role="tab"
            [attr.aria-selected]="tab() === t"
            (click)="setTab(t)"
            class="-mb-px whitespace-nowrap border-b-2 px-4 py-2 text-sm font-semibold"
            [class]="tab() === t ? 'border-brand-500 text-slate-800' : 'border-transparent text-slate-500 hover:text-slate-700'"
          >
            {{ 'buy.tab.' + t | t }}
          </button>
        }
      </div>

      @if (locationId || tab() === 'suppliers') {
        @switch (tab()) {
          @case ('toOrder') {
            <app-to-order-panel [locationId]="locationId" (ordered)="openDelivery($event)" />
          }
          @case ('deliveries') {
            <app-deliveries-panel [locationId]="locationId" [isAdmin]="isAdmin" [openId]="openId()" />
          }
          @case ('suppliers') {
            <app-suppliers-panel />
          }
        }
      }
    </div>
  `,
})
export class PurchasingComponent implements OnInit {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);

  private readonly LOC_KEY = 'yurt_stock_location';
  readonly isAdmin = this.auth.isAdmin;
  readonly tabs: PurchasingTab[] = this.isAdmin ? ['toOrder', 'deliveries', 'suppliers'] : ['toOrder', 'deliveries'];

  locations = signal<Location[]>([]);
  tab = signal<PurchasingTab>('toOrder');
  /** An order just made from the "what to order" list, opened on the Deliveries tab. */
  openId = signal<string | null>(null);
  locationId = '';

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe((locs) => {
      this.locations.set(locs);
      const saved = localStorage.getItem(this.LOC_KEY) ?? '';
      this.locationId = locs.some((l) => l.id === saved) ? saved : (locs[0]?.id ?? '');
    });
  }

  pickLocation(): void {
    localStorage.setItem(this.LOC_KEY, this.locationId);
    this.openId.set(null);
  }

  setTab(t: PurchasingTab): void {
    this.openId.set(null);
    this.tab.set(t);
  }

  openDelivery(id: string): void {
    this.openId.set(id);
    this.tab.set('deliveries');
  }
}
