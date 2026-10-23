import { Component, inject, Input, OnChanges, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { Ingredient } from 'shared-models';
import { ToastService } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';

interface IngredientRow extends Ingredient {
  qtyInput: number | null;
  lowInput: number | null;
  saving: boolean;
}

/** Ingredients and how much of each a branch has (Phase 6, `own_pos`). */
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
    this.api.createIngredient({ name, unit: this.newUnit.trim() || 'pcs' }).subscribe({
      next: () => {
        this.newName = '';
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
    const qty = this.asNumber(row.qtyInput);
    this.api
      .setIngredientStock(row.id, this.locationId, {
        quantity: qty,
        lowStockAt: qty === null ? null : this.asNumber(row.lowInput),
      })
      .subscribe({
        next: (saved) => {
          this.rows.update((list) => list.map((r) => (r.id === saved.id ? this.toRow(saved) : r)));
          this.toast.success(this.lang.t('common.saved'));
        },
        error: () => {
          row.saving = false;
          this.toast.error(this.lang.t('common.saveFailed'));
        },
      });
  }

  stopCounting(row: IngredientRow): void {
    row.qtyInput = null;
    row.lowInput = null;
    this.save(row);
  }

  isDirty(row: IngredientRow): boolean {
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
    return { ...i, qtyInput: i.quantity, lowInput: i.lowStockAt, saving: false };
  }
}
