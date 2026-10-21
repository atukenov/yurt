import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, inject, Input, OnChanges, signal } from '@angular/core';
import { YurtApiService } from 'shared-api';
import { StockChange } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';

/** Restocking history of a branch (Phase 6, `own_pos`): who set which count by hand, and when. */
@Component({
  selector: 'app-stock-history-panel',
  standalone: true,
  imports: [AdminTranslatePipe, DatePipe, DecimalPipe],
  templateUrl: './stock-history-panel.component.html',
})
export class StockHistoryPanelComponent implements OnChanges {
  private api = inject(YurtApiService);

  @Input() locationId = '';

  rows = signal<StockChange[]>([]);
  loading = signal(true);

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
    this.api.getStockHistory(this.locationId).subscribe({
      next: (list) => {
        this.rows.set(list);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  isCounted(value: number | null | undefined): value is number {
    return value !== null && value !== undefined;
  }
}
