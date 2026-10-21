import { Component, computed, EventEmitter, inject, Input, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { Order, SplitPaymentLine } from 'shared-models';
import { ToastService } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';

interface PartRow {
  method: string;
  amount: number | null;
}

/** Pay one order in parts with different methods (Phase 6, `own_pos`). */
@Component({
  selector: 'app-split-payment',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe],
  templateUrl: './split-payment.component.html',
})
export class SplitPaymentComponent {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);

  readonly methods = ['Cash', 'Card', 'KaspiBank', 'HalykBank', 'FreedomBank', 'ApplePay', 'Other'];

  @Input({ required: true }) set order(o: Order) {
    this._order = o;
    this.toPay.set(Math.max(0, o.total - (o.loyaltyPointsSpent ?? 0)));
    this.open.set(false);
  }
  @Output() saved = new EventEmitter<SplitPaymentLine[]>();

  private _order!: Order;
  open = signal(false);
  saving = signal(false);
  toPay = signal(0);
  rows = signal<PartRow[]>([]);
  left = computed(
    () =>
      Math.round((this.toPay() - this.rows().reduce((s, r) => s + (r.amount ?? 0), 0)) * 100) / 100,
  );

  start(): void {
    this.open.set(true);
    this.api.getSplitPayment(this._order.id).subscribe({
      next: (p) =>
        this.rows.set(
          p.lines.length
            ? p.lines.map((l) => ({ method: l.method, amount: l.amount }))
            : [
                { method: 'Cash', amount: null },
                { method: 'Card', amount: null },
              ],
        ),
      error: () => this.open.set(false),
    });
  }

  add(): void {
    this.rows.update((r) => [
      ...r,
      { method: 'Card', amount: this.left() > 0 ? this.left() : null },
    ]);
  }

  remove(i: number): void {
    this.rows.update((r) => r.filter((_, j) => j !== i));
  }

  fillRest(i: number): void {
    this.rows.update((r) =>
      r.map((x, j) =>
        j === i ? { ...x, amount: Math.round(((x.amount ?? 0) + this.left()) * 100) / 100 } : x,
      ),
    );
  }

  save(): void {
    this.saving.set(true);
    const lines = this.rows()
      .filter((r) => (r.amount ?? 0) > 0)
      .map((r) => ({ method: r.method, amount: r.amount! }));
    this.api.saveSplitPayment(this._order.id, lines).subscribe({
      next: (p) => {
        this.saving.set(false);
        this.open.set(false);
        this.toast.success('Payment updated');
        this.saved.emit(p.lines);
      },
      error: () => this.saving.set(false),
    });
  }
}
