import { Component, computed, DestroyRef, effect, HostListener, inject, OnInit, signal, untracked } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AuthStateService, BrandService, YurtApiService } from 'shared-api';
import { Location, MenuCategory, MenuItem, MenuItemVariant, MenuTopping, Order, OrderType } from 'shared-models';
import { Currency2Pipe } from 'shared-ui';
import {
  addLine, cartCount, cartTotal, CartLine, changeQuantity, comboSummary, defaultVariant, missingChoice, needsOptions, ruleOf, toggleTopping,
} from '../../core/cart';
import {
  allergensOf, idleState, KioskStep, orderNumber, registerExitTap, secondsToReset, toKioskRequest,
} from '../../core/kiosk';
import { PosLang, PosLangService } from '../../core/lang.service';
import { TillService } from '../../core/till.service';
import { PosTranslatePipe } from '../../core/translate.pipe';

/**
 * The self-order kiosk (module kiosk): a tablet in the cafe, signed in with a staff login and locked to this one screen.
 * A guest picks here or to go, chooses dishes (sizes, add-ons and their rules, allergens shown), and gets an order
 * number. The order waits unpaid: they pay at the counter, where the cashier finds it in the orders list and accepts it.
 * After a minute without a touch the order is thrown away and the welcome screen comes back.
 */
