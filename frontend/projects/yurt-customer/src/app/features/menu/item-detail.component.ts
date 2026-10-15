import { Component, inject, OnInit, signal, computed, Input, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { YurtApiService, AuthStateService } from 'shared-api';
import { MenuItem, MenuTopping, MenuItemVariant, OrderItemToppingInput } from 'shared-models';
import { ToastService, Currency2Pipe } from 'shared-ui';
import { CartService } from '../cart/cart.service';
import { LangService } from '../../core/lang.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { StoreStatusService } from '../../core/store-status.service';
import { LocationService } from '../../core/location.service';
import { PullToRefreshDirective } from '../../shared/pull-to-refresh.directive';
import { CupViewerComponent } from './cup-viewer.component';

@Component({
  selector: 'app-item-detail',
  standalone: true,
  imports: [CommonModule, Currency2Pipe, TranslatePipe, PullToRefreshDirective, CupViewerComponent],
  templateUrl: './item-detail.component.html',
  styleUrl: './item-detail.component.css',
})
export class ItemDetailComponent implements OnInit {
  @Input() id!: string;

  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private cart = inject(CartService);
  private router = inject(Router);
  private toast = inject(ToastService);
  private langService = inject(LangService);
  private locationSvc = inject(LocationService);
  private storeStatus = inject(StoreStatusService);

  // ── Existing state (unchanged) ──────────────────────────────────────────────
  item = signal<MenuItem | null>(null);
  loading = signal(true);
  quantity = signal(1);
  isFav = signal(false);
  selectedToppingIds = signal<Set<string>>(new Set());
  selectedVariant = signal<MenuItemVariant | null>(null);
  imageError = signal(false);

  readonly effectivePrice = computed(() =>
    this.selectedVariant()?.price ?? this.item()?.price ?? 0
  );

  readonly unavailableAtLocation = computed(() => {
    const item = this.item();
    const locId = this.locationSvc.locationId();
    if (!item?.locationIds?.length) return false;
    return !item.locationIds.includes(locId);
  });

  readonly toppingTotal = computed(() => {
    const item = this.item();
    if (!item?.availableToppings) return 0;
    return item.availableToppings
      .filter((t) => this.selectedToppingIds().has(t.id))
      .reduce((sum, t) => sum + t.price, 0);
  });

  readonly lineTotal = computed(() =>
    (this.effectivePrice() + this.toppingTotal()) * this.quantity()
  );

  // Unit price shown in header (no quantity)
  readonly unitPrice = computed(() => this.effectivePrice() + this.toppingTotal());

  // ── Cup visualization (presentational only) ─────────────────────────────────
  private readonly MILK_COLORS: Record<string, string> = {
    'Обычное': '#c9a06e', 'Овсяное': '#d8b988', 'Миндальное': '#bd9264',
  };
  private readonly SYRUP_COLORS: Record<string, string> = {
    'Без сиропа': '#f5f5f4', 'Карамель': '#b06f24', 'Ваниль': '#e8cf9a',
  };

  // Index-based fallback palette so any milk selection visibly changes the cup
  private readonly MILK_PALETTE = ['#c9a06e', '#d8b988', '#bd9264', '#b8956a'];

  milkSwatchColor(name: string): string {
    if (this.MILK_COLORS[name]) return this.MILK_COLORS[name];
    // Partial match for names like "Миндальное молоко" → "Миндальное"
    const key = Object.keys(this.MILK_COLORS).find(k => name.includes(k));
    return key ? this.MILK_COLORS[key] : '#c9a06e';
  }

  syrupSwatchColor(name: string): string {
    if (this.SYRUP_COLORS[name]) return this.SYRUP_COLORS[name];
    const key = Object.keys(this.SYRUP_COLORS).find(k => name.includes(k));
    return key ? this.SYRUP_COLORS[key] : '#e8cf9a';
  }

  // Category names come back localized (en/ru/kk) from the API, so match on the
  // shared "coffee"/"tea" word roots rather than a hardcoded category id/name —
  // works across all three languages without needing a structured category tag.
  private static readonly HOT_DRINK_KEYWORDS = ['coffee', 'кофе', 'tea', 'чай', 'шай'];
  private static readonly COLD_DRINK_KEYWORDS = ['cold', 'iced', 'холодн', 'суық'];

  private readonly isHotDrinkItem = computed(() => {
    const category = this.item()?.categoryName?.toLowerCase() ?? '';
    return ItemDetailComponent.HOT_DRINK_KEYWORDS.some((k) => category.includes(k));
  });

  private readonly isColdDrinkItem = computed(() => {
    const category = this.item()?.categoryName?.toLowerCase() ?? '';
    return ItemDetailComponent.COLD_DRINK_KEYWORDS.some((k) => category.includes(k));
  });

  private readonly isDrinkItem = computed(() => this.isHotDrinkItem() || this.isColdDrinkItem());

  readonly modelSrc = computed(() => {
    if (this.isColdDrinkItem()) return 'iced-cup.glb';
    return this.isHotDrinkItem() ? 'cup.glb' : 'dish.glb';
  });

  readonly cupScale = computed(() => {
    // Dishes have no size variants, so they always fell through to the cup's
    // base scale — bump them up a bit since the dish model reads small at that size.
    if (!this.isDrinkItem()) return 0.95;
    const variants = this.item()?.variants ?? [];
    const sel = this.selectedVariant();
    if (!sel || !variants.length) return 0.76;
    const sorted = [...variants].sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0));
    const idx = sorted.findIndex(v => v.id === sel.id);
    return ([0.68, 0.76, 0.84] as const)[Math.min(Math.max(idx, 0), 2)] ?? 0.76;
  });

  // 3D viewer tint: milk color blended 45% toward syrup when a syrup is selected
  readonly liquidColor = computed((): string => {
    const milk = this.cupLiquidColor();        // e.g. '#c9a06e'
    const syrup = this.cupSyrupColor();        // null | '#b06f24' | '#e8cf9a'
    if (!syrup) return milk;
    const blend = (a: number, b: number, t: number) => Math.round(a + (b - a) * t);
    const parseHex = (h: string) => [
      parseInt(h.slice(1, 3), 16),
      parseInt(h.slice(3, 5), 16),
      parseInt(h.slice(5, 7), 16),
    ];
    const [mr, mg, mb] = parseHex(milk);
    const [sr, sg, sb] = parseHex(syrup);
    const toHex = (n: number) => n.toString(16).padStart(2, '0');
    return `#${toHex(blend(mr, sr, 0.45))}${toHex(blend(mg, sg, 0.45))}${toHex(blend(mb, sb, 0.45))}`;
  });

  readonly cupFillFraction = computed(() => {
    const variants = this.item()?.variants ?? [];
    const sel = this.selectedVariant();
    if (!sel || !variants.length) return 0.74;
    const idx = variants.findIndex(v => v.id === sel.id);
    return ([0.58, 0.74, 0.9] as const)[Math.min(Math.max(idx, 0), 2)] ?? 0.74;
  });

  readonly cupLiquidShift = computed(() =>
    Math.round(132 * (1 - this.cupFillFraction()))
  );

  readonly cupLiquidColor = computed(() => {
    const milk = this.item()?.availableToppings?.filter(t => t.group === 'milk') ?? [];
    const ids = this.selectedToppingIds();
    const sel = milk.find(t => ids.has(t.id));
    if (!sel) return '#c9a06e';
    const exact = this.MILK_COLORS[sel.name];
    if (exact) return exact;
    const partial = Object.keys(this.MILK_COLORS).find(k => sel.name.includes(k));
    return partial ? this.MILK_COLORS[partial] : this.MILK_PALETTE[milk.indexOf(sel) % this.MILK_PALETTE.length];
  });

  readonly cupSyrupColor = computed((): string | null => {
    const syrup = this.item()?.availableToppings?.filter(t => t.group === 'syrup') ?? [];
    const ids = this.selectedToppingIds();
    const sel = syrup.find(t => ids.has(t.id));
    if (!sel) return null;
    const color = this.SYRUP_COLORS[sel.name];
    return color && color !== '#f5f5f4' ? color : null;
  });

  readonly sugarCount = computed(() => {
    const sugar = this.item()?.availableToppings?.filter(t => t.group === 'sugar') ?? [];
    const ids = this.selectedToppingIds();
    const idx = sugar.findIndex(t => ids.has(t.id));
    return idx === -1 ? 0 : idx + 1;
  });

  readonly hasCream = computed(() => {
    const toppings = this.item()?.availableToppings ?? [];
    const ids = this.selectedToppingIds();
    return toppings.some(t => ids.has(t.id) && /сливк|cream/i.test(t.name));
  });

  // Sugar cube positions (pre-computed to avoid Math.floor in template)
  readonly SUGAR_CUBE_POS = [
    { x: 82, y: 176, cx: 88.5, cy: 182.5 },
    { x: 108, y: 176, cx: 114.5, cy: 182.5 },
    { x: 82, y: 154, cx: 88.5, cy: 160.5 },
    { x: 108, y: 154, cx: 114.5, cy: 160.5 },
  ] as const;

  // Pop animation for footer price
  pricePop = signal(false);
  private _popTimer: ReturnType<typeof setTimeout> | undefined;

  // ── Lifecycle ────────────────────────────────────────────────────────────────
  constructor() {
    // Reload on language change (existing behavior)
    effect(() => {
      const lang = this.langService.lang();
      if (this.id) this.loadItem(lang);
    });
    // Price pop animation — runs in setTimeout so signal writes happen outside effect
    effect(() => {
      this.lineTotal(); // track
      clearTimeout(this._popTimer);
      this._popTimer = setTimeout(() => {
        this.pricePop.set(false);
        this._popTimer = setTimeout(() => {
          this.pricePop.set(true);
          this._popTimer = setTimeout(() => this.pricePop.set(false), 380);
        }, 10);
      }, 0);
    });
  }

  // ── Existing methods (unchanged) ─────────────────────────────────────────────
  onImageError(): void { this.imageError.set(true); }

  reload(): void { this.loadItem(this.langService.lang()); }

  private loadItem(lang: string): void {
    this.loading.set(true);
    const locationId = this.locationSvc.locationId() || undefined;
    this.api.getMenuItem(this.id, lang, locationId).subscribe({
      next: (item) => {
        this.item.set(item);
        const def = item.variants?.find((v) => v.isDefault) ?? item.variants?.[0] ?? null;
        this.selectedVariant.set(def);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  ngOnInit(): void {
    this.api.getFavorites().subscribe({
      next: (favs) => this.isFav.set(favs.some((f) => f.id === this.id)),
      error: () => {},
    });
  }

  get toppings(): MenuTopping[] {
    return this.item()?.availableToppings ?? [];
  }

  get groupedToppings(): { label: string; toppings: MenuTopping[] }[] {
    const groupMap = new Map<string, MenuTopping[]>();
    const extras: MenuTopping[] = [];
    for (const t of this.toppings) {
      if (t.group) {
        if (!groupMap.has(t.group)) groupMap.set(t.group, []);
        groupMap.get(t.group)!.push(t);
      } else {
        extras.push(t);
      }
    }
    const result: { label: string; toppings: MenuTopping[] }[] = [];
    groupMap.forEach((toppings, key) =>
      result.push({ label: this.langService.t(`topping.${key}`), toppings })
    );
    if (extras.length) result.push({ label: this.langService.t('topping.extras'), toppings: extras });
    return result;
  }

  get sugarToppings(): MenuTopping[] {
    return this.toppings.filter(t => t.group === 'sugar');
  }

  isToppingSelected(id: string): boolean {
    return this.selectedToppingIds().has(id);
  }

  toggleTopping(topping: MenuTopping): void {
    this.selectedToppingIds.update((set) => {
      const next = new Set(set);
      if (next.has(topping.id)) {
        next.delete(topping.id);
      } else {
        if (topping.group) {
          for (const t of this.toppings) {
            if (t.group === topping.group) next.delete(t.id);
          }
        }
        next.add(topping.id);
      }
      return next;
    });
  }

  // Sugar stepper (maps to existing toggleTopping radio logic)
  incrementSugar(): void {
    const sugar = this.sugarToppings;
    const count = this.sugarCount();
    if (count < sugar.length) this.toggleTopping(sugar[count]);
  }

  decrementSugar(): void {
    const sugar = this.sugarToppings;
    const count = this.sugarCount();
    if (count === 0) return;
    if (count === 1) this.toggleTopping(sugar[0]);
    else this.toggleTopping(sugar[count - 2]);
  }

  incrementQty(): void { this.quantity.update((q) => q + 1); }
  decrementQty(): void { if (this.quantity() > 1) this.quantity.update((q) => q - 1); }

  addToCart(): void {
    if (!this.auth.isLoggedIn) {
      this.router.navigate(['/auth/login']);
      return;
    }
    if (!this.storeStatus.guardOpen()) return;
    const item = this.item();
    if (!item) return;
    const variant = this.selectedVariant();
    if (item.variants?.length && !variant) {
      this.toast.warning('Please select a size');
      return;
    }
    const selectedToppings = (item.availableToppings ?? [])
      .filter((t) => this.selectedToppingIds().has(t.id))
      .map<OrderItemToppingInput>((t) => ({ toppingId: t.id, toppingName: t.name, price: t.price }));
    this.cart.addItem({
      menuItemId: item.id,
      name: item.name,
      price: this.effectivePrice(),
      quantity: this.quantity(),
      imageUrl: item.imageUrl,
      selectedToppings: selectedToppings.length ? selectedToppings : undefined,
      variantId: variant?.id,
      variantLabel: variant?.label,
    });
    this.toast.success(`${this.quantity()}x ${item.name} added to cart`);
    this.router.navigate(['/menu']);
  }

  toggleFavorite(): void {
    if (this.isFav()) {
      this.api.removeFavorite(this.id).subscribe(() => {
        this.isFav.set(false);
        this.toast.info('Removed from favorites');
      });
    } else {
      this.api.addFavorite(this.id).subscribe(() => {
        this.isFav.set(true);
        this.toast.success('Added to favorites ♥');
      });
    }
  }

  goBack(): void { this.router.navigate(['/menu']); }
}
