import { Component, inject, Input, OnChanges, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { YurtApiService } from 'shared-api';
import { StocktakeResult } from 'shared-models';
import { Currency2Pipe, LocalNumberPipe, ToastService } from 'shared-ui';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';

interface CountRow {
  kind: 'dish' | 'ingredient';
  id: string;
  name: string;
  unit: string | null;
  expected: number;
  costPerUnit: number | null;
  /** What staff counted on the shelf; empty = not counted this time. */
  counted: number | null;
}

/** The counted value of a row as a number, or null when left empty. */
export function countedValue(value: number | null | undefined): number | null {
  if (value === null || value === undefined || `${value}` === '') return null;
  const n = Number(value);
  return Number.isNaN(n) || n < 0 ? null : n;
}

/**
 * Stocktake (stock pack, `own_pos`): count what is really on the shelf and see the difference from what the system
 * expected, with its money value. Saving replaces the counts and logs each difference in the history.
 */
@Component({
  selector: 'app-stocktake-panel',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe, LocalNumberPipe, Currency2Pipe],
  templateUrl: './stocktake-panel.component.html',
})
export class StocktakePanelComponent implements OnChanges {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);

  @Input() locationId = '';

  rows = signal<CountRow[]>([]);
  loading = signal(true);
  saving = signal(false);
  result = signal<StocktakeResult | null>(null);
  note = '';

  ngOnChanges(): void {
    this.result.set(null);
    this.load();
  }

  load(): void {
    if (!this.locationId) {
      this.rows.set([]);
      this.loading.set(false);
      return;
    }
    this.loading.set(true);
    forkJoin({
      ingredients: this.api.getIngredients(this.locationId),
      dishes: this.api.getAdminStock(this.locationId),
    }).subscribe({
      next: ({ ingredients, dishes }) => {
        this.rows.set([
          ...ingredients
            .filter((i) => i.quantity !== null)
            .map((i) => ({
              kind: 'ingredient' as const,
              id: i.id,
              name: i.name,
              unit: i.unit,
              expected: i.quantity ?? 0,
              costPerUnit: i.costPerUnit ?? null,
              counted: null,
            })),
          ...dishes
            .filter((d) => d.quantity !== null)
            .map((d) => ({
              kind: 'dish' as const,
              id: d.menuItemId,
              name: d.name,
              unit: null,
              expected: d.quantity ?? 0,
              costPerUnit: null,
              counted: null,
            })),
        ]);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  difference(row: CountRow): number | null {
    const c = countedValue(row.counted);
    return c === null ? null : c - row.expected;
  }

  get countedRows(): CountRow[] {
    return this.rows().filter((r) => countedValue(r.counted) !== null);
  }

  save(): void {
    const lines = this.countedRows.map((r) => ({
      kind: r.kind,
      itemId: r.id,
      counted: countedValue(r.counted)!,
    }));
    if (!lines.length) return;
    if (lines.some((l) => l.kind === 'dish' && !Number.isInteger(l.counted))) {
      this.toast.error(this.lang.t('moves.wholeOnly'));
      return;
    }
    this.saving.set(true);
    this.api
      .saveStocktake({ locationId: this.locationId, note: this.note.trim() || null, lines })
      .subscribe({
        next: (res) => {
          this.saving.set(false);
          this.result.set(res);
          this.note = '';
          this.toast.success(this.lang.t('common.saved'));
          this.load();
        },
        error: () => this.saving.set(false),
      });
  }
}
