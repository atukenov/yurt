import { Component, inject, OnInit, OnDestroy, signal, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ModuleService, YurtApiService, SignalrService } from 'shared-api';
import {
  FiscalReceipt,
  Order,
  OrderStatus,
  Location,
  IikoOrderSyncStatus,
  PaymentStatus,
  SplitPaymentLine,
} from 'shared-models';
import { SplitPaymentComponent } from './split-payment.component';
import {
  BadgeComponent,
  ButtonComponent,
  ManagerApproval,
  ManagerApprovalComponent,
  ToastService,
  OrderStatusColorPipe,
  Currency2Pipe,
  BadgeVariant,
  LocalDatePipe,
} from 'shared-ui';
import { environment } from '../../../environments/environment';
import { Subscription, catchError, forkJoin, map, of } from 'rxjs';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { SoundService } from '../../core/sound.service';
import { ReceiptService } from '../../core/receipt.service';
import { AdminLangService } from '../../core/lang.service';

@Component({
  selector: 'app-orders-live',
  standalone: true,
  imports: [LocalDatePipe, 
    SplitPaymentComponent,
    CommonModule,
    FormsModule,
    BadgeComponent,
    ButtonComponent,
    ManagerApprovalComponent,
    OrderStatusColorPipe,
    Currency2Pipe,
    AdminTranslatePipe,
  ],
  templateUrl: './orders-live.component.html',
  styleUrl: './orders-live.component.css',
})
export class OrdersLiveComponent implements OnInit, OnDestroy {
  readonly signalr = inject(SignalrService);
  private api = inject(YurtApiService);
  readonly modules = inject(ModuleService);
  private toast = inject(ToastService);
  private sound = inject(SoundService);
  private receipts = inject(ReceiptService);
  private lang = inject(AdminLangService);

  printReceipt(order: Order): void {
    if (!this.modules.isEnabled('own_pos')) return this.receipts.print(order);
    // A receipt printer at the branch prints it directly; without one, the browser prints it as before.
    this.api.printOrderReceipt(order.id).subscribe({
      next: (r) => (r.printers > 0 ? this.toast.success(this.lang.t('printers.sent')) : this.browserReceipt(order)),
      error: () => this.browserReceipt(order),
    });
  }

  private browserReceipt(order: Order): void {
    // Parts of a split payment and the tax receipt QR code go on the receipt; if either cannot be loaded the receipt
    // still prints without it.
    forkJoin({
      lines: this.api.getSplitPayment(order.id).pipe(
        map((p) => p.lines),
        catchError(() => of<SplitPaymentLine[]>([])),
      ),
      fiscal: this.api.getOrderFiscalReceipts(order.id).pipe(
        map((list) => list.find((r) => r.kind === 'Sale' && r.status === 'Sent') ?? null),
        catchError(() => of(null)),
      ),
    }).subscribe(({ lines, fiscal }) => this.receipts.print(order, lines, fiscal));
  }

  readonly OrderStatus = OrderStatus;

  orders = signal<Order[]>([]);
  locations = signal<Location[]>([]);
  loading = signal(true);
  selectedOrder = signal<Order | null>(null);
  activeTab = signal<string>('active');
  selectedLocationId = '';
  searchQuery = '';
  private searchDebounce: ReturnType<typeof setTimeout> | null = null;
  private readonly LOC_KEY = 'yurt_orders_location';

  etaInput = 10;
  etaPresetValue = '10';
  useCustomEta = false;
  declineReason = '';
  paymentStatus = 'Unpaid';

  actionLoading = signal(false);
  paymentLoading = signal(false);
  showDecline = signal(false);

  /** Ids of orders whose official tax receipt is not sent yet ("not fiscalized"); empty while fiscal receipts are off. */
  fiscalWaiting = signal<Set<string>>(new Set());
  /** Tax receipts of the order on screen. */
  orderFiscal = signal<FiscalReceipt[]>([]);
  private fiscalOrderId: string | null = null;

