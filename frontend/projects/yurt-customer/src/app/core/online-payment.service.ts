import { inject, Injectable, signal } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { firstValueFrom } from 'rxjs';
import { YurtApiService } from 'shared-api';

/**
 * Paying an order online through the cafe's payment provider (Stripe). The customer pays on the provider's own
 * payment page, which offers card, Apple Pay and Google Pay where the device supports them.
 */
@Injectable({ providedIn: 'root' })
export class OnlinePaymentService {
  private api = inject(YurtApiService);

  /** True when this cafe takes online payments (module on, provider chosen, keys set). */
  readonly available = signal(false);

  load(): void {
    this.api.getPaymentConfig().subscribe({
      next: (c) => this.available.set(!!c.provider),
      error: () => this.available.set(false),
    });
  }

  /**
   * Opens the payment page for an order. In the browser it opens in the same tab and comes back to the order page.
   * In the phone apps the app has no web address of its own, so the server picks the cafe's site to come back to,
   * and the app hands the page to the phone's browser (Capacitor opens other sites there), where Apple Pay and
   * Google Pay work; the order page here updates by itself once the payment arrives.
   */
  async open(orderId: string): Promise<void> {
    const native = Capacitor.isNativePlatform();
    const invoice = await firstValueFrom(
      this.api.createPayment({ orderId, returnUrl: native ? undefined : window.location.origin }),
    );
    window.location.href = invoice.paymentUrl;
  }
}
