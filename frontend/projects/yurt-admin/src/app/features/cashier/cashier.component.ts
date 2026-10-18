import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { Location, Shift, ShiftReport } from 'shared-models';
import { Currency2Pipe, ToastService } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';

/**
 * Cashier screen (Phase 6, `own_pos`): open a shift with the cash in the drawer, record cash in/out, see the running
 * (X) report, then close with the cash counted to get the Z report.
 */
@Component({
  selector: 'app-cashier',
  standalone: true,
  imports: [FormsModule, DatePipe, Currency2Pipe, AdminTranslatePipe],
  templateUrl: './cashier.component.html',
})
export class CashierComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);

  private readonly LOC_KEY = 'yurt_stock_location';

  locations = signal<Location[]>([]);
  shift = signal<Shift | null>(null);
  report = signal<ShiftReport | null>(null);
  history = signal<Shift[]>([]);
  loading = signal(true);
  busy = signal(false);
  /** True once the report on screen is the Z report of a shift that was just closed or picked from history. */
  viewingClosed = signal(false);

  locationId = '';
  openingCash: number | null = 0;
  moveAmount: number | null = null;
  moveOut = false;
  moveReason = '';
  countedCash: number | null = null;
  closeNote = '';

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe((locs) => {
      this.locations.set(locs);
      const saved = localStorage.getItem(this.LOC_KEY) ?? '';
      this.locationId = locs.some((l) => l.id === saved) ? saved : (locs[0]?.id ?? '');
      this.load();
    });
  }

  load(): void {
    if (!this.locationId) {
      this.loading.set(false);
      return;
    }
    localStorage.setItem(this.LOC_KEY, this.locationId);
    this.loading.set(true);
    this.viewingClosed.set(false);
    this.api.getCurrentShift(this.locationId).subscribe({
      next: (s) => {
        this.shift.set(s ?? null);
        this.report.set(null);
        if (s) this.refreshReport();
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
    this.api
      .getShifts(this.locationId)
      .subscribe((list) => this.history.set(list.filter((s) => s.closedAt)));
  }

  refreshReport(): void {
    const s = this.shift();
    if (!s) return;
    this.api.getShiftReport(s.id).subscribe((r) => this.report.set(r));
  }

  open(): void {
    this.busy.set(true);
    this.api.openShift(this.locationId, Math.max(0, Number(this.openingCash) || 0)).subscribe({
      next: (s) => {
        this.busy.set(false);
        this.shift.set(s);
        this.viewingClosed.set(false);
        this.refreshReport();
      },
      error: () => {
        this.busy.set(false);
        this.toast.error('Could not open the shift (one may already be open)');
      },
    });
  }

  addMovement(): void {
    const s = this.shift();
    const amount = Number(this.moveAmount);
    if (!s || !(amount > 0) || !this.moveReason.trim()) return;
    this.busy.set(true);
    this.api
      .addCashMovement(s.id, { amount, isOut: this.moveOut, reason: this.moveReason.trim() })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.moveAmount = null;
          this.moveReason = '';
          this.refreshReport();
        },
        error: () => {
          this.busy.set(false);
          this.toast.error('Could not save');
        },
      });
  }

  close(): void {
    const s = this.shift();
    if (!s || this.countedCash === null || `${this.countedCash}` === '') return;
    if (!confirm('Close the shift? This cannot be undone.')) return;
    this.busy.set(true);
    this.api
      .closeShift(s.id, {
        countedCash: Math.max(0, Number(this.countedCash)),
        note: this.closeNote.trim() || undefined,
      })
      .subscribe({
        next: (r) => {
          this.busy.set(false);
          this.report.set(r);
          this.viewingClosed.set(true);
          this.shift.set(null);
          this.countedCash = null;
          this.closeNote = '';
          this.api
            .getShifts(this.locationId)
            .subscribe((list) => this.history.set(list.filter((x) => x.closedAt)));
        },
        error: () => {
          this.busy.set(false);
          this.toast.error('Could not close the shift');
        },
      });
  }

  showClosed(s: Shift): void {
    this.api.getShiftReport(s.id).subscribe((r) => {
      this.report.set(r);
      this.viewingClosed.set(true);
    });
  }

  print(): void {
    window.print();
  }
}
