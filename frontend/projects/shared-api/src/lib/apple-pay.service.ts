import { Platform } from '@angular/cdk/platform';
import { inject, Injectable } from '@angular/core';
import { registerPlugin } from '@capacitor/core';
import { firstValueFrom } from 'rxjs';
import { PaymentProvider } from 'shared-models';
import { YurtApiService } from './yurt-api.service';

export interface ApplePayStartPaymentOptions {
  orderId: string;
  /** Order total in major currency units (KZT), matching Order.Total. */
  amount: number;
  /** Apple Merchant ID, fetched from the backend config so it isn't hardcoded. */
  merchantId: string;
}

export interface ApplePayStartPaymentResult {
  /** The Freedom Pay payment id returned by `confirmApplePayment`, forwarded to our
   * backend's confirm endpoint so it can be recorded against the Payment row. */
  freedomPaymentId: string;
}

export interface ApplePayNativePlugin {
  canMakePayments(): Promise<{ available: boolean }>;
  startPayment(options: ApplePayStartPaymentOptions): Promise<ApplePayStartPaymentResult>;
}

/**
 * Local (non-npm) Capacitor plugin — see `frontend/ios/App/App/ApplePayPlugin.swift`.
 * Native code only exists on iOS; registerPlugin's web fallback would reject every call,
 * which is fine since every call site here is already gated by `isAvailable()`.
 */
const ApplePayNative = registerPlugin<ApplePayNativePlugin>('ApplePay');

@Injectable({ providedIn: 'root' })
export class ApplePayService {
  private platform = inject(Platform);
  private api = inject(YurtApiService);

  /** True only when: running on iOS, the backend hasn't killed the feature
   * (`FreedomPay:Enabled`), and the device itself can present the Apple Pay sheet. */
  async isAvailable(): Promise<boolean> {
    if (!this.platform.IOS) {
      console.log('Apple Pay unavailable: not running on iOS.');
      return false;
    }

    try {
      const config = await firstValueFrom(this.api.getApplePayConfig());
      console.log('Apple Pay config:', config);
      if (!config.enabled) {
        console.log('Apple Pay unavailable: backend FreedomPay:Enabled is false.');
        return false;
      }

      const native = await ApplePayNative.canMakePayments();
      console.log('Apple Pay native canMakePayments:', native);
      if (!native.available) {
        console.log('Apple Pay unavailable: no Visa/Mastercard card in Wallet (or device unsupported).');
      }
      return native.available;
    } catch (error) {
      console.error('Apple Pay availability check failed:', error);
      return false;
    }
  }

  /**
   * Runs the full Apple Pay flow for an already-created order: registers a pending
   * payment, presents the native Apple Pay sheet via the Freedom Pay SDK, then confirms
   * the result with the backend. Throws on cancellation or failure — callers should
   * distinguish a user cancel (leave the order as-is, no error toast) from a real
   * failure (show an error) by checking `error?.message` for Capacitor's cancel code.
   */
  async pay(orderId: string, amount: number): Promise<void> {
    const config = await firstValueFrom(this.api.getApplePayConfig());
    if (!config.enabled) {
      throw new Error('Apple Pay is not enabled.');
    }

    const invoice = await firstValueFrom(
      this.api.createPayment({ orderId, provider: PaymentProvider.FreedomPay }),
    );

    const result = await ApplePayNative.startPayment({
      orderId,
      amount,
      merchantId: config.appleMerchantId,
    });

    await firstValueFrom(
      this.api.confirmApplePay({
        invoiceId: invoice.invoiceId,
        freedomPaymentId: result.freedomPaymentId,
      }),
    );
  }
}
