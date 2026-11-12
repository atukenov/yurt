import { Injectable, signal } from '@angular/core';
import { Order } from 'shared-models';

/** A sale as this till remembers it, for the "sales on this till" list. */
export interface TillSale {
  id: string;
  total: number;
  method: string;
  at: string;
  handedOver: boolean;
}

const BRANCH_KEY = 'yurt_pos_branch';
const SALES_KEY = 'yurt_pos_sales';
const MAX_SALES = 200;

function read<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : fallback;
  } catch {
    return fallback;
  }
}

function write(key: string, value: unknown): void {
  try { localStorage.setItem(key, JSON.stringify(value)); } catch { /* storage full or blocked: keep it in memory only */ }
}

/** What this tablet remembers between visits: its branch and its own recent sales. */
@Injectable({ providedIn: 'root' })
export class TillService {
  readonly branchId = signal<string | null>(read<string | null>(BRANCH_KEY, null));
  readonly sales = signal<TillSale[]>(read<TillSale[]>(SALES_KEY, []));

  setBranch(id: string | null): void {
    this.branchId.set(id);
    write(BRANCH_KEY, id);
  }

  remember(order: Order, handedOver: boolean): void {
    if (this.sales().some((s) => s.id === order.id)) return; // a resent sale came back as the first one
    const sale: TillSale = { id: order.id, total: order.total, method: order.paymentMethod ?? '', at: order.createdAt, handedOver };
    this.sales.update((list) => [sale, ...list].slice(0, MAX_SALES));
    write(SALES_KEY, this.sales());
  }

  /** Sales from today, by this tablet's clock. */
  today(): TillSale[] {
    const start = new Date();
    start.setHours(0, 0, 0, 0);
    return this.sales().filter((s) => new Date(s.at) >= start);
  }
}