  constructor() {
    effect(() => {
      const o = this.selectedOrder();
      if (o) this.paymentStatus = o.paymentStatus;
      // An order for a set time is ready by that time unless staff pick another ETA.
      if (o?.status === OrderStatus.Created && o.pickupAt && o.id !== this.etaOrderId) {
        this.etaOrderId = o.id;
        this.etaPresetValue = 'pickup';
        this.useCustomEta = false;
        this.etaInput = this.minutesUntilPickup(o);
      }
      if ((o?.id ?? null) !== this.fiscalOrderId) {
        this.fiscalOrderId = o?.id ?? null;
        this.loadOrderFiscal();
      }
    });
  }

  private loadFiscalWaiting(): void {
    if (!this.modules.isEnabled('own_pos')) return;
    this.api.getWaitingFiscalReceipts(this.selectedLocationId || undefined).subscribe({
      next: (list) => this.fiscalWaiting.set(new Set(list.map((r) => r.orderId))),
      error: () => this.fiscalWaiting.set(new Set()),
    });
  }

  private loadOrderFiscal(): void {
    this.orderFiscal.set([]);
    const id = this.fiscalOrderId;
    if (!id || !this.modules.isEnabled('own_pos')) return;
    this.api.getOrderFiscalReceipts(id).subscribe({
      next: (list) => { if (this.fiscalOrderId === id) this.orderFiscal.set(list); },
      error: () => {},
    });
  }

  retryFiscal(r: FiscalReceipt): void {
    this.api.retryFiscalReceipt(r.id).subscribe({
      next: (saved) => {
        if (saved.status === 'Pending') this.toast.error(saved.lastError || this.lang.t('common.notSent'));
        this.loadOrderFiscal();
        this.loadFiscalWaiting();
      },
    });
  }

  private etaOrderId: string | null = null;

  /** Whole minutes from now until the guest's pickup time, at least 1. */
  minutesUntilPickup(order: Order): number {
    if (!order.pickupAt) return this.etaInput;
    return Math.max(1, Math.ceil((new Date(order.pickupAt).getTime() - Date.now()) / 60_000));
  }

  onEtaPresetChange(val: string): void {
    if (val === 'pickup') {
      this.useCustomEta = false;
      const o = this.selectedOrder();
      if (o) this.etaInput = this.minutesUntilPickup(o);
    } else if (val === 'custom') {
      this.useCustomEta = true;
    } else {
      this.useCustomEta = false;
      this.etaInput = +val;
    }
  }

  readonly statusTabs = [
    { labelKey: 'orders.tabActive', value: 'active' },
    { labelKey: 'orders.tabDone', value: 'done' },
    { labelKey: 'orders.tabAll', value: 'all' },
  ];

  private activeStatuses = [
    OrderStatus.Created,
    OrderStatus.Accepted,
    OrderStatus.Preparing,
    OrderStatus.Ready,
  ];

  filteredOrders = computed(() => {
    const tab = this.activeTab();
    return this.orders().filter((o) => {
      if (tab === 'active') return this.activeStatuses.includes(o.status as OrderStatus);
      if (tab === 'done')
        return o.status === OrderStatus.Completed || o.status === OrderStatus.Declined;
      return true;
    });
  });

  totalQty(order: Order): number {
    return order.items.reduce((s, i) => s + i.quantity, 0);
  }

  countForStatus(tab: string): number {
    if (tab === 'active')
      return this.orders().filter((o) => this.activeStatuses.includes(o.status as OrderStatus))
        .length;
    return 0;
  }

  private subs: Subscription[] = [];

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe((locs) => {
      this.locations.set(locs);
      const saved = localStorage.getItem(this.LOC_KEY) ?? '';
      if (saved && locs.some((l) => l.id === saved)) {
        this.selectedLocationId = saved;
      }
      this.loadOrders();
    });

