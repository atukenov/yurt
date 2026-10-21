import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { Location, StockAlerts, StockItem } from 'shared-models';
import { ToastService } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { IngredientsPanelComponent } from './ingredients-panel.component';
import { RecipesPanelComponent } from './recipes-panel.component';
import { StockAlertPhonesComponent } from './stock-alert-phones.component';

interface StockRow extends StockItem {
  /** What the staff typed, kept apart from the saved value until they press Save. */
  qtyInput: number | null;
  lowInput: number | null;
  saving: boolean;
}

/**
 * Stock counts per branch (Phase 6, `own_pos`). A counted item drops off the customer menu at zero by itself;
 * clearing the count makes the item unlimited again.
 */
@Component({
  selector: 'app-stock-management',
  standalone: true,
  imports: [
    FormsModule,
    AdminTranslatePipe,
    IngredientsPanelComponent,
    RecipesPanelComponent,
    StockAlertPhonesComponent,
  ],
  templateUrl: './stock-management.component.html',
})
export class StockManagementComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);

  private readonly LOC_KEY = 'yurt_stock_location';

  locations = signal<Location[]>([]);
  rows = signal<StockRow[]>([]);
  loading = signal(true);
  selectedLocationId = '';
  alerts = signal<StockAlerts>({ low: 0, out: 0, items: [] });
  tab = signal<'products' | 'ingredients' | 'recipes'>('products');
  readonly tabs = ['products', 'ingredients', 'recipes'] as const;

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe((locs) => {
      this.locations.set(locs);
      const saved = localStorage.getItem(this.LOC_KEY) ?? '';
      this.selectedLocationId = locs.some((l) => l.id === saved) ? saved : (locs[0]?.id ?? '');
      this.load();
    });
  }

  load(): void {
    if (!this.selectedLocationId) {
      this.rows.set([]);
      this.loading.set(false);
      return;
    }
    localStorage.setItem(this.LOC_KEY, this.selectedLocationId);
    this.api.getStockAlerts(this.selectedLocationId).subscribe((a) => this.alerts.set(a));
    this.loading.set(true);
    this.api.getAdminStock(this.selectedLocationId).subscribe({
      next: (items) => {
        this.rows.set(items.map((i) => this.toRow(i)));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  save(row: StockRow): void {
    row.saving = true;
    const qty = this.asCount(row.qtyInput);
    this.api
      .setAdminStock(row.menuItemId, this.selectedLocationId, {
        quantity: qty,
        lowStockAt: qty === null ? null : this.asCount(row.lowInput),
      })
      .subscribe({
        next: (saved) => {
          this.rows.update((list) =>
            list.map((r) => (r.menuItemId === saved.menuItemId ? this.toRow(saved) : r)),
          );
          this.toast.success('Saved');
          this.api.getStockAlerts(this.selectedLocationId).subscribe((a) => this.alerts.set(a));
        },
        error: () => {
          row.saving = false;
          this.toast.error('Could not save');
        },
      });
  }

  /** Stop counting this item: it becomes unlimited again. */
  clear(row: StockRow): void {
    row.qtyInput = null;
    row.lowInput = null;
    this.save(row);
  }

  isDirty(row: StockRow): boolean {
    return (
      this.asCount(row.qtyInput) !== row.quantity || this.asCount(row.lowInput) !== row.lowStockAt
    );
  }

  private asCount(value: number | null | undefined): number | null {
    return value === null || value === undefined || Number.isNaN(Number(value)) || `${value}` === ''
      ? null
      : Math.max(0, Math.floor(Number(value)));
  }

  private toRow(item: StockItem): StockRow {
    return { ...item, qtyInput: item.quantity, lowInput: item.lowStockAt, saving: false };
  }
}
