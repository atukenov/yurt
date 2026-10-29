import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { SignalrService, YurtApiService } from 'shared-api';
import { Location, Order, OrderItem, OrderStatus, Station, StationRouting } from 'shared-models';
import { LocalDatePipe, ToastService } from 'shared-ui';
import { environment } from '../../../environments/environment';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { SoundService } from '../../core/sound.service';
import { ReceiptService } from '../../core/receipt.service';
import { AdminLangService } from '../../core/lang.service';

interface KitchenColumn {
  status: OrderStatus;
  titleKey: string;
  actionKey: string;
  next: OrderStatus;
}

/**
 * Kitchen screen for a tablet (Phase 6, `own_pos`): the accepted orders of one branch as big cards, moved along with
 * one tap. Uses the same order API and live events as the Live Orders page, so staff on both screens see the same thing.
 */
@Component({
  selector: 'app-kitchen-display',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe, LocalDatePipe],
  templateUrl: './kitchen-display.component.html',
})
export class KitchenDisplayComponent implements OnInit, OnDestroy {
  readonly signalr = inject(SignalrService);
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private sound = inject(SoundService);
  private receipts = inject(ReceiptService);
  private lang = inject(AdminLangService);

  private readonly LOC_KEY = 'yurt_kitchen_location';
  private readonly STATION_KEY = 'yurt_kitchen_station';
  private subs: Subscription[] = [];
  private clock: ReturnType<typeof setInterval> | null = null;

  readonly Status = OrderStatus;

  readonly columns: KitchenColumn[] = [
    {
      status: OrderStatus.Accepted,
      titleKey: 'kitchen.colNew',
      actionKey: 'kitchen.start',
      next: OrderStatus.Preparing,
    },
    {
      status: OrderStatus.Preparing,
      titleKey: 'kitchen.colPreparing',
      actionKey: 'kitchen.ready',
      next: OrderStatus.Ready,
    },
    {
      status: OrderStatus.Ready,
      titleKey: 'kitchen.colReady',
      actionKey: 'kitchen.handedOver',
      next: OrderStatus.Completed,
    },
  ];

  orders = signal<Order[]>([]);
  locations = signal<Location[]>([]);
  loading = signal(true);
  busyId = signal<string | null>(null);
  now = signal(Date.now());
  selectedLocationId = '';

  routing = signal<StationRouting>({ stations: [], categories: [], itemStations: {} });
  /** The station this screen shows ('' = everything). Items of a category with no station show on every screen. */
  stationFilter = signal('');
  showSetup = signal(false);
  newStation = '';

  /** Orders for the kitchen only: accepted and onwards, oldest first, so the longest wait is on top. */
  byColumn = computed(() => {
    const map = new Map<OrderStatus, Order[]>();
    for (const c of this.columns) map.set(c.status, []);
    for (const o of this.orders())
      if (this.itemsOf(o).length) map.get(o.status as OrderStatus)?.push(o);
    for (const list of map.values())
      list.sort((a, b) => this.since(a).localeCompare(this.since(b)));
    return map;
  });

  ngOnInit(): void {
    const savedStation = localStorage.getItem(this.STATION_KEY) ?? '';
    this.stationFilter.set(savedStation);
    this.loadRouting();
    this.api.getAdminLocations().subscribe((locs) => {
      this.locations.set(locs);
      const saved = localStorage.getItem(this.LOC_KEY) ?? '';
      if (saved && locs.some((l) => l.id === saved)) this.selectedLocationId = saved;
      else if (locs.length === 1) this.selectedLocationId = locs[0].id;
      this.load();
    });

    this.signalr.configure(environment.apiUrl);
    this.signalr.startConnection().then(() => {
      this.subs.push(
        this.signalr.orderCreated$.subscribe((o) => this.upsert(o)),
        this.signalr.orderUpdated$.subscribe((o) => this.upsert(o)),
        this.signalr.orderDeclined$.subscribe((o) => this.upsert(o)),
        this.signalr.paymentUpdated$.subscribe((o) => this.upsert(o)),
      );
    });
    this.clock = setInterval(() => this.now.set(Date.now()), 30_000);
  }

  ngOnDestroy(): void {
    this.subs.forEach((s) => s.unsubscribe());
    if (this.clock) clearInterval(this.clock);
    this.signalr.stopConnection();
  }

