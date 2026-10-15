import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-payment-qr-code',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="rounded-3xl border border-stone-200 bg-white p-4 shadow-sm">
      <div class="text-sm font-semibold text-stone-700 mb-3">{{ label }}</div>
      <div class="grid place-items-center">
        <img
          *ngIf="qrCode"
          [src]="qrCode"
          alt="Payment QR code"
          class="w-64 h-64 rounded-3xl border border-stone-100 object-contain"
        />
        <div *ngIf="!qrCode" class="text-stone-400 text-sm">QR code unavailable.</div>
      </div>
      @if (amount != null) {
        <div class="mt-4 text-center text-stone-700 text-sm">
          Amount: <strong>{{ amount | number: '1.2-2' }} KZT</strong>
        </div>
      }
    </div>
  `,
})
export class PaymentQrCodeComponent {
  @Input() qrCode = '';
  @Input() label = 'Scan with Kaspi app';
  @Input() amount?: number;
}
