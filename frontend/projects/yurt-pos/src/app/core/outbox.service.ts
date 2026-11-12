import { DestroyRef, inject, Injectable, signal, computed } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthStateService, YurtApiService } from 'shared-api';
import { CreateCounterSaleRequest } from 'shared-models';
import { applyOutcome, due, enqueue, outcomeOf, QueuedSale, retryRefused, toQueued } from './outbox';
import { TillService } from './till.service';

/** How often the till tries to upload waiting sales while it has any. */
const RETRY_MS = 20_000;

/**
 * Sales made while the internet was down, kept on this tablet until the server has them. Each cafe has its own list,
 * so a sale is never sent to another cafe after someone else signs in. The list survives sign-out and reloads.
 */
@Injectable({ providedIn: 'root' })
export class OutboxService {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private till = inject(TillService);

  readonly queue = signal<QueuedSale[]>([]);
  readonly online = signal(typeof navigator === 'undefined' ? true : navigator.onLine);
  readonly syncing = signal(false);
  /** The server asked for a new sign-in; waiting sales go up after it. */
  readonly needsSignIn = signal(false);
  readonly waiting = computed(() => this.queue().filter((q) => q.state === 'waiting'));
  readonly refused = computed(() => this.queue().filter((q) => q.state === 'refused'));

  private cafe: string | null = null;

  constructor() {
    this.auth.user$.subscribe((u) => {
      const cafe = u ? (u.cafe ?? '') : null;
      if (cafe === null) return; // signed out: keep the list as it is, it goes up after the next sign-in
      this.cafe = cafe;
      this.queue.set(this.load());
      this.needsSignIn.set(false);
      void this.sync();
    });
    const up = () => { this.online.set(true); void this.sync(); };
    const down = () => this.online.set(false);
    window.addEventListener('online', up);
    window.addEventListener('offline', down);
    const timer = setInterval(() => { if (this.waiting().length) void this.sync(); }, RETRY_MS);
    inject(DestroyRef).onDestroy(() => {
      window.removeEventListener('online', up);
      window.removeEventListener('offline', down);
      clearInterval(timer);
    });
  }

  /** Keeps a sale that could not be sent and tries to send it soon. */
  save(request: CreateCounterSaleRequest, total: number, soldAt = new Date()): QueuedSale {
    const sale = toQueued(request, total, soldAt);
    this.set(enqueue(this.queue(), sale));
    this.till.rememberPending(request.idempotencyKey, total, request.paymentMethod, sale.request.soldAt);
    return sale;
  }

  retry(key: string): void {
    this.set(retryRefused(this.queue(), key));
    void this.sync();
  }

  /** A person decided a refused sale is not to be sent (they entered it by hand, or it was a mistake). */
  remove(key: string): void {
    this.set(this.queue().filter((q) => q.request.idempotencyKey !== key));
    this.till.forget(key);
  }

  /** Sends waiting sales one by one, oldest first; stops at the first sign that the server can't be reached. */
  async sync(): Promise<void> {
    if (this.syncing() || this.cafe === null || !this.auth.isLoggedIn) return;
    this.syncing.set(true);
    try {
      for (const sale of due(this.queue())) {
        const key = sale.request.idempotencyKey;
        try {
          const order = await firstValueFrom(this.api.createCounterSale(sale.request));
          this.set(applyOutcome(this.queue(), key, 'sent'));
          this.till.markUploaded(key, order);
          this.online.set(true);
        } catch (e) {
          const status = e instanceof HttpErrorResponse ? e.status : 0;
          const outcome = outcomeOf(status);
          const reason = e instanceof HttpErrorResponse ? (e.error?.detail ?? e.error?.title ?? e.error?.error ?? null) : null;
          this.set(applyOutcome(this.queue(), key, outcome, typeof reason === 'string' ? reason : null));
          if (outcome === 'signin') { this.needsSignIn.set(true); break; }
          if (outcome === 'retry') { if (status === 0) this.online.set(false); break; }
        }
      }
    } finally {
      this.syncing.set(false);
    }
  }

  private storageKey(): string {
    return `yurt_pos_outbox:${this.cafe ?? ''}`;
  }

  private load(): QueuedSale[] {
    try {
      const raw = localStorage.getItem(this.storageKey());
      return raw ? (JSON.parse(raw) as QueuedSale[]) : [];
    } catch {
      return [];
    }
  }

  private set(queue: QueuedSale[]): void {
    this.queue.set(queue);
    try {
      localStorage.setItem(this.storageKey(), JSON.stringify(queue));
    } catch {
      // Storage full or blocked: the list still lives in memory until the page closes. Not expected for a few sales.
    }
  }
}