  load(): void {
    localStorage.setItem(this.LOC_KEY, this.selectedLocationId);
    this.loading.set(true);
    this.api.getAdminOrders(undefined, this.selectedLocationId || undefined).subscribe({
      next: (res) => {
        const items = Array.isArray(res) ? (res as unknown as Order[]) : (res.items ?? []);
        this.orders.set(items.filter((o) => this.inKitchen(o)));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  /** The items of an order this screen should show: all of them, or those made at the chosen station. */
  itemsOf(order: Order): OrderItem[] {
    const station = this.stationFilter();
    if (!station) return order.items;
    const map = this.routing().itemStations;
    return order.items.filter((i) => !map[i.menuItemId] || map[i.menuItemId] === station);
  }

  /** The stations that have items in this order, in screen order. Items with no station belong to none. */
  stationsOf(order: Order): Station[] {
    const map = this.routing().itemStations;
    const ids = new Set(order.items.map((i) => map[i.menuItemId]).filter(Boolean));
    return this.routing().stations.filter((s) => ids.has(s.id));
  }

  isPartReady(order: Order, stationId: string): boolean {
    return order.readyStationIds?.includes(stationId) ?? false;
  }

  /**
   * True when this screen is one station and the order has items made here: the screen then marks only its own part
   * ready, and the order becomes Ready once every station's part is.
   */
  ownsPart(order: Order): boolean {
    const station = this.stationFilter();
    return !!station && this.stationsOf(order).some((s) => s.id === station);
  }

  togglePart(order: Order): void {
    const station = this.stationFilter();
    this.busyId.set(order.id);
    this.api.setStationReady(order.id, station, !this.isPartReady(order, station)).subscribe({
      next: (o) => {
        this.upsert(o);
        this.busyId.set(null);
      },
      error: () => {
        this.busyId.set(null);
        this.toast.error(this.lang.t('common.orderUpdateFailed'));
      },
    });
  }

  /**
   * Sends the ticket to the branch's network printers (this station's, or every kitchen printer on the All tab). With
   * no printer set up, or if the server cannot be asked, it prints through the browser as before.
   */
  printTicket(order: Order): void {
    const station = this.routing().stations.find((s) => s.id === this.stationFilter());
    const browserPrint = () => this.receipts.printTicket(order, this.itemsOf(order), station?.name);
    this.api.printOrderTicket(order.id, station?.id).subscribe({
      next: (r) => (r.printers > 0 ? this.toast.success(this.lang.t('printers.sent')) : browserPrint()),
      error: () => browserPrint(),
    });
  }

  pickStation(id: string): void {
    this.stationFilter.set(id);
    localStorage.setItem(this.STATION_KEY, id);
  }

  loadRouting(): void {
    this.api.getStationRouting().subscribe((r) => {
      this.routing.set(r);
      // A station that was deleted since this screen last ran falls back to showing everything.
      if (this.stationFilter() && !r.stations.some((s) => s.id === this.stationFilter()))
        this.pickStation('');
    });
  }

  addStation(): void {
    const name = this.newStation.trim();
    if (!name) return;
    this.api.createStation(name).subscribe({
      next: () => {
        this.newStation = '';
        this.loadRouting();
      },
      error: () => this.toast.error(this.lang.t('common.addFailed')),
    });
  }

  removeStation(id: string): void {
    if (!confirm(this.lang.t('kitchen.confirmDeleteStation'))) return;
    this.api.deleteStation(id).subscribe({ next: () => this.loadRouting() });
  }

  routeCategory(categoryId: string, stationId: string): void {
    this.api.setCategoryStation(categoryId, stationId || null).subscribe({
      next: () => this.loadRouting(),
      error: () => this.toast.error(this.lang.t('common.saveFailed')),
    });
  }

  move(order: Order, column: KitchenColumn): void {
    this.busyId.set(order.id);
    this.api.updateOrderStatus(order.id, { status: column.next }).subscribe({
      next: (o) => {
        this.upsert(o);
        this.busyId.set(null);
      },
      error: () => {
        this.busyId.set(null);
        this.toast.error(this.lang.t('common.orderUpdateFailed'));
      },
    });
  }

  shortId(order: Order): string {
    return order.id.slice(-6).toUpperCase();
  }

  /** Minutes the order has waited since it was accepted (or created, if the time is missing). */
  waitMinutes(order: Order): number {
    return Math.max(0, Math.floor((this.now() - new Date(this.since(order)).getTime()) / 60_000));
  }

  isLate(order: Order): boolean {
    return order.etaMinutes != null && this.waitMinutes(order) > order.etaMinutes;
  }

  private since(order: Order): string {
    return order.acceptedAt ?? order.createdAt;
  }

  private inKitchen(order: Order): boolean {
    if (this.selectedLocationId && order.locationId !== this.selectedLocationId) return false;
    return this.columns.some((c) => c.status === order.status);
  }

  private upsert(updated: Order): void {
    const known = this.orders().some((o) => o.id === updated.id);
    const wanted = this.inKitchen(updated);
    if (!wanted) {
      if (known) this.orders.update((list) => list.filter((o) => o.id !== updated.id));
      return;
    }
    if (!known && updated.status === OrderStatus.Accepted) void this.sound.playNewOrder();
    this.orders.update((list) =>
      known ? list.map((o) => (o.id === updated.id ? updated : o)) : [updated, ...list],
    );
  }
}
