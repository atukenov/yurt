import { Component, computed, effect, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService, AuthStateService, ModuleService } from 'shared-api';
import { CartItem, LoyaltyBalance, PaymentMethod } from 'shared-models';
import { CartService } from './cart.service';
import { ToastService, Currency2Pipe } from 'shared-ui';
import { TranslatePipe } from '../../core/translate.pipe';
import { LocationService } from '../../core/location.service';
import { LangService } from '../../core/lang.service';
import { StoreStatusService } from '../../core/store-status.service';
import { OnlinePaymentService } from '../../core/online-payment.service';

@Component({
  selector: 'app-cart',
  standalone: true,
  imports: [CommonModule, FormsModule, Currency2Pipe, TranslatePipe],
  templateUrl: './cart.component.html',
  styleUrl: './cart.component.css',
})
export class CartComponent implements OnInit {
  readonly cart = inject(CartService);
  readonly langService = inject(LangService);
  private api = inject(YurtApiService);
  readonly modules = inject(ModuleService);
  readonly online = inject(OnlinePaymentService);
  private router = inject(Router);
  private toast = inject(ToastService);

  private auth = inject(AuthStateService);
  private locationSvc = inject(LocationService);
  private storeStatus = inject(StoreStatusService);
  readonly locationName = this.locationSvc.locationName;
  readonly PaymentMethod = PaymentMethod;

  loading = signal(false);
  selectedPaymentMethod = signal<PaymentMethod | null>(null);
  /** Whether the chosen branch offers a payment method (all of them unless the cafe narrowed it). */
  offers = (method: PaymentMethod): boolean => {
    const list = this.locationSvc.paymentMethods();
    return !list || list.includes(method);
  };
  private checkoutKey: string | null = null;

  // iiko loyalty — pay with bonus points
  loyalty = signal<LoyaltyBalance | null>(null);
  bonusInput = signal<number | null>(null);
  readonly fullBonusBalance = computed(() => {
    const l = this.loyalty();
    if (!l?.enabled || !l.available || !l.balance || l.balance <= 0) return 0;
    return l.balance;
  });
  readonly maxBonus = computed(() => Math.min(this.fullBonusBalance(), this.cart.total()));
  readonly appliedBonus = computed(() => {
    const raw = this.bonusInput() ?? 0;
    return Math.min(Math.max(raw, 0), this.maxBonus());
  });
  readonly remainingBonus = computed(() => this.fullBonusBalance() - this.appliedBonus());
  readonly payableTotal = computed(() => this.cart.total() - this.appliedBonus());
  readonly fullyCoveredByBonus = computed(() => this.appliedBonus() >= this.cart.total());

  // Pop animation on the total whenever it changes (bonus toggle, promo, qty change)
  totalPulse = signal(false);
  private lastPayableTotal = this.payableTotal();

  constructor() {
    // A code applied before the marketing module went off no longer counts at checkout.
    effect(() => {
      if (!this.modules.isEnabled('marketing') && this.cart.appliedDiscount()) this.cart.clearDiscount();
    });
    // A method the branch stopped offering cannot stay selected.
    effect(() => {
      const picked = this.selectedPaymentMethod();
      if (picked && !this.offers(picked)) this.selectedPaymentMethod.set(null);
    });
    effect(() => {
      const value = this.payableTotal();
      if (value === this.lastPayableTotal) return;
      this.lastPayableTotal = value;
      this.totalPulse.set(false);
      queueMicrotask(() => {
        this.totalPulse.set(true);
        setTimeout(() => this.totalPulse.set(false), 350);
      });
    });
  }

  ngOnInit(): void {
    this.api.getLoyaltyBalance().subscribe({
      next: (l) => this.loyalty.set(l),
      error: () => {},
    });
    this.online.load();
  }

  useAllBonus(): void {
    this.bonusInput.set(this.maxBonus());
  }
  expandedNoteKeys = signal<Set<string>>(new Set());
  expandedItemKeys = signal<Set<string>>(new Set());

  promoCodeInput = signal('');
  promoLoading = signal(false);
  promoError = signal<string | null>(null);
  showConfirm = signal(false);

