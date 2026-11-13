import { Component, inject, Input, OnChanges, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { cleanBarcode, YurtApiService } from 'shared-api';
import { Ingredient } from 'shared-models';
import { ToastService } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';

interface IngredientRow extends Ingredient {
  qtyInput: number | null;
  lowInput: number | null;
  costInput: number | null;
  saving: boolean;
}

/**
 * Ingredients, how much of each a branch has and what one unit costs (Phase 6, `own_pos`). A delivery adds to the
 * count and, with its price, keeps the cost up to date as a running average.
 */
@Component({
  selector: 'app-ingredients-panel',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe],
  templateUrl: './ingredients-panel.component.html',
})
export class IngredientsPanelComponent implements OnChanges {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);

  @Input() locationId = '';

  rows = signal<IngredientRow[]>([]);
  loading = signal(true);
  newName = '';
  newUnit = 'g';
  newBarcode = '';

  /** The "delivery arrived" form. */
  deliveryId = '';
  deliveryQty: number | null = null;
  deliveryCost: number | null = null;
  deliveryNote = '';
  deliverySaving = signal(false);

  ngOnChanges(): void {
    this.load();
  }

  load(): void {
    if (!this.locationId) {
      this.rows.set([]);
      this.loading.set(false);
      return;
    }
    this.loading.set(true);
    this.api.getIngredients(this.locationId).subscribe({
      next: (list) => {
        this.rows.set(list.map((i) => this.toRow(i)));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  add(): void {
    const name = this.newName.trim();
    if (!name) return;
    this.api.createIngredient({ name, unit: this.newUnit.trim() || 'pcs', barcode: cleanBarcode(this.newBarcode) || null }).subscribe({
      next: () => {
        this.newName = '';
        this.newBarcode = '';
        this.load();
      },
      error: () => this.toast.error(this.lang.t('common.addFailed')),
    });
  }

  remove(row: IngredientRow): void {
    const msg =
      row.usedInItems > 0
        ? this.lang.t('ing.confirmDeleteUsed', { name: row.name, count: row.usedInItems })
        : this.lang.t('ing.confirmDelete', { name: row.name });
    if (!confirm(msg)) return;
    this.api.deleteIngredient(row.id).subscribe({ next: () => this.load() });
  }

  save(row: IngredientRow): void {
    row.saving = true;
    const cost = this.asNumber(row.costInput);
    if (cost !== (row.costPerUnit ?? null)) {
      this.api.setIngredientCost(row.id, cost).subscribe({
        next: (saved) => {
          row.costPerUnit = saved.costPerUnit ?? null;
          if (this.isCountDirty(row)) this.saveCount(row);
          else {
            row.saving = false;
            this.toast.success(this.lang.t('common.saved'));
          }
        },
        error: () => {
          row.saving = false;
          this.toast.error(this.lang.t('common.saveFailed'));
        },
      });
      return;
    }
    this.saveCount(row);
  }

  private saveCount(row: IngredientRow): void {
    const qty = this.asNumber(row.qtyInput);
    const cost = row.costPerUnit ?? null;
    this.api
      .setIngredientStock(row.id, this.locationId, {
        quantity: qty,
        lowStockAt: qty === null ? null : this.asNumber(row.lowInput),
      })
      .subscribe({
        next: (saved) => {
          this.rows.update((list) =>
            list.map((r) => (r.id === saved.id ? this.toRow({ ...saved, costPerUnit: cost }) : r)),
          );
          this.toast.success(this.lang.t('common.saved'));
        },
        error: () => {
          row.saving = false;
          this.toast.error(this.lang.t('common.saveFailed'));
        },
      });
  }

  /** The last pack scanned (or typed) in the delivery form, when no ingredient has its barcode yet. */
  unknownScan = signal<string | null>(null);

  /** A pack was scanned: pick its ingredient. A code nobody has can be linked to the ingredient picked next. */
  scanPack(raw: string): void {
    const code = cleanBarcode(raw);
    if (!code) return;
    const found = this.rows().find((r) => r.barcode && cleanBarcode(r.barcode) === code);
    if (found) {
      this.unknownScan.set(null);
      this.deliveryId = found.id;
      return;
    }
    this.unknownScan.set(code);
    this.toast.error(this.lang.t('ing.scanUnknown', { code }));
  }

  /** Gives the picked ingredient the barcode that was just scanned. */
  linkScan(): void {
    const code = this.unknownScan();
    const row = this.deliveryRow;
    if (!code || !row) return;
    this.saveBarcode(row, code, () => this.unknownScan.set(null));
  }

  /** Saves a barcode typed or scanned into an ingredient's row. Empty removes it. */
  saveBarcode(row: IngredientRow, value: string, done?: () => void): void {
    const barcode = cleanBarcode(value);
    if (barcode === (row.barcode ?? '')) return;
    this.api.updateIngredient(row.id, { name: row.name, unit: row.unit, barcode: barcode || null }).subscribe({
      next: () => {
        row.barcode = barcode || null;
        this.rows.update((list) => [...list]);
        this.toast.success(this.lang.t('ing.barcodeSaved'));
        done?.();
      },
      error: () => this.toast.error(this.lang.t('common.saveFailed')),
    });
  }

  get deliveryRow(): IngredientRow | undefined {
    return this.rows().find((r) => r.id === this.deliveryId);
  }

  /** A delivery arrived: add it to the count; with a price, the cost per unit becomes the running average. */
  addDelivery(): void {
    const quantity = this.asNumber(this.deliveryQty);
    if (!this.deliveryId || !quantity) return;
    this.deliverySaving.set(true);
    this.api
      .restockIngredient({
        locationId: this.locationId,
        ingredientId: this.deliveryId,
        quantity,
        totalCost: this.asNumber(this.deliveryCost),
        note: this.deliveryNote.trim() || null,
      })
      .subscribe({
        next: (res) => {
          this.deliverySaving.set(false);
          this.toast.success(
            this.lang.t('ing.delivered', {
              name: res.name,
              left: `${res.left} ${res.unit ?? ''}`.trim(),
            }),
          );
          this.deliveryQty = null;
          this.deliveryCost = null;
          this.deliveryNote = '';
          this.load();
        },
        error: () => this.deliverySaving.set(false),
      });
  }

  stopCounting(row: IngredientRow): void {
    row.qtyInput = null;
    row.lowInput = null;
    this.save(row);
  }

  isDirty(row: IngredientRow): boolean {
    return this.isCountDirty(row) || this.asNumber(row.costInput) !== (row.costPerUnit ?? null);
  }

  private isCountDirty(row: IngredientRow): boolean {
    return (
      this.asNumber(row.qtyInput) !== row.quantity || this.asNumber(row.lowInput) !== row.lowStockAt
    );
  }

  private asNumber(value: number | null | undefined): number | null {
    return value === null || value === undefined || `${value}` === '' || Number.isNaN(Number(value))
      ? null
      : Math.max(0, Number(value));
  }

  private toRow(i: Ingredient): IngredientRow {
    return {
      ...i,
      qtyInput: i.quantity,
      lowInput: i.lowStockAt,
      costInput: i.costPerUnit ?? null,
      saving: false,
    };
  }
}
