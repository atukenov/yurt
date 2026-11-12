import { Injectable, signal } from '@angular/core';
import { Location, MenuCategory, MenuItem, Order } from 'shared-models';

/** A sale as this till remembers it, for the "sales on this till" list. */
export interface TillSale {
  id: string;
  total: number;
  method: string;
  at: string;
  handedOver: boolean;
  /** Saved on this tablet, not on the server yet (id is the sale's key until then). */
  pending?: boolean;
  /** A manager cancelled it; the money was handed back. */
  cancelled?: boolean;
}

/** The last menu this till loaded for a branch, used when the internet is down. */
export interface SavedMenu {
  categories: MenuCategory[];
  items: MenuItem[];
  savedAt: string;
}

const BRANCH_KEY = 'yurt_pos_branch';
const SALES_KEY = 'yurt_pos_sales';
const MAX_SALES = 200;
const BRANCHES_KEY = 'yurt_pos_branches';
const menuKey = (branch: string, lang: string) => `yurt_pos_menu:${branch}:${lang}`;

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
    this.save([sale, ...this.sales()]);
  }

  /** A sale kept on the tablet until the internet is back. */
  rememberPending(key: string, total: number, method: string, at: string): void {
    if (this.sales().some((s) => s.id === key)) return;
    this.save([{ id: key, total, method, at, handedOver: true, pending: true }, ...this.sales()]);
  }

  /** The server has the sale now: show its real number. */
  markUploaded(key: string, order: Order): void {
    this.save(this.sales().map((s) => (s.id === key ? { ...s, id: order.id, pending: false } : s)));
  }

  markCancelled(id: string): void {
    this.save(this.sales().map((s) => (s.id === id ? { ...s, cancelled: true } : s)));
  }

  forget(key: string): void {
    this.save(this.sales().filter((s) => s.id !== key));
  }

  private save(list: TillSale[]): void {
    this.sales.set(list.slice(0, MAX_SALES));
    write(SALES_KEY, this.sales());
  }

  /** The cafe's branches as last seen, for starting the till offline. */
  savedBranches(): Location[] | null {
    return read<Location[] | null>(BRANCHES_KEY, null);
  }

  saveBranches(list: Location[]): void {
    write(BRANCHES_KEY, list);
  }

  savedMenu(branch: string, lang: string): SavedMenu | null {
    return read<SavedMenu | null>(menuKey(branch, lang), null);
  }

  saveMenu(branch: string, lang: string, categories: MenuCategory[], items: MenuItem[]): void {
    write(menuKey(branch, lang), { categories, items, savedAt: new Date().toISOString() } satisfies SavedMenu);
  }

  /** Sales from today, by this tablet's clock. */
  today(): TillSale[] {
    const start = new Date();
    start.setHours(0, 0, 0, 0);
    return this.sales().filter((s) => new Date(s.at) >= start);
  }
}
