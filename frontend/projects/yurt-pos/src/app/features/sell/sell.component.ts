import { Component, computed, effect, inject, OnInit, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AuthStateService, BrandService, YurtApiService } from 'shared-api';
import { Location, MenuCategory, MenuItem, MenuItemVariant, MenuTopping, Order, PaymentMethod } from 'shared-models';
import { Currency2Pipe, LocalDatePipe } from 'shared-ui';
import {
  addLine, branchMethods, cartCount, cartTotal, CartLine, changeDue, changeQuantity, defaultVariant, needsOptions,
  toggleTopping, toRequest,
} from '../../core/cart';
import { PosLang, PosLangService } from '../../core/lang.service';
import { TillService } from '../../core/till.service';
import { PosTranslatePipe } from '../../core/translate.pipe';

type Step = 'cart' | 'pay' | 'done';

/** The till: pick items, take payment, send the sale. */
@Component({
  selector: 'pos-sell',
  imports: [FormsModule, Currency2Pipe, LocalDatePipe, PosTranslatePipe],
  templateUrl: './sell.component.html',
})
export class SellComponent implements OnInit {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private router = inject(Router);
  readonly brand = inject(BrandService);
  readonly lang = inject(PosLangService);
  readonly till = inject(TillService);

  readonly langs: PosLang[] = ['ru', 'kk', 'en'];
  readonly cashier = this.auth.currentUser?.displayName ?? '';

  // Branch
  readonly branches = signal<Location[]>([]);
  readonly branchesLoaded = signal(false);
  readonly branch = computed(() => this.branches().find((b) => b.id === this.till.branchId()) ?? null);
  readonly methods = computed(() => branchMethods(this.branch()?.paymentMethods));

  // Menu
  readonly categories = signal<MenuCategory[]>([]);
  readonly items = signal<MenuItem[]>([]);
  readonly menuState = signal<'loading' | 'ready' | 'failed'>('loading');
  readonly category = signal<string | null>(null);
  readonly search = signal('');
  readonly shown = computed(() => {
    const q = this.search().trim().toLowerCase();
    const cat = this.category();
    return this.items().filter((i) => (q ? i.name.toLowerCase().includes(q) : !cat || i.categoryId === cat));
  });

  // Choosing a size and add-ons
  readonly picking = signal<MenuItem | null>(null);
  readonly pickVariant = signal<string | null>(null);
  readonly pickToppings = signal<MenuTopping[]>([]);
  readonly pickPrice = computed(() => {
    const item = this.picking();
    if (!item) return 0;
    const v = item.variants?.find((x) => x.id === this.pickVariant());
    return (v?.price ?? item.price) + this.pickToppings().reduce((s, t) => s + t.price, 0);
  });

  // The sale
  readonly lines = signal<CartLine[]>([]);
  readonly total = computed(() => cartTotal(this.lines()));
  readonly count = computed(() => cartCount(this.lines()));
  readonly step = signal<Step>('cart');
  readonly method = signal<PaymentMethod | null>(null);
  readonly cashGiven = signal<number | null>(null);
  readonly change = computed(() => changeDue(this.total(), this.cashGiven()));
  readonly handedOver = signal(false);
  readonly eta = signal(5);
  readonly sending = signal(false);
  readonly failed = signal(false);
  readonly lastSale = signal<Order | null>(null);
  readonly showSales = signal(false);
  /** One key per sale, kept for every retry of it, so a resend never makes a second sale. A new cart gets a new key. */
  private saleKey: string | null = null;