    this.signalr.configure(environment.apiUrl);
    this.signalr.startConnection().then(() => {
      this.subs.push(
        this.signalr.orderCreated$.subscribe((o) => {
          this.orders.update((list) => [o, ...list]);
          this.toast.info(this.lang.t('orders.newToast', { no: o.number || o.id.slice(-6).toUpperCase() }));
          void this.sound.playNewOrder();
        }),
        this.signalr.orderUpdated$.subscribe((o) => this.upsertOrder(o)),
        this.signalr.orderDeclined$.subscribe((o) => this.upsertOrder(o)),
        this.signalr.paymentUpdated$.subscribe((o) => this.upsertOrder(o)),
      );
    });
  }

  ngOnDestroy(): void {
    this.subs.forEach((s) => s.unsubscribe());
    this.signalr.stopConnection();
  }

  onSearchChange(): void {
    if (this.searchDebounce) clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(() => this.loadOrders(), 300);
  }

  loadOrders(): void {
    localStorage.setItem(this.LOC_KEY, this.selectedLocationId);
    this.loading.set(true);
    const search = this.searchQuery.trim() || undefined;
    this.api.getAdminOrders(undefined, this.selectedLocationId || undefined, search).subscribe({
      next: (res) => {
        // Guard: old backend returns Order[] directly; new backend returns { items: Order[] }
        const items = Array.isArray(res) ? (res as unknown as Order[]) : (res.items ?? []);
        this.orders.set(items);
        this.loading.set(false);
        this.loadFiscalWaiting();
      },
      error: () => this.loading.set(false),
    });
  }

  /**
   * Accepts an order. With own stock on, first checks whether the order needs more of something than the branch has
   * left, and asks before taking the count below zero (accepting anyway stops it at zero and logs what was missing).
   */
  acceptOrder(order: Order): void {
    this.actionLoading.set(true);
    if (!this.modules.isEnabled('own_pos')) return this.doAccept(order);
    this.api
      .getOrderStockCheck(order.id)
      .pipe(catchError(() => of([])))
      .subscribe((shorts) => {
        if (shorts.length) {
          const lines = shorts
            .map((s) =>
              this.lang.t('orders.stockShortLine', {
                name: s.name,
                need: `${s.need}${s.unit ? ' ' + s.unit : ''}`,
                left: `${s.left}${s.unit ? ' ' + s.unit : ''}`,
              }),
            )
            .join('\n');
          if (!confirm(`${this.lang.t('orders.stockShort')}\n\n${lines}\n\n${this.lang.t('orders.stockShortAsk')}`)) {
            this.actionLoading.set(false);
            return;
          }
        }
        this.doAccept(order);
      });
  }

  private doAccept(order: Order): void {
    if (this.etaPresetValue === 'pickup' && order.pickupAt) this.etaInput = this.minutesUntilPickup(order);
    this.api.acceptOrder(order.id, { etaMinutes: this.etaInput }).subscribe({
      next: (o) => {
        this.upsertOrder(o);
        this.actionLoading.set(false);
        this.toast.success(this.lang.t('orders.acceptedToast'));
      },
      error: () => this.actionLoading.set(false),
    });
  }

  /** Declining an order the guest already paid online gives money back, so it asks for a manager PIN (own_pos). */
  startDecline(order: Order): void {
    if (this.modules.isEnabled('own_pos') && order.paymentStatus === 'Paid') this.approval.set({ kind: 'decline', order });
    else this.showDecline.set(true);
  }

  declineOrder(order: Order, approved?: ManagerApproval): void {
    this.actionLoading.set(true);
    const reason = approved?.reason ?? this.declineReason;
    this.api.declineOrder(order.id, { reason, pin: approved?.pin }).subscribe({
      next: (o) => {
        this.upsertOrder(o);
        this.actionLoading.set(false);
        this.showDecline.set(false);
        this.approval.set(null);
        this.declineReason = '';
        this.toast.success(this.lang.t('orders.declinedToast'));
      },
      error: () => this.actionLoading.set(false),
    });
  }

  updateStatus(order: Order, status: OrderStatus): void {
    this.actionLoading.set(true);
    this.api.updateOrderStatus(order.id, { status }).subscribe({
      next: (o) => {
        this.upsertOrder(o);
        this.actionLoading.set(false);
        this.toast.success(this.lang.t('orders.statusToast', { status: this.lang.t(`order.status.${status}`) }));
      },
      error: () => this.actionLoading.set(false),
    });
  }

  savePayment(order: Order, approved?: ManagerApproval): void {
    // Taking back "Paid" is how money leaves the till without a sale: it asks for a manager PIN and a reason (own_pos).
    if (!approved && this.modules.isEnabled('own_pos') && order.paymentStatus === 'Paid' && this.paymentStatus !== 'Paid') {
      this.approval.set({ kind: 'undoPaid', order, status: this.paymentStatus });
      return;
    }
    this.paymentLoading.set(true);
    this.api
      .updateOrderPayment(order.id, {
        paymentStatus: this.paymentStatus as any,
        pin: approved?.pin,
        reason: approved?.reason,
      })
      .subscribe({
        next: (o) => {
          this.upsertOrder(o);
          this.approval.set(null);
          this.paymentLoading.set(false);
          this.toast.success(this.lang.t('orders.paymentUpdated'));
        },
        error: () => { this.paymentLoading.set(false); this.paymentStatus = order.paymentStatus; },
      });
  }

  refundLoading = signal(false);

  /** Gives an online payment back to the customer's card in full (the server refuses if it was not paid online). */
  refundOnline(order: Order, approved?: ManagerApproval): void {
    if (this.modules.isEnabled('own_pos')) {
      if (!approved) { this.approval.set({ kind: 'refund', order }); return; }
    } else if (!confirm(this.lang.t('orders.refundConfirm'))) return;
    this.refundLoading.set(true);
    this.api.refundOrder(order.id, approved ?? { pin: '', reason: '' }).subscribe({
      next: (o) => {
        this.upsertOrder(o);
        this.paymentStatus = o.paymentStatus;
        this.refundLoading.set(false);
        this.approval.set(null);
        this.toast.success(this.lang.t('orders.refundDone'));
      },
      error: () => this.refundLoading.set(false),
    });
  }

  // ── Manager PIN (own_pos): cancels, refunds and discounts ──────────────────

  /** The action waiting for a manager's PIN and reason; null = no dialog. */
  approval = signal<{ kind: 'refund' | 'decline' | 'cancel' | 'discount' | 'undoPaid' | 'tip'; order: Order; status?: string } | null>(null);
  discountMode: 'percent' | 'amount' = 'percent';
  discountValue: number | null = null;

  /** Taken on (or a finished counter sale) and not yet cancelled. */
  canCancel(o: Order): boolean {
    if (!this.modules.isEnabled('own_pos')) return false;
    return [OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Ready].includes(o.status)
      || (o.status === OrderStatus.Completed && o.source === 'Counter');
  }

  /** One discount per order; an order paid online can't be lowered here. */
  canDiscount(o: Order): boolean {
    if (!this.modules.isEnabled('own_pos')) return false;
    if (o.status === OrderStatus.Completed || o.status === OrderStatus.Declined || o.discountAmount > 0) return false;
    return o.source === 'Counter' || o.paymentStatus !== 'Paid';
  }

  /** A tip can be set, changed or removed on any order that is not cancelled. */
  canTip(o: Order): boolean {
    return this.modules.isEnabled('own_pos') && o.status !== OrderStatus.Declined;
  }

  tipValue: number | null = null;

  openApproval(kind: 'cancel' | 'discount' | 'tip', order: Order): void {
    this.discountMode = 'percent';
    this.discountValue = null;
    this.tipValue = order.tipAmount || null;
    this.approval.set({ kind, order });
  }

  discountReady(): boolean {
    const v = this.discountValue ?? 0;
    return v > 0 && (this.discountMode === 'amount' || v <= 100);
  }

  approvalReady(): boolean {
    const kind = this.approval()?.kind;
    if (kind === 'tip') return this.tipValue !== null && this.tipValue >= 0 && this.tipValue !== (this.approval()!.order.tipAmount || 0);
    return kind !== 'discount' || this.discountReady();
  }

  approve(approved: ManagerApproval): void {
    const a = this.approval();
    if (!a) return;
    switch (a.kind) {
      case 'refund': return this.refundOnline(a.order, approved);
      case 'decline': return this.declineOrder(a.order, approved);
      case 'undoPaid': return this.savePayment(a.order, approved);
      case 'cancel':
        this.actionLoading.set(true);
        return this.afterApproval(this.api.voidOrder(a.order.id, approved), 'orders.cancelledToast');
      case 'tip':
        this.actionLoading.set(true);
        return this.afterApproval(this.api.setOrderTip(a.order.id, { ...approved, amount: this.tipValue ?? 0 }), 'orders.tipToast');
      case 'discount':
        this.actionLoading.set(true);
        return this.afterApproval(
          this.api.discountOrder(a.order.id, {
            ...approved,
            percent: this.discountMode === 'percent' ? this.discountValue : null,
            amount: this.discountMode === 'amount' ? this.discountValue : null,
          }),
          'orders.discountedToast',
        );
    }
  }

  private afterApproval(call: ReturnType<YurtApiService['voidOrder']>, toastKey: string): void {
    call.subscribe({
      next: (o) => {
        this.upsertOrder(o);
        this.paymentStatus = o.paymentStatus;
        this.actionLoading.set(false);
        this.approval.set(null);
        this.toast.success(this.lang.t(toastKey));
      },
      error: () => this.actionLoading.set(false),
    });
  }

  closeApproval(): void {
    // Leaving the "undo paid" dialog puts the payment buttons back where they were.
    const a = this.approval();
    if (a?.kind === 'undoPaid') this.paymentStatus = a.order.paymentStatus;
    this.approval.set(null);
  }

  approvalTitle(): string {
    return this.lang.t(`approval.${this.approval()?.kind ?? 'cancel'}.title`);
  }

  /** A split payment marks the order paid and keeps the biggest part as its main method. */
  onSplitSaved(order: Order, lines: SplitPaymentLine[]): void {
    const main = [...lines].sort((a, b) => b.amount - a.amount)[0];
    this.upsertOrder({
      ...order,
      paymentStatus: PaymentStatus.Paid,
      paymentMethod: (main?.method ?? order.paymentMethod) as Order['paymentMethod'],
    });
  }

  canAccept(order: Order): boolean {
    return order.paymentStatus === 'Paid';
  }

  formatPhone(phone: string): string {
    const d = phone.replace(/\D/g, '');
    if (d.length === 11 && d[0] === '7')
      return `+7 ${d.slice(1, 4)} ${d.slice(4, 7)} ${d.slice(7)}`;
    return phone;
  }

  // iiko order-push status badge — hidden for NotPushed (the default while the feature
  // is off, or before an accepted order's push attempt runs) since that's the vast
  // majority of orders and not worth surfacing.
  iikoSyncBadge(status?: IikoOrderSyncStatus): { label: string; variant: BadgeVariant } | null {
    switch (status) {
      case IikoOrderSyncStatus.Pushed:
      case IikoOrderSyncStatus.Closed:
        return { label: this.lang.t('orders.iiko.synced'), variant: 'teal' };
      case IikoOrderSyncStatus.SkippedUnmapped:
        return { label: this.lang.t('orders.iiko.unmapped'), variant: 'slate' };
      case IikoOrderSyncStatus.Failed:
        return { label: this.lang.t('orders.iiko.failed'), variant: 'red' };
      default:
        return null;
    }
  }

  private upsertOrder(updated: Order): void {
    this.orders.update((list) => {
      const idx = list.findIndex((o) => o.id === updated.id);
      return idx >= 0 ? list.map((o) => (o.id === updated.id ? updated : o)) : [updated, ...list];
    });
    if (this.selectedOrder()?.id === updated.id) {
      this.selectedOrder.set(updated);
      this.paymentStatus = updated.paymentStatus;
    }
  }
}
