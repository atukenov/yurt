import { Component, inject, OnInit, OnDestroy, signal, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService, SignalrService } from 'shared-api';
import { Order, OrderStatus, Location, IikoOrderSyncStatus } from 'shared-models';
import {
  BadgeComponent,
  ButtonComponent,
  ToastService,
  OrderStatusColorPipe,
  Currency2Pipe,
  BadgeVariant,
} from 'shared-ui';
import { environment } from '../../../environments/environment';
import { Subscription } from 'rxjs';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { SoundService } from '../../core/sound.service';

@Component({
  selector: 'app-orders-live',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    BadgeComponent,
    ButtonComponent,
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
  private toast = inject(ToastService);
  private sound = inject(SoundService);

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

  constructor() {
    effect(() => {
      const o = this.selectedOrder();
      if (o) this.paymentStatus = o.paymentStatus;
    });
  }

  onEtaPresetChange(val: string): void {
    if (val === 'custom') {
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
          this.toast.info(`New order #${o.id.slice(-6).toUpperCase()}`);
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
        const items = Array.isArray(res) ? (res as unknown as Order[]) : res.items ?? [];
        this.orders.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  acceptOrder(order: Order): void {
    this.actionLoading.set(true);
    this.api.acceptOrder(order.id, { etaMinutes: this.etaInput }).subscribe({
      next: (o) => {
        this.upsertOrder(o);
        this.actionLoading.set(false);
        this.toast.success('Order accepted');
      },
      error: () => this.actionLoading.set(false),
    });
  }

  declineOrder(order: Order): void {
    this.actionLoading.set(true);
    this.api.declineOrder(order.id, { reason: this.declineReason }).subscribe({
      next: (o) => {
        this.upsertOrder(o);
        this.actionLoading.set(false);
        this.showDecline.set(false);
        this.declineReason = '';
        this.toast.success('Order declined');
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
        this.toast.success(`Status: ${status}`);
      },
      error: () => this.actionLoading.set(false),
    });
  }

  savePayment(order: Order): void {
    this.paymentLoading.set(true);
    this.api
      .updateOrderPayment(order.id, {
        paymentStatus: this.paymentStatus as any,
      })
      .subscribe({
        next: (o) => {
          this.upsertOrder(o);
          this.paymentLoading.set(false);
          this.toast.success('Payment updated');
        },
        error: () => this.paymentLoading.set(false),
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
        return { label: 'Synced to iiko', variant: 'teal' };
      case IikoOrderSyncStatus.SkippedUnmapped:
        return { label: 'Not mapped — check menu settings', variant: 'slate' };
      case IikoOrderSyncStatus.Failed:
        return { label: 'iiko sync failed', variant: 'red' };
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