@Component({
  selector: 'pos-kiosk',
  imports: [FormsModule, Currency2Pipe, PosTranslatePipe],
  templateUrl: './kiosk.component.html',
})
export class KioskComponent implements OnInit {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);
  readonly brand = inject(BrandService);
  readonly lang = inject(PosLangService);
  readonly till = inject(TillService);
  readonly langs: PosLang[] = ['ru', 'kk', 'en'];
  readonly comboSummary = comboSummary;

  readonly step = signal<KioskStep>('welcome');
  readonly orderType = signal<OrderType>('TakeAway');

  // Branch (asked once, by staff, when the cafe has several)
  readonly branches = signal<Location[]>([]);
  readonly branchesLoaded = signal(false);
  readonly branch = computed(() => this.branches().find((b) => b.id === this.till.branchId()) ?? null);

  // Menu
  readonly categories = signal<MenuCategory[]>([]);
  readonly items = signal<MenuItem[]>([]);
  readonly menuState = signal<'loading' | 'ready' | 'failed'>('loading');
  readonly category = signal<string | null>(null);
  readonly shown = computed(() => this.items().filter((i) => !this.category() || i.categoryId === this.category()));

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
  readonly pickMissing = computed(() => {
    const item = this.picking();
    return item ? missingChoice(item, this.pickToppings())?.title ?? null : null;
  });

  // The order
  readonly lines = signal<CartLine[]>([]);
  readonly total = computed(() => cartTotal(this.lines()));
  readonly count = computed(() => cartCount(this.lines()));
  readonly sending = signal(false);
  /** Why the order was not accepted (a translation of the server's reason when it gave one). */
  readonly problem = signal<string | null>(null);
  readonly order = signal<Order | null>(null);
  readonly number = computed(() => { const o = this.order(); return o ? orderNumber(o.id) : ''; });
  /** One key per order, kept for every retry, so a lost answer never makes a second order. A changed cart gets a new key. */
  private orderKey: string | null = null;

  // Idle clock
  private readonly lastTouch = signal(Date.now());
  private readonly now = signal(Date.now());
  readonly warning = computed(() => idleState(this.step(), this.lastTouch(), this.now()) === 'warn');
  readonly secondsLeft = computed(() => secondsToReset(this.lastTouch(), this.now()));

  // Staff exit
  private exitTaps: number[] = [];
  readonly exitOpen = signal(false);
  readonly exitBusy = signal(false);
  readonly exitFailed = signal(false);
  exitPin = '';

  constructor() {
    const tick = setInterval(() => {
      this.now.set(Date.now());
      if (idleState(this.step(), this.lastTouch(), this.now()) === 'reset' && !this.sending()) this.startOver();
    }, 1000);
    // The menu comes in the kiosk's language, for its branch.
    effect(() => {
      const branch = this.till.branchId();
      this.lang.lang();
      if (branch && this.branchesLoaded()) untracked(() => this.loadMenu());
    });
    // Bring the menu up to date every few minutes while nobody is ordering (stop-list, time menus).
    const refresh = setInterval(() => { if (this.step() === 'welcome') this.loadMenu(true); }, 5 * 60_000);
    this.destroyRef.onDestroy(() => { clearInterval(tick); clearInterval(refresh); });
  }

  ngOnInit(): void {
    // Signed out (the sign-in ran out): back to the sign-in screen.
    this.auth.user$.subscribe((u) => { if (!u) this.router.navigateByUrl('/login'); });
    this.api.getAdminLocations().subscribe({
      next: (list) => {
        this.till.saveBranches(list);
        this.useBranches(list);
      },
      error: () => this.useBranches(this.till.savedBranches() ?? []),
    });
    // The back button must not leave the kiosk: put the page back in history every time it is pressed.
    history.pushState(null, '', location.href);
  }

  private useBranches(list: Location[]): void {
    const open = list.filter((l) => l.isActive);
    this.branches.set(open);
    if (!open.some((b) => b.id === this.till.branchId())) this.till.setBranch(open.length === 1 ? open[0].id : null);
    this.branchesLoaded.set(true);
  }

  pickBranch(id: string): void {
    this.till.setBranch(id);
  }

  loadMenu(quiet = false): void {
    const branch = this.till.branchId();
    if (!branch) return;
    if (!quiet) this.menuState.set('loading');
    forkJoin({
      categories: this.api.getCategories(this.lang.lang(), branch),
      items: this.api.getMenuItems(undefined, undefined, this.lang.lang(), branch),
    }).subscribe({
      next: ({ categories, items }) => {
        // A guest cannot weigh a dish: dishes sold by weight are left to the counter.
        const sellable = items.filter((i) => !i.soldByWeight);
        const used = new Set(sellable.map((i) => i.categoryId));
        this.categories.set(categories.filter((c) => used.has(c.id)));
        this.items.set(sellable);
        if (this.category() && !used.has(this.category()!)) this.category.set(null);
        this.menuState.set('ready');
      },
      error: () => { if (!quiet) this.menuState.set('failed'); },
    });
  }

  // ── Any touch counts as "still here"; a locked screen has no way out ───────

  @HostListener('document:pointerdown')
  touched(): void {
    this.lastTouch.set(Date.now());
    this.now.set(Date.now());
  }

  @HostListener('window:popstate')
  keepHere(): void {
    history.pushState(null, '', location.href);
  }

  @HostListener('document:contextmenu', ['$event'])
  noMenu(e: Event): void {
    e.preventDefault();
  }

  // ── Steps ──────────────────────────────────────────────────────────────────

  begin(): void {
    // Fullscreen needs a touch, so it starts here. It is fine if the browser says no.
    document.documentElement.requestFullscreen?.().catch(() => undefined);
    this.step.set('type');
  }

  chooseType(type: OrderType): void {
    this.orderType.set(type);
    this.step.set('menu');
  }

  /** Throws the order away and shows the welcome screen for the next guest. */
  startOver(): void {
    this.picking.set(null);
    this.setLines([]);
    this.order.set(null);
    this.orderType.set('TakeAway');
    this.category.set(null);
    this.step.set('welcome');
    this.loadMenu(true);
  }

  // ── Items ──────────────────────────────────────────────────────────────────

  tap(item: MenuItem): void {
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

  allergens(item: MenuItem): string {
    return allergensOf(item).map((a) => this.lang.t('allergen.' + a)).join(', ');
  }

  toppingNames(line: CartLine): string {
    return line.toppings.map((t) => t.name).join(', ');
  }

  isPicked(t: MenuTopping): boolean {
    return this.pickToppings().some((x) => x.id === t.id);
  }

  toggle(t: MenuTopping): void {
    this.pickToppings.set(toggleTopping(this.pickToppings(), t, this.picking()?.optionRules));
  }

  /** The add-ons in their groups, each with the rule line the guest sees ("Required", "Up to 2"...). */
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

  confirmPick(): void {
    const item = this.picking();
    if (!item || missingChoice(item, this.pickToppings())) return;
    this.setLines(addLine(this.lines(), item, this.pickVariant(), this.pickToppings()));
    this.picking.set(null);
  }

  plus(line: CartLine): void { this.setLines(changeQuantity(this.lines(), line.key, +1)); }
  minus(line: CartLine): void {
    this.setLines(changeQuantity(this.lines(), line.key, -1));
    if (this.lines().length === 0 && this.step() === 'review') this.step.set('menu');
  }

  private setLines(lines: CartLine[]): void {
    this.lines.set(lines);
    this.orderKey = null;
    this.problem.set(null);
  }

  toReview(): void {
    if (this.lines().length > 0) this.step.set('review');
  }

  // ── Send ───────────────────────────────────────────────────────────────────

  send(): void {
    const branch = this.till.branchId();
    if (!branch || this.sending() || this.lines().length === 0) return;
    this.orderKey ??= crypto.randomUUID();
    this.sending.set(true);
    this.problem.set(null);
    this.api.createKioskOrder(toKioskRequest(this.lines(), branch, this.orderKey, this.orderType())).subscribe({
      next: (order) => {
        this.sending.set(false);
        this.order.set(order);
        this.step.set('done');
        this.touched();
      },
      error: (e: unknown) => {
        this.sending.set(false);
        // A refusal says why (a dish left the menu...); no answer at all means the internet is down.
        const reason = e instanceof HttpErrorResponse && e.status >= 400 && e.status < 500 ? (e.error?.title as string | undefined) : undefined;
        this.problem.set(reason || this.lang.t('kiosk.noInternet'));
      },
    });
  }

  // ── Staff exit: five quick taps on the logo, then a staff PIN ──────────────

  logoTap(): void {
    const r = registerExitTap(this.exitTaps, Date.now());
    this.exitTaps = r.taps;
    if (r.open) {
      this.exitPin = '';
      this.exitFailed.set(false);
      this.exitOpen.set(true);
    }
  }

  exitValid(): boolean {
    return /^[0-9]{4,6}$/.test(this.exitPin);
  }

  /** The server checks the PIN (the same staff PIN as at the till); a right one leaves the kiosk. */
  leave(): void {
    if (!this.exitValid() || this.exitBusy()) return;
    this.exitBusy.set(true);
    this.exitFailed.set(false);
    this.api.switchStaff(this.exitPin, this.auth.currentUser?.refreshToken ?? null).subscribe({
      next: (res) => {
        this.exitBusy.set(false);
        this.auth.setUser({
          accessToken: res.accessToken, refreshToken: res.refreshToken, userId: res.userId,
          displayName: res.displayName, userType: 'admin', role: res.role, cafe: res.cafe ?? null,
        });
        this.exitOpen.set(false);
        if (document.fullscreenElement) void document.exitFullscreen().catch(() => undefined);
        this.router.navigateByUrl('/');
      },
      error: () => {
        this.exitBusy.set(false);
        this.exitPin = '';
        this.exitFailed.set(true);
      },
    });
  }
}
