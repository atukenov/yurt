import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { SignalrService, YurtApiService } from 'shared-api';
import { Location, Order, OrderStatus } from 'shared-models';
import { environment } from '../../../environments/environment';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { SoundService } from '../../core/sound.service';

/**
 * Order-ready screen for a TV above the counter: big order numbers in two columns, "Preparing" and "Ready". The number is
 * the same short number the guest sees in the app (the last 6 characters of the order id). Open it on the TV's browser,
 * sign in once, and press Full screen. It follows the same live events as the kitchen screen, so a number jumps to
 * "Ready" the moment staff press Ready, and a soft sound plays.
 */
@Component({
  selector: 'app-order-ready-tv',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe],
  template: `
    <div class="flex h-screen flex-col overflow-hidden bg-slate-900 text-white">
      <div class="flex flex-shrink-0 items-center justify-between gap-3 px-8 py-4">
        <h1 class="text-3xl font-bold">{{ 'tv.title' | t }}</h1>
        <div class="flex items-center gap-3" [class.opacity-0]="chromeHidden()">
          <select
            [(ngModel)]="selectedLocationId"
            (ngModelChange)="load()"
            [attr.aria-label]="'orders.allLocations' | t"
            class="rounded-xl bg-slate-800 px-3 py-2 text-sm text-white outline-none focus:ring-2 focus:ring-brand-400"
          >
            <option value="">{{ 'orders.allLocations' | t }}</option>
            @for (loc of locations(); track loc.id) {
              <option [value]="loc.id">{{ loc.name }}</option>
            }
          </select>
          <button
            type="button"
            (click)="toggleFullscreen()"
            class="rounded-xl bg-slate-800 px-3 py-2 text-sm font-semibold hover:bg-slate-700"
          >
            {{ 'tv.fullscreen' | t }}
          </button>
        </div>
      </div>

      <div class="grid min-h-0 flex-1 grid-cols-2 gap-6 px-8 pb-8">
        <section class="flex min-h-0 flex-col rounded-3xl bg-slate-800 p-6" aria-labelledby="tv-preparing">
          <h2 id="tv-preparing" class="mb-4 text-3xl font-bold text-amber-300">{{ 'tv.preparing' | t }}</h2>
          <ul class="grid flex-1 content-start grid-cols-2 gap-4 overflow-hidden xl:grid-cols-3">
            @for (o of preparing(); track o.id) {
              <li class="rounded-2xl bg-slate-700 py-5 text-center text-5xl font-extrabold tabular-nums">
                {{ number(o) }}
              </li>
            }
          </ul>
        </section>

        <section class="flex min-h-0 flex-col rounded-3xl bg-emerald-700 p-6" aria-labelledby="tv-ready">
          <h2 id="tv-ready" class="mb-4 text-3xl font-bold">{{ 'tv.ready' | t }}</h2>
          <ul class="grid flex-1 content-start grid-cols-1 gap-4 overflow-hidden xl:grid-cols-2">
            @for (o of ready(); track o.id) {
              <li
                class="flex items-center justify-between rounded-2xl bg-white px-6 py-5 text-emerald-800 shadow-lg"
              >
                <span class="text-7xl font-extrabold tabular-nums">{{ number(o) }}</span>
                <span class="text-xl font-semibold">{{
                  (o.orderType === 'DineIn' ? 'tv.here' : 'tv.toGo') | t
                }}</span>
              </li>
            }
          </ul>
        </section>
      </div>
    </div>
  `,
})
export class OrderReadyTvComponent implements OnInit, OnDestroy {
  private api = inject(YurtApiService);
  private signalr = inject(SignalrService);
  private sound = inject(SoundService);

  private readonly LOC_KEY = 'yurt_tv_location';
  private subs: Subscription[] = [];
  private poll: ReturnType<typeof setInterval> | null = null;
  private hideTimer: ReturnType<typeof setTimeout> | null = null;

  orders = signal<Order[]>([]);
  locations = signal<Location[]>([]);
  chromeHidden = signal(false);
  selectedLocationId = '';

  /** Accepted or being made, oldest first, so the number that has waited longest is first. */
  preparing = computed(() =>
    this.orders()
      .filter((o) => o.status === OrderStatus.Accepted || o.status === OrderStatus.Preparing)
      .sort((a, b) => this.since(a).localeCompare(this.since(b))),
  );
  /** Ready to take, the one that became ready most recently first. */
  ready = computed(() =>
    this.orders()
      .filter((o) => o.status === OrderStatus.Ready)
      .sort((a, b) => this.since(b).localeCompare(this.since(a))),
  );

  ngOnInit(): void {
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
      );
    });
    // A TV runs for days: reload the list now and then in case a live event was missed while the network dropped.
    this.poll = setInterval(() => this.load(), 120_000);
    document.addEventListener('mousemove', this.showChrome);
    this.showChrome();
  }

  ngOnDestroy(): void {
    this.subs.forEach((s) => s.unsubscribe());
    if (this.poll) clearInterval(this.poll);
    if (this.hideTimer) clearTimeout(this.hideTimer);
    document.removeEventListener('mousemove', this.showChrome);
    this.signalr.stopConnection();
  }

  load(): void {
    try {
      localStorage.setItem(this.LOC_KEY, this.selectedLocationId);
    } catch {
      /* private mode: the choice just isn't remembered */
    }
    this.api.getAdminOrders(undefined, this.selectedLocationId || undefined).subscribe({
      next: (res) => {
        const items = Array.isArray(res) ? (res as unknown as Order[]) : (res.items ?? []);
        this.orders.set(items.filter((o) => this.shown(o)));
      },
    });
  }

  number(order: Order): string {
    return order.id.slice(-6).toUpperCase();
  }

  toggleFullscreen(): void {
    if (document.fullscreenElement) void document.exitFullscreen();
    else void document.documentElement.requestFullscreen?.();
  }

  /** The buttons fade out a few seconds after the mouse stops, so the TV shows only numbers. */
  private showChrome = (): void => {
    this.chromeHidden.set(false);
    if (this.hideTimer) clearTimeout(this.hideTimer);
    this.hideTimer = setTimeout(() => this.chromeHidden.set(true), 5000);
  };

  private since(order: Order): string {
    return order.acceptedAt ?? order.createdAt;
  }

  private shown(order: Order): boolean {
    if (this.selectedLocationId && order.locationId !== this.selectedLocationId) return false;
    return (
      order.status === OrderStatus.Accepted ||
      order.status === OrderStatus.Preparing ||
      order.status === OrderStatus.Ready
    );
  }

  private upsert(updated: Order): void {
    const known = this.orders().find((o) => o.id === updated.id);
    if (!this.shown(updated)) {
      if (known) this.orders.update((list) => list.filter((o) => o.id !== updated.id));
      return;
    }
    if (updated.status === OrderStatus.Ready && known?.status !== OrderStatus.Ready) void this.sound.playNewOrder();
    this.orders.update((list) =>
      known ? list.map((o) => (o.id === updated.id ? updated : o)) : [updated, ...list],
    );
  }
}
