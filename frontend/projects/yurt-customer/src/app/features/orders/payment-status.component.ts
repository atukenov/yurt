import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { OrderStatus, PaymentProvider, PaymentStatus } from 'shared-models';

@Component({
  selector: 'app-payment-status',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="mt-4 rounded-3xl border border-stone-200 bg-stone-50 p-4 text-sm text-stone-700">
      <div class="flex items-center justify-between mb-3">
        <span class="font-semibold">Payment status</span>
        <span class="uppercase text-xs tracking-wide text-stone-500">{{ status }}</span>
      </div>
      <div class="space-y-2 text-sm">
        <div class="flex justify-between">
          <span>Provider</span>
          <span>{{ provider }}</span>
        </div>
        <div class="flex justify-between">
          <span>Order status</span>
          <span>{{ orderStatus }}</span>
        </div>
        @if (paymentUrl) {
          <div class="text-stone-500 truncate">URL: {{ paymentUrl }}</div>
        }
      </div>
    </div>
  `,
})
export class PaymentStatusComponent {
  @Input() status: PaymentStatus | string = PaymentStatus.Pending;
  @Input() provider: PaymentProvider | string = PaymentProvider.KaspiSandbox;
  @Input() orderStatus: OrderStatus | string = OrderStatus.Created;
  @Input() paymentUrl?: string;
}
