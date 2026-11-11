import { Currency2Pipe, LocalDatePipe, LocalNumberPipe } from 'shared-ui';
import { Component, inject, Input, OnChanges, signal } from '@angular/core';
import { AuthStateService, YurtApiService } from 'shared-api';
import { StockChange, WriteOffSummary } from 'shared-models';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';

/**
 * Stock history of a branch (Phase 6, `own_pos`): who changed which count, how (delivery, write-off and why, move,
 * stocktake, by hand) and when, plus the write-offs of the last 30 days by reason.
 */
@Component({
  selector: 'app-stock-history-panel',
  standalone: true,
  imports: [LocalDatePipe, LocalNumberPipe, Currency2Pipe, AdminTranslatePipe],
  templateUrl: './stock-history-panel.component.html',
})
export class StockHistoryPanelComponent implements OnChanges {
  private api = inject(YurtApiService);
  private lang = inject(AdminLangService);
  private auth = inject(AuthStateService);

  @Input() locationId = '';

  /** Money values (write-off totals, cost of each change) are for the CEO only. */
  readonly isCeo = this.auth.isCeo;

  rows = signal<StockChange[]>([]);
  writeOffs = signal<WriteOffSummary | null>(null);
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
    if (this.isCeo) {
      this.api.getWriteOffSummary(this.locationId).subscribe({
        next: (w) => this.writeOffs.set(w),
        error: () => this.writeOffs.set(null),
      });
    }
    this.api.getStockHistory(this.locationId).subscribe({
      next: (list) => {
        this.rows.set(list);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  /** What kind of change a row was, in words: "Spilled", "Sent to Midtown", "Delivery"... */
  describe(row: StockChange): string {
    switch (row.type) {
      case 'writeoff':
        return `${this.lang.t('hist.type.writeoff')}: ${this.lang.t(`moves.reason.${row.reason ?? 'other'}`)}`;
      case 'transfer_out':
        return this.lang.t('moves.sentTo', { branch: row.otherLocationName ?? '' });
      case 'transfer_in':
        return this.lang.t('moves.receivedFrom', { branch: row.otherLocationName ?? '' });
      case 'restock':
      case 'restock_cancel':
      case 'stocktake':
      case 'shortfall':
        return this.lang.t(`hist.type.${row.type}`);
      default:
        return this.lang.t('hist.type.set');
    }
  }

  isCounted(value: number | null | undefined): value is number {
    return value !== null && value !== undefined;
  }
}
