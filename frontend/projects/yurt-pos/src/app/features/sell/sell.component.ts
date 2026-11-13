import { Component, computed, DestroyRef, effect, HostListener, inject, OnInit, signal, untracked } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AuthStateService, BrandService, findScanned, ModuleService, ScanCollector, YurtApiService } from 'shared-api';
import { Location, MenuCategory, MenuItem, MenuItemVariant, MenuTopping, Order, PaymentMethod } from 'shared-models';
import { Currency2Pipe, LocalDatePipe, ManagerApproval, ManagerApprovalComponent } from 'shared-ui';
import {
  addLine, addWeighedLine, kgText, branchMethods, comboSummary, cartCount, offlineNumber, receiptLines, cartTotal, CartLine, changeDue, changeQuantity, defaultVariant, needsOptions,
  discountOf, missingChoice, ruleOf, TillDiscount, TIP_PERCENTS, TipChoice, tipOf, toggleTopping, toRequest,
} from '../../core/cart';
import { weighedPrice } from 'shared-api';
import { PosLang, PosLangService } from '../../core/lang.service';
import { TillSale, TillService } from '../../core/till.service';
import { isTooOld, outcomeOf, QueuedSale } from '../../core/outbox';
import { GuestDisplayService } from '../../core/guest-display.service';
import { guestView } from '../../core/guest-display';
import { OutboxService } from '../../core/outbox.service';
import { StaffDialogComponent } from './staff-dialog.component';
import { TillPrinterService } from '../../core/till-printer.service';
import { BAUD_RATES, ScaleService } from '../../core/scale.service';
import { PosTranslatePipe } from '../../core/translate.pipe';

type Step = 'cart' | 'pay' | 'done';

/** The till: pick items, take payment, send the sale. */
@Component({
  selector: 'pos-sell',
  imports: [FormsModule, Currency2Pipe, LocalDatePipe, ManagerApprovalComponent, PosTranslatePipe, StaffDialogComponent],
  templateUrl: './sell.component.html',
})
export class SellComponent implements OnInit {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private router = inject(Router);
  readonly modules = inject(ModuleService);
  private destroyRef = inject(DestroyRef);
  readonly brand = inject(BrandService);
  readonly lang = inject(PosLangService);
  readonly till = inject(TillService);
  readonly outbox = inject(OutboxService);
  readonly printer = inject(TillPrinterService);
  readonly scale = inject(ScaleService);
  private guestDisplay = inject(GuestDisplayService);
  readonly baudRates = BAUD_RATES;
  readonly kgText = kgText;
  readonly comboSummary = comboSummary;
  readonly showPrinter = signal(false);
  /** Why the receipt of a sale kept on the tablet did not print (null = printed, or no printer connected). */
  readonly printProblem = signal<string | null>(null);

  readonly langs: PosLang[] = ['ru', 'kk', 'en'];
  /** Who is at the till now; changes when someone takes over with their staff PIN. */
  get cashier(): string {
    return this.auth.currentUser?.displayName ?? '';
  }
  readonly showStaff = signal(false);

  // Branch
  readonly branches = signal<Location[]>([]);
  readonly branchesLoaded = signal(false);
  readonly branch = computed(() => this.branches().find((b) => b.id === this.till.branchId()) ?? null);
  readonly methods = computed(() => branchMethods(this.branch()?.paymentMethods));