  toggleNote(key: string): void {
    this.expandedNoteKeys.update((s) => {
      const next = new Set(s);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  isNoteExpanded(key: string): boolean {
    return this.expandedNoteKeys().has(key);
  }

  toggleItem(key: string): void {
    this.expandedItemKeys.update((s) => {
      const next = new Set(s);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  isItemExpanded(key: string): boolean {
    return this.expandedItemKeys().has(key);
  }

  itemLineTotal(item: CartItem): number {
    const toppings = item.selectedToppings?.reduce((s, t) => s + t.price, 0) ?? 0;
    return (item.price + toppings) * item.quantity;
  }

  goToMenu(): void {
    this.router.navigate(['/menu']);
  }

  goToLocations(): void {
    this.router.navigate(['/locations'], { queryParams: { returnTo: '/cart' } });
  }

  openConfirm(): void {
    if (!this.locationSvc.locationId()) {
      this.toast.warning(this.langService.t('cart.selectLocationFirst'));
      this.goToLocations();
      return;
    }
    this.showConfirm.set(true);
  }

  applyPromoCode(): void {
    const code = this.promoCodeInput().trim();
    if (!code) return;
    this.promoError.set(null);
    this.promoLoading.set(true);
    this.api.validateDiscountCode(code, this.cart.subtotal()).subscribe({
      next: (res) => {
        this.promoLoading.set(false);
        if (res.isValid) {
          this.cart.applyDiscount(code, res);
          this.toast.success(`${this.langService.t('cart.promoCode')}: ${res.description}`);
        } else {
          const msg = res.message;
          const key = msg.includes('not found') ? 'cart.promoNotFound'
            : msg.includes('not yet active') ? 'cart.promoNotActive'
            : msg.includes('expired') ? 'cart.promoExpired'
            : msg.includes('usage limit') ? 'cart.promoMaxUses'
            : msg.includes('Minimum order') ? 'cart.promoMinOrder'
            : null;
          this.promoError.set(key ? this.langService.t(key) : msg);
        }
      },
      error: () => {
        this.promoLoading.set(false);
        this.promoError.set(this.langService.t('cart.promoNetworkError'));
      },
    });
  }

  removePromoCode(): void {
    this.cart.clearDiscount();
    this.promoCodeInput.set('');
  }

  checkout(): void {
    if (!this.auth.isLoggedIn) {
      this.router.navigate(['/auth/login']);
      return;
    }
    const locationId = this.locationSvc.locationId();
    if (!locationId) {
      this.toast.warning(this.langService.t('cart.selectLocationFirst'));
      this.goToLocations();
      return;
    }
    if (!this.cart.items().length) {
      this.toast.warning(this.langService.t('cart.empty'));
      return;
    }

    if (!this.storeStatus.guardOrdering()) return;

    const paymentMethod = this.selectedPaymentMethod();
    if (!paymentMethod && !this.fullyCoveredByBonus()) {
      this.toast.warning(this.langService.t('cart.payRequired'));
      return;
    }

    this.loading.set(true);
    const items = this.cart
      .items()
      .map((i) => ({
        menuItemId: i.menuItemId,
        quantity: i.quantity,
        toppings: i.selectedToppings ?? [],
        notes: i.notes,
        variantId: i.variantId,
      }));

    const discountCode = this.cart.appliedDiscount()?.code;

    // Generate a unique key per checkout attempt; reuse across retries of the same attempt
    if (!this.checkoutKey) this.checkoutKey = crypto.randomUUID();

    const loyaltyPointsToSpend = this.appliedBonus() > 0 ? this.appliedBonus() : undefined;

    this.api.createOrder(
      { locationId, items, paymentMethod: paymentMethod ?? undefined, discountCode, loyaltyPointsToSpend },
      this.checkoutKey,
    ).subscribe({
      next: async (order) => {
        this.checkoutKey = null;
        this.cart.clear();

        // "Card" in the customer app means paying online now (card, Apple Pay or Google Pay on the provider's page).
        if (paymentMethod === PaymentMethod.Card && order.total > 0) {
          try {
            await this.online.open(order.id);
            return;
          } catch {
            // The order already exists (unpaid): the customer can try again from the order page or pay at the counter.
            this.toast.warning(this.langService.t('cart.payOnlineFailed'));
          }
        } else {
          this.toast.success(this.langService.t('group.placedToast'));
        }

        this.router.navigate(['/orders', order.id]);
      },
      // Keep checkoutKey on error so a retry reuses it and won't create a duplicate order
      error: () => this.loading.set(false),
    });
  }
}
