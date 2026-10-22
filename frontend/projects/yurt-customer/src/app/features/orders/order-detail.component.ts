import { CommonModule } from '@angular/common';
import { HttpContext } from '@angular/common/http';
import { Component, inject, Input, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { BrandService, ModuleService, SignalrService, SKIP_ERROR_TOAST, YurtApiService } from 'shared-api';
import {
  Order,
  OrderStatus,
  PaymentMethod,
  PaymentStatus,
} from 'shared-models';
import { Currency2Pipe, OrderStatusLabelPipe, ToastService, LocalDatePipe } from 'shared-ui';
import { environment } from '../../../environments/environment';
import { LangService } from '../../core/lang.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { PullToRefreshDirective } from '../../shared/pull-to-refresh.directive';

@Component({
  selector: 'app-order-detail',
  standalone: true,
  imports: [LocalDatePipe, 
    CommonModule,
    FormsModule,
    OrderStatusLabelPipe,
    Currency2Pipe,
    TranslatePipe,
    PullToRefreshDirective,
  ],
  templateUrl: './order-detail.component.html',
  styleUrl: './order-detail.component.css',
})
export class OrderDetailComponent implements OnInit, OnDestroy {
  @Input() id!: string;

  readonly router = inject(Router);
  private api = inject(YurtApiService);
  readonly modules = inject(ModuleService);
  readonly brand = inject(BrandService);
  private signalr = inject(SignalrService);
  private toast = inject(ToastService);
  private lang = inject(LangService);
  readonly OrderStatus = OrderStatus;
  readonly PaymentStatus = PaymentStatus;
  readonly PaymentMethod = PaymentMethod;

  order = signal<Order | null>(null);
  loading = signal(true);
  showReceipt = signal(false);
  private subs: Subscription[] = [];

  readonly stars = [1, 2, 3, 4, 5];
  hoverRating = signal(0);
  selectedRating = signal(0);
  ratingComment = signal('');
  submittingRating = signal(false);
  cancelling = signal(false);

  readonly timelineSteps = [
    { status: OrderStatus.Created, labelKey: 'timeline.Created' },
    { status: OrderStatus.Accepted, labelKey: 'timeline.Accepted' },
    { status: OrderStatus.Preparing, labelKey: 'timeline.Preparing' },
    { status: OrderStatus.Ready, labelKey: 'timeline.Ready' },
    { status: OrderStatus.Completed, labelKey: 'timeline.Completed' },
  ];

  readonly statusOrder = [
    OrderStatus.Created,
    OrderStatus.Accepted,
    OrderStatus.Preparing,
    OrderStatus.Ready,
    OrderStatus.Completed,
  ];

  ngOnInit(): void {
    this.loadOrder();

    this.signalr.configure(environment.apiUrl);
  }

  loadOrder(): void {
    // Skips the generic HTTP error toast — this page shows a more specific
    // "order not found" message instead of layering both on top of each other.
    const context = new HttpContext().set(SKIP_ERROR_TOAST, true);
    this.api.getOrder(this.id, context).subscribe({
      next: (o) => {
        this.order.set(o);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toast.error(this.lang.t('order.notFound'));
      },
    });
    this.signalr.startConnection().then(() => {
      this.subs.push(
        this.signalr.orderUpdated$.subscribe((o) => {
          if (o.id === this.id) this.setLive(o);
        }),
        this.signalr.orderDeclined$.subscribe((o) => {
          if (o.id === this.id) this.setLive(o);
        }),
        this.signalr.paymentUpdated$.subscribe((o) => {
          if (o.id === this.id) this.setLive(o);
        }),
      );
    });
  }

  /**
   * Live updates do not carry the tax receipt links, so the ones already shown are kept. Until the receipt is there,
   * the order is fetched again on each update to pick it up once the cafe's cash register sent it.
   */
  private setLive(o: Order): void {
    const prev = this.order();
    const merged = {
      ...o,
      fiscalReceiptUrl: o.fiscalReceiptUrl ?? prev?.fiscalReceiptUrl,
      fiscalReturnUrl: o.fiscalReturnUrl ?? prev?.fiscalReturnUrl,
    };
    this.order.set(merged);
    if (!merged.fiscalReceiptUrl && o.paymentStatus === PaymentStatus.Paid && o.status !== OrderStatus.Created && o.status !== OrderStatus.Declined) {
      const context = new HttpContext().set(SKIP_ERROR_TOAST, true);
      this.api.getOrder(this.id, context).subscribe({
        next: (fresh) => {
          if (fresh.fiscalReceiptUrl || fresh.fiscalReturnUrl) this.setLive(fresh);
        },
        error: () => {},
      });
    }
  }

  cancelOrder(): void {
    if (this.cancelling()) return;
    if (!confirm(this.lang.t('order.cancelConfirm'))) return;
    this.cancelling.set(true);
    this.api.cancelOrder(this.id).subscribe({
      next: (o) => {
        this.order.set(o);
        this.cancelling.set(false);
        this.toast.success(this.lang.t('order.cancelled'));
      },
      error: () => {
        // e.g. the order was accepted in the meantime — refresh to show its real state
        this.cancelling.set(false);
        this.loadOrder();
      },
    });
  }

  ngOnDestroy(): void {
    this.subs.forEach((s) => s.unsubscribe());
  }

  isActive(status: OrderStatus): boolean {
    return [
      OrderStatus.Created,
      OrderStatus.Accepted,
      OrderStatus.Preparing,
      OrderStatus.Ready,
    ].includes(status);
  }

  stepIsReached(current: OrderStatus, step: OrderStatus): boolean {
    return this.statusOrder.indexOf(current) >= this.statusOrder.indexOf(step);
  }

  stepDotClass(current: OrderStatus, step: OrderStatus): string {
    return this.stepIsReached(current, step) ? 'bg-brand-500' : 'bg-stone-200';
  }

  statusEmoji(status: OrderStatus): string {
    const map: Record<OrderStatus, string> = {
      [OrderStatus.Created]: '📋',
      [OrderStatus.Accepted]: '✅',
      [OrderStatus.Preparing]: '☕',
      [OrderStatus.Ready]: '🎉',
      [OrderStatus.Completed]: '🌟',
      [OrderStatus.Declined]: '❌',
    };
    return map[status] ?? '📋';
  }

  // ── Presentational helpers (design-only) ────────────────────────────────────

  get ringPercent(): number {
    const o = this.order();
    if (!o) return 0;
    const map: Partial<Record<OrderStatus, number>> = {
      [OrderStatus.Created]: 5,
      [OrderStatus.Accepted]: 10,
      [OrderStatus.Preparing]: 45,
      [OrderStatus.Ready]: 85,
      [OrderStatus.Completed]: 100,
      [OrderStatus.Declined]: 0,
    };
    return map[o.status] ?? 0;
  }

  get displaySteps(): { status: OrderStatus; labelKey: string }[] {
    return [
      { status: OrderStatus.Accepted,  labelKey: 'timeline.s.Accepted'  },
      { status: OrderStatus.Preparing, labelKey: 'timeline.s.Preparing' },
      { status: OrderStatus.Ready,     labelKey: 'timeline.s.Ready'     },
      { status: OrderStatus.Completed, labelKey: 'timeline.s.Completed' },
    ];
  }

  toppingList(toppings: { toppingName: string }[]): string {
    return toppings.map(t => t.toppingName).join(', ');
  }

  submitRating(): void {
    const rating = this.selectedRating();
    if (rating < 1 || rating > 5 || this.submittingRating()) return;

    this.submittingRating.set(true);
    this.api.rateOrder(this.id, rating, this.ratingComment().trim() || undefined).subscribe({
      next: (o) => {
        this.order.set(o);
        this.submittingRating.set(false);
        this.toast.success(this.lang.t('rating.thanks'));
      },
      error: () => this.submittingRating.set(false),
    });
  }
}