  constructor() {
    // The menu comes in the till's language, for its branch.
    effect(() => {
      const branch = this.till.branchId();
      this.lang.lang();
      if (branch && this.branchesLoaded()) untracked(() => this.loadMenu());
    });
  }

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe({
      next: (list) => {
        const open = list.filter((l) => l.isActive);
        this.branches.set(open);
        const saved = this.till.branchId();
        if (!open.some((b) => b.id === saved)) this.till.setBranch(open.length === 1 ? open[0].id : null);
        this.branchesLoaded.set(true);
      },
      error: () => this.branchesLoaded.set(true),
    });
  }

  loadMenu(): void {
    const branch = this.till.branchId();
    if (!branch) return;
    this.menuState.set('loading');
    forkJoin({
      categories: this.api.getCategories(this.lang.lang(), branch),
      items: this.api.getMenuItems(undefined, undefined, this.lang.lang(), branch),
    }).subscribe({
      next: ({ categories, items }) => {
        const used = new Set(items.map((i) => i.categoryId));
        this.categories.set(categories.filter((c) => used.has(c.id)));
        this.items.set(items);
        if (this.category() && !used.has(this.category()!)) this.category.set(null);
        this.menuState.set('ready');
      },
      error: () => this.menuState.set('failed'),
    });
  }

  pickBranch(id: string | null): void {
    this.clear();
    this.till.setBranch(id);
  }

  // ── Items ──────────────────────────────────────────────────────────────────

  tap(item: MenuItem): void {
    if (this.step() === 'pay') return;
    // Tapping an item after a sale starts the next guest's sale.
    if (this.step() === 'done') this.next();
    if (needsOptions(item)) {
      this.picking.set(item);
      this.pickVariant.set(defaultVariant(item)?.id ?? null);
      this.pickToppings.set([]);
      return;
    }
    this.setLines(addLine(this.lines(), item, defaultVariant(item)?.id ?? null, []));
  }

  toppingsOf(item: MenuItem): MenuTopping[] {
    return (item.availableToppings ?? []).filter((t) => t.isAvailable);
  }

  variantsOf(item: MenuItem): MenuItemVariant[] {
    return [...(item.variants ?? [])].sort((a, b) => a.sortOrder - b.sortOrder);
  }

  isPicked(t: MenuTopping): boolean {
    return this.pickToppings().some((x) => x.id === t.id);
  }

  toggle(t: MenuTopping): void {
    this.pickToppings.set(toggleTopping(this.pickToppings(), t));
  }

  confirmPick(): void {
    const item = this.picking();
    if (!item) return;
    this.setLines(addLine(this.lines(), item, this.pickVariant(), this.pickToppings()));
    this.picking.set(null);
  }

  plus(line: CartLine): void { this.setLines(changeQuantity(this.lines(), line.key, +1)); }
  minus(line: CartLine): void { this.setLines(changeQuantity(this.lines(), line.key, -1)); }

  clear(): void {
    this.setLines([]);
    this.step.set('cart');
  }

  private setLines(lines: CartLine[]): void {
    this.lines.set(lines);
    this.saleKey = null;
    this.failed.set(false);
  }

  // ── Payment ────────────────────────────────────────────────────────────────

  toPay(): void {
    if (this.lines().length === 0) return;
    this.method.set(this.methods().length === 1 ? this.methods()[0] : null);
    this.cashGiven.set(null);
    this.failed.set(false);
    this.step.set('pay');
  }

  backToCart(): void {
    if (this.sending()) return;
    this.step.set('cart');
  }

  canFinish(): boolean {
    const m = this.method();
    if (!m || this.sending() || this.lines().length === 0) return false;
    // Cash given is optional (only to work out the change), but if typed it must cover the total.
    return m !== PaymentMethod.Cash || this.cashGiven() === null || this.change() !== null;
  }

  finish(): void {
    const branch = this.till.branchId();
    const method = this.method();
    if (!branch || !method || !this.canFinish()) return;
    this.saleKey ??= crypto.randomUUID();
    const handedOver = this.handedOver();
    this.sending.set(true);
    this.failed.set(false);
    this.api.createCounterSale(toRequest(this.lines(), branch, method, this.saleKey, handedOver, this.eta())).subscribe({
      next: (order) => {
        this.sending.set(false);
        this.till.remember(order, handedOver);
        this.lastSale.set(order);
        this.lines.set([]);
        this.saleKey = null;
        this.step.set('done');
      },
      error: () => {
        // The key stays, so pressing again cannot make the sale twice.
        this.sending.set(false);
        this.failed.set(true);
      },
    });
  }

  next(): void {
    this.lastSale.set(null);
    this.handedOver.set(false);
    this.step.set('cart');
  }

  shortNo(id: string): string {
    return id.slice(-6).toUpperCase();
  }

  setCash(value: string | number | null): void {
    const n = value === null || value === '' ? null : Number(value);
    this.cashGiven.set(n === null || Number.isNaN(n) ? null : n);
  }

  signOut(): void {
    this.auth.logout();
    this.router.navigateByUrl('/login');
  }
}