  // Menu
  readonly categories = signal<MenuCategory[]>([]);
  readonly items = signal<MenuItem[]>([]);
  readonly menuState = signal<'loading' | 'ready' | 'failed'>('loading');
  /** When the menu on screen was saved, if it came from this tablet because the server could not be reached. */
  readonly menuSavedAt = signal<string | null>(null);
  readonly category = signal<string | null>(null);
  readonly search = signal('');
  readonly shown = computed(() => {
    const q = this.search().trim().toLowerCase();
    const cat = this.category();
    // A barcode typed or scanned into the search box finds its dish too.
    return this.items().filter((i) => (q ? i.name.toLowerCase().includes(q) || (!!i.barcode && i.barcode.toLowerCase().includes(q)) : !cat || i.categoryId === cat));
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

  // Weighing an item sold by weight
  readonly weighing = signal<MenuItem | null>(null);
  /** The weight typed in grams (a cashier without a scale); empty = none yet. */
  readonly weighGrams = signal<string>('');
  /** What the scanner last did, shown for a few seconds ("Unknown barcode 4600..."). */
  readonly scanNote = signal<string | null>(null);
  private scanNoteTimer: ReturnType<typeof setTimeout> | null = null;
  private readonly scanner = new ScanCollector();
  /** The weight on the way in: the scale's if it is steady, else the typed one. */
  readonly weighValue = computed<number | null>(() => {
    const live = this.scale.usable();
    if (live && this.weighGrams() === '') return live.grams;
    const typed = Math.round(Number(this.weighGrams()));
    return Number.isFinite(typed) && typed > 0 && typed <= 50_000 ? typed : null;
  });
  readonly weighPrice = computed(() => {
    const item = this.weighing();
    const grams = this.weighValue();
    return item && grams ? weighedPrice(item.price, grams) : 0;
  });

  // The sale
  readonly lines = signal<CartLine[]>([]);
  readonly total = computed(() => cartTotal(this.lines()));
  readonly count = computed(() => cartCount(this.lines()));
  readonly step = signal<Step>('cart');
  readonly method = signal<PaymentMethod | null>(null);
  readonly cashGiven = signal<number | null>(null);
  /** Money a manager took off this sale (with their PIN); null = no discount. */
  readonly discount = signal<TillDiscount | null>(null);
  readonly discountAmount = computed(() => discountOf(this.total(), this.discount()));
  /** What the guest pays. */
  readonly payable = computed(() => Math.round((this.total() - this.discountAmount()) * 100) / 100);
  /** The tip on top of the bill: chosen on the payment step, never part of the total. */
  readonly tipChoice = signal<TipChoice>({ kind: 'none' });
  readonly tipPercents = TIP_PERCENTS;
  readonly tipCustomOpen = signal(false);
  /** The percent chosen (5, 10...), or null when no percent button is on. */
  readonly tipPercent = computed(() => { const c = this.tipChoice(); return c.kind === 'percent' ? c.value : null; });
  /** The typed amount, or null. */
  readonly tipTyped = computed(() => { const c = this.tipChoice(); return c.kind === 'amount' ? c.value : null; });
  /** Guest paid the bill by card but gave the tip in cash. */
  readonly tipInCash = signal(false);
  readonly tip = computed(() => tipOf(this.payable(), this.tipChoice()));
  /** Cash change counts a cash tip too: the guest hands over the bill and the tip together. */
  readonly change = computed(() => changeDue(this.payable() + (this.method() === PaymentMethod.Cash ? this.tip() : 0), this.cashGiven()));
  readonly discountOpen = signal(false);
  discountKind: 'percent' | 'amount' = 'percent';
  discountValue: number | null = null;
  /** Why the server refused the discount (wrong PIN, locked...) or that it needs the internet. */
  readonly discountProblem = signal<string | null>(null);
  /** The sale a manager is cancelling from the list of sales on this till. */
  readonly cancelTarget = signal<TillSale | null>(null);
  readonly cancelBusy = signal(false);
  readonly cancelProblem = signal<string | null>(null);
  readonly handedOver = signal(false);
  readonly orderType = signal<'DineIn' | 'TakeAway'>('TakeAway');
  readonly eta = signal(5);
  readonly sending = signal(false);
  readonly failed = signal(false);
  readonly lastSale = signal<Order | null>(null);
  /** The last sale was kept on this tablet (no internet): its total. */
  readonly lastQueued = signal<QueuedSale | null>(null);
  readonly signinNeeded = signal(false);
  readonly showSales = signal(false);
  /** One key per sale, kept for every retry of it, so a resend never makes a second sale. A new cart gets a new key. */
  private saleKey: string | null = null;

  constructor() {
    this.destroyRef.onDestroy(() => { if (this.menuTimer) clearTimeout(this.menuTimer); });
    void this.scale.reconnect();
    // The guest screen (a second window on the customer monitor) shows what is being rung up.
    effect(() => {
      const done = this.step() === 'done';
      const sale = this.lastSale();
      const queued = this.lastQueued();
      this.guestDisplay.publish(guestView({
        step: this.step(), lang: this.lang.lang(), lines: this.lines(), total: this.total(), discount: this.discountAmount(),
        payable: this.payable(), tip: this.tip(), method: this.method(), cashGiven: this.cashGiven(), change: this.change(),
        orderType: this.orderType(), paid: done ? (sale?.total ?? queued?.total ?? 0) : 0,
        saleNo: done ? (sale ? this.shortNo(sale.id) : queued ? offlineNumber(queued.request.idempotencyKey) : null) : null,
        saved: done && !sale && !!queued,
      }));
    });
    // Closing the till leaves the guest screen on its welcome picture, not on the last order.
    this.destroyRef.onDestroy(() => this.guestDisplay.publish(guestView({
      step: 'cart', lang: this.lang.lang(), lines: [], total: 0, discount: 0, payable: 0, tip: 0, method: null,
      cashGiven: null, change: null, orderType: 'TakeAway', paid: 0, saleNo: null, saved: false,
    })));
    // The menu comes in the till's language, for its branch.
    effect(() => {
      const branch = this.till.branchId();
      this.lang.lang();
      if (branch && this.branchesLoaded()) untracked(() => { this.loadMenu(); void this.printer.start(branch); });
    });
  }

  ngOnInit(): void {
    // Signed out (for example the sign-in ran out): back to the sign-in screen. Waiting sales stay on the tablet.
    this.auth.user$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((u) => { if (!u) this.router.navigateByUrl('/login'); });
    this.api.getAdminLocations().subscribe({
      next: (list) => {
        this.till.saveBranches(list);
        this.useBranches(list);
      },
      // No internet: the branches this tablet saw last time.
      error: () => this.useBranches(this.till.savedBranches() ?? []),
    });
  }

  private useBranches(list: Location[]): void {
    const open = list.filter((l) => l.isActive);
    this.branches.set(open);
    const saved = this.till.branchId();
    if (!open.some((b) => b.id === saved)) this.till.setBranch(open.length === 1 ? open[0].id : null);
    this.branchesLoaded.set(true);
  }

  /** Time menus (breakfast, happy hour): load the menu again when one opens or closes, so the till follows the clock. */
  private menuTimer: ReturnType<typeof setTimeout> | null = null;

  private scheduleMenuRefresh(branch: string): void {
    if (this.menuTimer) clearTimeout(this.menuTimer);
    this.api.getMenuNextChange(branch).subscribe({
      next: ({ at }) => {
        if (!at) return;
        const wait = Math.min(Math.max(new Date(at).getTime() - Date.now() + 1000, 1000), 2_000_000_000);
        this.menuTimer = setTimeout(() => this.loadMenu(true), wait);
      },
      error: () => undefined,
    });
  }

  loadMenu(quiet = false): void {
    const branch = this.till.branchId();
    if (!branch) return;
    // A reload by the clock keeps the menu on screen; a cashier in the middle of a sale is not interrupted.
    if (!quiet) this.menuState.set('loading');
    forkJoin({
      categories: this.api.getCategories(this.lang.lang(), branch),
      items: this.api.getMenuItems(undefined, undefined, this.lang.lang(), branch),
    }).subscribe({
      next: ({ categories, items }) => {
        this.till.saveMenu(branch, this.lang.lang(), categories, items);
        this.showMenu(categories, items, null);
        this.scheduleMenuRefresh(branch);
      },
      error: () => {
        // No internet: sell from the menu this tablet saved last time.
        const saved = this.till.savedMenu(branch, this.lang.lang());
        if (saved) this.showMenu(saved.categories, saved.items, saved.savedAt);
        else if (!quiet) this.menuState.set('failed');
        // Try again in a minute: the internet may be back, and the clock may have moved a time menu on.
        if (this.menuTimer) clearTimeout(this.menuTimer);
        this.menuTimer = setTimeout(() => this.loadMenu(true), 60_000);
      },
    });
  }

  private showMenu(categories: MenuCategory[], items: MenuItem[], savedAt: string | null): void {
    const used = new Set(items.map((i) => i.categoryId));
    this.categories.set(categories.filter((c) => used.has(c.id)));
    this.items.set(items);
    if (this.category() && !used.has(this.category()!)) this.category.set(null);
    this.menuSavedAt.set(savedAt);
    this.menuState.set('ready');
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
    if (item.soldByWeight) {
      this.openWeigh(item);
      return;
    }
    if (needsOptions(item)) {
      this.picking.set(item);
      this.pickVariant.set(defaultVariant(item)?.id ?? null);
      this.pickToppings.set([]);
      return;
    }
    this.setLines(addLine(this.lines(), item, defaultVariant(item)?.id ?? null, []));
  }

  // ── Scanner and scale ──────────────────────────────────────────────────────

  /** A barcode scanner types like a very fast keyboard and ends with Enter; a person typing is far slower. */
  @HostListener('document:keydown', ['$event'])
  onKey(e: KeyboardEvent): void {
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    const el = e.target as HTMLElement | null;
    const inField = !!el && ['INPUT', 'TEXTAREA', 'SELECT'].includes(el.tagName);
    const inSearch = !!el && el.dataset?.['scan'] === 'search';
    // Other fields (cash given, PINs, amounts) keep what is typed into them.
    if (inField && !inSearch) return;
    if (this.step() === 'pay' || this.picking() || this.weighing() || this.showStaff() || this.showSales() || this.showPrinter()) return;
    const code = this.scanner.feed(e.key, e.timeStamp);
    if (code === null) return;
    e.preventDefault();
    if (inSearch) this.search.set('');
    this.onScan(code);
  }

  /** A scanned code: add the dish, or open the weighing for one sold by weight. */
  onScan(code: string): void {
    const found = findScanned(this.items(), code);
    if (found.kind === 'unknown') {
      this.note(this.lang.t('scan.unknown', { code }));
      return;
    }
    if (this.step() === 'done') this.next();
    if (found.kind === 'item') {
      this.tap(found.item);
      return;
    }
    // A label from the shop scale already holds the weight.
    if (found.grams) this.setLines(addWeighedLine(this.lines(), found.item, found.grams, crypto.randomUUID()));
    else this.openWeigh(found.item);
  }

  private note(text: string): void {
    this.scanNote.set(text);
    if (this.scanNoteTimer) clearTimeout(this.scanNoteTimer);
    this.scanNoteTimer = setTimeout(() => this.scanNote.set(null), 4000);
  }

  openWeigh(item: MenuItem): void {
    this.weighGrams.set('');
    this.weighing.set(item);
  }

  /** Takes the typed weight, or the scale's if the cashier typed nothing. */
  confirmWeigh(): void {
    const item = this.weighing();
    const grams = this.weighValue();
    if (!item || !grams) return;
    this.setLines(addWeighedLine(this.lines(), item, grams, crypto.randomUUID()));
    this.weighing.set(null);
  }

  async toggleScale(): Promise<void> {
    if (this.scale.connected()) await this.scale.disconnect();
    else await this.scale.connect();
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
    this.pickToppings.set(toggleTopping(this.pickToppings(), t, this.picking()?.optionRules));
  }

  /** The add-ons in their groups, each with the rule line the cashier sees ("Required", "Up to 2"...). */
  pickGroups(item: MenuItem): { key: string; title: string; hint: string; toppings: MenuTopping[] }[] {
    const all = this.toppingsOf(item);
    const keys = [...new Set(all.filter((t) => t.group).map((t) => t.group!))];
    const result = keys.map((key) => {
      const r = ruleOf(item.optionRules, key);
      const hint = r.min > 0 && r.max !== null && r.max > r.min ? this.lang.t('opt.range', { min: r.min, max: r.max })
        : r.min > 1 ? this.lang.t('opt.atLeast', { min: r.min })
        : r.min > 0 ? this.lang.t('opt.required')
        : r.max !== null && r.max > 1 ? this.lang.t('opt.upTo', { max: r.max }) : '';
      return { key, title: r.title && r.title !== key ? r.title : key, hint, toppings: all.filter((t) => t.group === key) };
    });
    const free = all.filter((t) => !t.group);
    if (free.length) result.push({ key: '', title: this.lang.t('opt.addons'), hint: '', toppings: free });
    return result;
  }

  /** What is still to choose for the item being picked; null when the add button may work. */
  readonly pickMissing = computed(() => {
    const item = this.picking();
    return item ? missingChoice(item, this.pickToppings())?.title ?? null : null;
  });

  confirmPick(): void {
    const item = this.picking();
    if (!item || missingChoice(item, this.pickToppings())) return;
    this.setLines(addLine(this.lines(), item, this.pickVariant(), this.pickToppings()));
    this.picking.set(null);
  }

  plus(line: CartLine): void { this.setLines(changeQuantity(this.lines(), line.key, +1)); }
  minus(line: CartLine): void { this.setLines(changeQuantity(this.lines(), line.key, -1)); }

  chooseTip(percent: number | null): void {
    this.tipCustomOpen.set(false);
    this.tipChoice.set(percent ? { kind: 'percent', value: percent } : { kind: 'none' });
  }

  setTipAmount(value: string | number | null): void {
    const n = value === null || value === '' ? 0 : Number(value);
    this.tipChoice.set(Number.isFinite(n) && n > 0 ? { kind: 'amount', value: n } : { kind: 'none' });
  }

  clear(): void {
    this.setLines([]);
    this.step.set('cart');
  }

  private resetTip(): void {
    this.tipChoice.set({ kind: 'none' });
    this.tipCustomOpen.set(false);
    this.tipInCash.set(false);
  }

  private setLines(lines: CartLine[]): void {
    this.lines.set(lines);
    this.discount.set(null);
    this.resetTip();
    this.discountProblem.set(null);
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

  // ── Discount (manager PIN) ─────────────────────────────────────────────────

  openDiscount(): void {
    this.discountKind = 'percent';
    this.discountValue = null;
    this.discountProblem.set(null);
    this.discountOpen.set(true);
  }

  discountReady(): boolean {
    const v = this.discountValue ?? 0;
    return v > 0 && (this.discountKind === 'amount' ? v <= this.total() : v <= 100);
  }

  /** Keeps the PIN and reason in memory only until the sale is sent. The server checks them then. */
  applyDiscount(approved: ManagerApproval): void {
    if (!this.discountReady()) return;
    this.discount.set({ kind: this.discountKind, value: this.discountValue!, pin: approved.pin, reason: approved.reason });
    this.discountOpen.set(false);
  }

  removeDiscount(): void {
    this.discount.set(null);
    this.discountProblem.set(null);
  }

  // ── Cancel a sale (manager PIN) ────────────────────────────────────────────

  cancelSale(approved: ManagerApproval): void {
    const sale = this.cancelTarget();
    if (!sale) return;
    this.cancelBusy.set(true);
    this.cancelProblem.set(null);
    this.api.voidOrder(sale.id, approved).subscribe({
      next: () => {
        this.cancelBusy.set(false);
        this.till.markCancelled(sale.id);
        this.cancelTarget.set(null);
      },
      error: (e: unknown) => {
        this.cancelBusy.set(false);
        this.cancelProblem.set(this.problemText(e));
        this.cancelTarget.set(null);
      },
    });
  }

  private problemText(e: unknown): string {
    const title = e instanceof HttpErrorResponse ? (e.error?.title as string | undefined) : undefined;
    return title || this.lang.t('disc.offline');
  }

  finish(): void {
    const branch = this.till.branchId();
    const method = this.method();
    if (!branch || !method || !this.canFinish()) return;
    this.saleKey ??= crypto.randomUUID();
    const handedOver = this.handedOver();
    const request = toRequest(this.lines(), branch, method, this.saleKey, handedOver, this.eta(), this.orderType(), this.discount(),
      this.tip(), this.tipInCash() ? PaymentMethod.Cash : null);
    this.failed.set(false);
    this.signinNeeded.set(false);
    this.discountProblem.set(null);
    // A discount is checked by the server with the manager's PIN, so it can't wait on the tablet: no internet, no discount.
    if (request.discount && !this.outbox.online()) {
      this.discountProblem.set(this.lang.t('disc.offline'));
      return;
    }
    // Known to be offline: keep the sale on the tablet straight away.
    if (!this.outbox.online()) {
      this.keepOffline(request);
      return;
    }
    this.sending.set(true);
    this.api.createCounterSale(request).subscribe({
      next: (order) => {
        this.sending.set(false);
        this.outbox.online.set(true);
        this.till.remember(order, handedOver);
        this.lastQueued.set(null);
        this.lastSale.set(order);
        this.lines.set([]);
        this.discount.set(null);
        this.saleKey = null;
        this.step.set('done');
      },
      error: (e: unknown) => {
        this.sending.set(false);
        const outcome = outcomeOf(e instanceof HttpErrorResponse ? e.status : 0);
        if (request.discount && outcome !== 'signin') {
          // Wrong PIN, locked out or no connection: say so and let the cashier fix or remove the discount.
          this.discountProblem.set(this.problemText(e));
          return;
        }
        // No answer (or the server is down): the guest has paid, so the sale is kept on the tablet with the same key.
        // If the first try did reach the server, the upload gets that sale back instead of making a second one.
        if (outcome === 'retry') this.keepOffline(request);
        else {
          this.failed.set(outcome === 'refused');
          this.signinNeeded.set(outcome === 'signin');
        }
      },
    });
  }

  private keepOffline(request: ReturnType<typeof toRequest>): void {
    const lines = this.lines();
    const total = this.total();
    const sale = this.outbox.save(request, total);
    // The server never sees this sale until the internet is back, so the till prints the guest's receipt itself.
    this.printProblem.set(null);
    if (this.printer.hasPrinter()) {
      void this.printer.printOffline({
        brand: this.brand.name(), branch: this.branch()?.name ?? '', at: new Date(sale.request.soldAt),
        number: offlineNumber(request.idempotencyKey), lines: receiptLines(lines), total: this.payable(), tip: this.tip(),
        method: request.paymentMethod, offline: true,
      }, request.paymentMethod === PaymentMethod.Cash).then((problem) => this.printProblem.set(problem));
    }
    this.lastSale.set(null);
    this.lastQueued.set(sale);
    this.lines.set([]);
    this.saleKey = null;
    this.step.set('done');
  }

  retryQueued(key: string): void {
    this.outbox.retry(key);
  }

  removeQueued(sale: QueuedSale): void {
    if (!confirm(this.lang.t('queue.removeConfirm'))) return;
    this.outbox.remove(sale.request.idempotencyKey);
  }

  tooOld(sale: QueuedSale): boolean {
    return isTooOld(sale, new Date());
  }

  next(): void {
    this.discount.set(null);
    this.resetTip();
    this.lastSale.set(null);
    this.lastQueued.set(null);
    this.printProblem.set(null);
    this.handedOver.set(false);
    this.orderType.set('TakeAway');
    this.step.set('cart');
  }

  shortNo(id: string): string {
    return id.slice(-6).toUpperCase();
  }

  setCash(value: string | number | null): void {
    const n = value === null || value === '' ? null : Number(value);
    this.cashGiven.set(n === null || Number.isNaN(n) ? null : n);
  }

  /** Hands the tablet to guests: the self-order kiosk (module kiosk). Staff leave it with a PIN. */
  openKiosk(): void {
    this.router.navigateByUrl('/kiosk');
  }

  signOut(): void {
    this.auth.logout();
    this.router.navigateByUrl('/login');
  }
}
