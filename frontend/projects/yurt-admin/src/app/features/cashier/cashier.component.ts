import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { FiscalReceipt, Location, Shift, ShiftReport } from 'shared-models';
import { Currency2Pipe, ToastService, LocalDatePipe } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';

/**
 * Cashier screen (Phase 6, `own_pos`): open a shift with the cash in the drawer, record cash in/out, see the running
 * (X) report, then close with the cash counted to get the Z report.
 */
@Component({
  selector: 'app-cashier',
  standalone: true,
  imports: [LocalDatePipe, FormsModule, Currency2Pipe, AdminTranslatePipe],
  templateUrl: './cashier.component.html',
})
export class CashierComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);

  private readonly LOC_KEY = 'yurt_stock_location';

  locations = signal<Location[]>([]);
  shift = signal<Shift | null>(null);
  report = signal<ShiftReport | null>(null);
  history = signal<Shift[]>([]);
  loading = signal(true);
  busy = signal(false);
  drawerBusy = signal(false);
  /** True once the report on screen is the Z report of a shift that was just closed or picked from history. */
  viewingClosed = signal(false);

  locationId = '';
  openingCash: number | null = 0;
  moveAmount: number | null = null;
  moveOut = false;
  moveReason = '';
  countedCash: number | null = null;
  closeNote = '';
  /** Official tax receipts of this branch not sent to the cash register yet (empty while fiscal receipts are off). */
  waitingReceipts = signal<FiscalReceipt[]>([]);

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
    this.loadWaitingReceipts();
  }

  loadWaitingReceipts(): void {
    if (!this.locationId) return;
    this.api.getWaitingFiscalReceipts(this.locationId).subscribe({
      next: (list) => this.waitingReceipts.set(list),
      error: () => this.waitingReceipts.set([]),
    });
  }

  retryReceipt(r: FiscalReceipt): void {
    this.busy.set(true);
    this.api.retryFiscalReceipt(r.id).subscribe({
      next: (saved) => {
        this.busy.set(false);
        if (saved.status === 'Pending') this.toast.error(saved.lastError || 'Not sent');
        this.loadWaitingReceipts();
      },
      error: () => this.busy.set(false),
    });
  }

  retryZReport(shiftId: string): void {
    this.busy.set(true);
    this.api.retryFiscalZReport(shiftId).subscribe({
      next: (z) => {
        this.busy.set(false);
        const r = this.report();
        if (r && r.shift.id === shiftId)
          this.report.set({ ...r, shift: { ...r.shift, fiscalZReportAt: z.fiscalZReportAt, fiscalZReportError: z.fiscalZReportError } });
        if (z.fiscalZReportError) this.toast.error(z.fiscalZReportError);
      },
      error: () => this.busy.set(false),
    });
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

  close(closeWithWaitingReceipts = false): void {
    const s = this.shift();
    if (!s || this.countedCash === null || `${this.countedCash}` === '') return;
    if (!closeWithWaitingReceipts && !confirm('Close the shift? This cannot be undone.')) return;
    this.busy.set(true);
    this.api
      .closeShift(s.id, {
        countedCash: Math.max(0, Number(this.countedCash)),
        note: this.closeNote.trim() || undefined,
        closeWithWaitingReceipts,
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
        error: (err) => {
          this.busy.set(false);
          // 409 with receipts still waiting: the cashier may close anyway; they stay queued and are sent later.
          if (err?.status === 409 && !closeWithWaitingReceipts && /receipt/i.test(err?.error?.title ?? '')) {
            this.loadWaitingReceipts();
            if (confirm(this.lang.t('cash.fiscal.closeAnyway'))) this.close(true);
            return;
          }
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

  /** Opens the cash drawer plugged into this branch's receipt printer (for change, cash in/out or counting). */
  openDrawer(): void {
    this.drawerBusy.set(true);
    this.api.openCashDrawer(this.locationId).subscribe({
      next: (r) => {
        this.drawerBusy.set(false);
        if (r.printers > 0) this.toast.success(this.lang.t('cash.drawerOpened'));
        else this.toast.error(this.lang.t('cash.noDrawer'));
      },
      error: () => {
        this.drawerBusy.set(false);
        this.toast.error(this.lang.t('cash.drawerFailed'));
      },
    });
  }

  print(): void {
    window.print();
  }
}
