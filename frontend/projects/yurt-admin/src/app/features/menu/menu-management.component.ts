import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CdkDropList, CdkDrag, CdkDragHandle, CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { ModuleService, YurtApiService } from 'shared-api';
import { ALLERGEN_CODES, MenuItem, MenuCategory, MenuTopping, OptionGroupRule, Location, MenuItemVariant, IikoNomenclatureProduct } from 'shared-models';
import { ButtonComponent, ToastService, Currency2Pipe } from 'shared-ui';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { PictureUploadComponent } from '../../shared/picture-upload/picture-upload.component';
import { ConfirmService } from '../../shared/confirm-dialog/confirm.service';
import { MenuImportComponent } from './menu-import.component';

interface VariantRow {
  label: string;
  labelRu: string;
  labelKk: string;
  price: number;
  sortOrder: number;
  isDefault: boolean;
  iikoProductSizeId: string;
}

interface MenuItemForm {
  id?: string;
  name: string;
  nameRu: string;
  nameKk: string;
  description: string;
  descriptionRu: string;
  descriptionKk: string;
  price: number;
  categoryId: string;
  imageUrl: string;
  isAvailable: boolean;
  isNew: boolean;
  barcode: string;
  soldByWeight: boolean;
  locationIds: string[];
  toppingIds: string[];
  locationPrices: Record<string, number | null>;
  variants: VariantRow[];
  iikoProductId: string;
  iikoProductSizeId: string;
  calories: number | null;
  protein: number | null;
  fat: number | null;
  carbs: number | null;
  weightGrams: number | null;
  allergens: string[];
}

interface ToppingForm {
  id?: string;
  name: string;
  nameRu: string;
  nameKk: string;
  price: number;
  isAvailable: boolean;
  categoryIds: string[];
  group: string;
  locationPrices: Record<string, number | null>;
}

/** One row of the "Rules for choices" panel. */
interface RuleRow {
  key: string;
  title: string;
  titleRu: string;
  titleKk: string;
  required: boolean;
  minSelect: number;
  maxSelect: number | null;
  saved: boolean;
}

interface CatInput {
  id?: string;
  name: string;
  nameRu: string;
  nameKk: string;
}

@Component({
  selector: 'app-menu-management',
  standalone: true,
  imports: [CommonModule, FormsModule, CdkDropList, CdkDrag, CdkDragHandle, ButtonComponent, Currency2Pipe, AdminTranslatePipe, MenuImportComponent, PictureUploadComponent],
  templateUrl: './menu-management.component.html',
  styleUrl: './menu-management.component.css',
})
export class MenuManagementComponent implements OnInit {
  private api = inject(YurtApiService);
  readonly modules = inject(ModuleService);
  private toast = inject(ToastService);
  private confirmSvc = inject(ConfirmService);
  readonly langService = inject(AdminLangService);

  activeTab = signal<'items' | 'toppings'>('items');
  mobileCatOpen = signal(false);

  items = signal<MenuItem[]>([]);
  categories = signal<MenuCategory[]>([]);
  toppings = signal<MenuTopping[]>([]);
  optionGroups = signal<OptionGroupRule[]>([]);
  ruleRows = signal<RuleRow[]>([]);
  locations = signal<Location[]>([]);
  loading = signal(true);
  showImport = signal(false);
  saving = signal(false);
  showItemDialog = signal(false);
  showCatDialog = signal(false);
  showToppingDialog = signal(false);
  selectedCategoryId = signal('');
  catInput = signal<CatInput>({ name: '', nameRu: '', nameKk: '' });

  itemForm = signal<MenuItemForm>({
    name: '', nameRu: '', nameKk: '',
    description: '', descriptionRu: '', descriptionKk: '',
    price: 0, categoryId: '', imageUrl: '', isAvailable: true, isNew: false, barcode: '', soldByWeight: false, locationIds: [], toppingIds: [], locationPrices: {}, variants: [],
    calories: null, protein: null, fat: null, carbs: null, weightGrams: null, allergens: [],
    iikoProductId: '', iikoProductSizeId: '',
  });

  // iiko product catalog for the mapping dropdowns (Phase 3 order-push feature).
  iikoNomenclature = signal<IikoNomenclatureProduct[]>([]);

  selectedIikoProduct = computed(() =>
    this.iikoNomenclature().find((p) => p.productId === this.itemForm().iikoProductId));

  toppingForm = signal<ToppingForm>({
    name: '', nameRu: '', nameKk: '',
    price: 0, isAvailable: true, categoryIds: [], group: '', locationPrices: {},
  });

  itemSearch = signal('');
  statusFilter = signal<'all' | 'hidden' | 'noPhoto' | 'noPrice'>('all');
  toppingSearch = signal('');

  filteredItems = computed(() => {
    const catId = this.selectedCategoryId();
    const q = this.itemSearch().toLowerCase().trim();
    let list = catId ? this.items().filter((i) => i.categoryId === catId) : this.items();
    if (q) list = list.filter((i) =>
      i.name.toLowerCase().includes(q) ||
      (i.nameRu ?? '').toLowerCase().includes(q) ||
      (i.nameKk ?? '').toLowerCase().includes(q));
    const f = this.statusFilter();
    if (f === 'hidden') list = list.filter((i) => !i.isAvailable);
    else if (f === 'noPhoto') list = list.filter((i) => !i.imageUrl);
    else if (f === 'noPrice') list = list.filter((i) => !i.price && !(i.variants?.length));
    return list;
  });

  /** Flip a dish's availability right in the list, without opening the editor. */
  toggleAvailable(item: MenuItem): void {
    const next = !item.isAvailable;
    this.items.update((list) => list.map((i) => (i.id === item.id ? { ...i, isAvailable: next } : i)));
    // The server replaces the whole dish on save, so send everything the list already knows.
    const payload = {
      name: item.name, nameRu: item.nameRu, nameKk: item.nameKk,
      description: item.description || undefined, descriptionRu: item.descriptionRu, descriptionKk: item.descriptionKk,
      price: item.price, categoryId: item.categoryId, imageUrl: item.imageUrl,
      isAvailable: next, isNew: item.isNew, barcode: item.barcode ?? null, soldByWeight: item.soldByWeight ?? false,
      locationIds: item.locationIds ?? [], toppingIds: item.toppingIds ?? [],
      locationPrices: item.locationPrices ?? [],
      variants: item.variants?.length ? item.variants : undefined,
      iikoProductId: item.iikoProductId ?? undefined, iikoProductSizeId: item.iikoProductSizeId ?? undefined,
      nutrition: item.nutrition ?? undefined,
    } as Partial<MenuItem>;
    this.api.adminUpdateMenuItem(item.id, payload).subscribe({
      next: (saved) => this.items.update((list) => list.map((i) => (i.id === saved.id ? saved : i))),
      error: () => this.items.update((list) => list.map((i) => (i.id === item.id ? { ...i, isAvailable: !next } : i))),
    });
  }

  filteredToppings = computed(() => {
    const q = this.toppingSearch().toLowerCase().trim();
    if (!q) return this.toppings();
    return this.toppings().filter((t) =>
      t.name.toLowerCase().includes(q) ||
      (t.nameRu ?? '').toLowerCase().includes(q) ||
      (t.nameKk ?? '').toLowerCase().includes(q) ||
      (t.group ?? '').toLowerCase().includes(q));
  });

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.loading.set(true);
    this.api.adminGetCategories().subscribe((cats) => this.categories.set(cats));
    this.api.adminGetMenuItems().subscribe({
      next: (items) => { this.items.set(items); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
    this.api.adminGetToppings().subscribe((toppings) => { this.toppings.set(toppings); this.buildRuleRows(); });
    this.api.adminGetOptionGroups().subscribe({ next: (g) => { this.optionGroups.set(g); this.buildRuleRows(); }, error: () => {} });
    this.api.getAdminLocations().subscribe((locs) => this.locations.set(locs));
    if (this.modules.isEnabled('iiko_sync')) {
      this.api.getIikoNomenclature().subscribe({
        next: (products) => this.iikoNomenclature.set(products),
        error: () => this.iikoNomenclature.set([]), // iiko disabled/unreachable — mapping picker just stays empty
      });
    }
  }

  localizedCatName(cat: MenuCategory): string {
    const lang = this.langService.lang();
    if (lang === 'ru') return cat.nameRu || cat.name;
    if (lang === 'kk') return cat.nameKk || cat.nameRu || cat.name;
    return cat.name;
  }

  localizedItemName(item: MenuItem): string {
    const lang = this.langService.lang();
    if (lang === 'ru') return item.nameRu || item.name;
    if (lang === 'kk') return item.nameKk || item.nameRu || item.name;
    return item.name;
  }

  localizedToppingName(topping: MenuTopping): string {
    const lang = this.langService.lang();
    if (lang === 'ru') return topping.nameRu || topping.name;
    if (lang === 'kk') return topping.nameKk || topping.nameRu || topping.name;
    return topping.name;
  }

  categoryName(id: string): string {
    const cat = this.categories().find((c) => c.id === id);
    return cat ? this.localizedCatName(cat) : '';
  }

  // Items can only be reordered while viewing a single, unfiltered category — otherwise
  // the visible row order doesn't correspond 1:1 to that category's backend order.
  itemsReorderable(): boolean {
    return !!this.selectedCategoryId() && !this.itemSearch().trim();
  }

  dropCategory(event: CdkDragDrop<MenuCategory[]>): void {
    if (event.previousIndex === event.currentIndex) return;
    const cats = [...this.categories()];
    moveItemInArray(cats, event.previousIndex, event.currentIndex);
    this.reorderCategories(cats);
  }

  moveCategory(index: number, direction: -1 | 1): void {
    const target = index + direction;
    const cats = [...this.categories()];
    if (target < 0 || target >= cats.length) return;
    [cats[index], cats[target]] = [cats[target], cats[index]];
    this.reorderCategories(cats);
  }

  private reorderCategories(cats: MenuCategory[]): void {
    this.categories.set(cats);
    this.api.adminReorderCategories(cats.map((c) => c.id)).subscribe({
      error: () => this.loadAll(),
    });
  }

  dropItem(event: CdkDragDrop<MenuItem[]>): void {
    if (event.previousIndex === event.currentIndex) return;
    const catItems = [...this.filteredItems()];
    moveItemInArray(catItems, event.previousIndex, event.currentIndex);
    this.reorderItems(catItems);
  }

  moveItem(index: number, direction: -1 | 1): void {
    const target = index + direction;
    const catItems = [...this.filteredItems()];
    if (target < 0 || target >= catItems.length) return;
    [catItems[index], catItems[target]] = [catItems[target], catItems[index]];
    this.reorderItems(catItems);
  }

  private reorderItems(catItems: MenuItem[]): void {
    const catId = this.selectedCategoryId();
    if (!catId) return;
    const queue = [...catItems];
    this.items.update((all) => all.map((i) => (i.categoryId === catId ? queue.shift()! : i)));
    this.api.adminReorderMenuItems(catId, catItems.map((i) => i.id)).subscribe({
      error: () => this.loadAll(),
    });
  }

  toppingCategoryNames(categoryIds: string[]): string {
    return categoryIds.map((id) => this.categoryName(id)).filter(Boolean).join(', ') || '—';
  }

  openItemDialog(item?: MenuItem): void {
    this.itemForm.set({
      id: item?.id,
      name: item?.name ?? '',
      nameRu: item?.nameRu ?? '',
      nameKk: item?.nameKk ?? '',
      description: item?.description ?? '',
      descriptionRu: item?.descriptionRu ?? '',
      descriptionKk: item?.descriptionKk ?? '',
      price: item?.price ?? 0,
      categoryId: item?.categoryId ?? (this.selectedCategoryId() || this.categories()[0]?.id) ?? '',
      imageUrl: item?.imageUrl ?? '',
      isAvailable: item?.isAvailable ?? true,
      isNew: item?.isNew ?? false,
      barcode: item?.barcode ?? '',
      soldByWeight: item?.soldByWeight ?? false,
      locationIds: item?.locationIds ? [...item.locationIds] : [],
      toppingIds: item?.toppingIds ? [...item.toppingIds] : [],
      locationPrices: Object.fromEntries((item?.locationPrices ?? []).map((p) => [p.locationId, p.price])),
      variants: item?.variants
        ? item.variants.map((v: MenuItemVariant) => ({
            label: v.label, labelRu: (v as any).labelRu ?? '', labelKk: (v as any).labelKk ?? '',
            price: v.price, sortOrder: v.sortOrder, isDefault: v.isDefault,
            iikoProductSizeId: v.iikoProductSizeId ?? '',
          }))
        : [],
      iikoProductId: item?.iikoProductId ?? '',
      iikoProductSizeId: item?.iikoProductSizeId ?? '',
      calories: item?.nutrition?.calories ?? null,
      protein: item?.nutrition?.protein ?? null,
      fat: item?.nutrition?.fat ?? null,
      carbs: item?.nutrition?.carbs ?? null,
      weightGrams: item?.nutrition?.weightGrams ?? null,
      allergens: [...(item?.nutrition?.allergens ?? [])],
    });
    this.showItemDialog.set(true);
  }

  addVariantRow(): void {
    this.itemForm.update((f) => ({
      ...f,
      variants: [
        ...f.variants,
        { label: '', labelRu: '', labelKk: '', price: 0, sortOrder: f.variants.length, isDefault: f.variants.length === 0, iikoProductSizeId: '' },
      ],
    }));
  }

  // iiko sizes belong to the item's selected product — switching product invalidates
  // any size already picked (on the item itself, and on every variant row).
  onIikoProductChange(productId: string): void {
    this.itemForm.update((f) => ({
      ...f,
      iikoProductId: productId,
      iikoProductSizeId: '',
      variants: f.variants.map((v) => ({ ...v, iikoProductSizeId: '' })),
    }));
  }

  removeVariantRow(index: number): void {
    this.itemForm.update((f) => {
      const variants = f.variants.filter((_, i) => i !== index)
        .map((v, i) => ({ ...v, sortOrder: i }));
      if (variants.length > 0 && !variants.some((v) => v.isDefault)) variants[0].isDefault = true;
      return { ...f, variants };
    });
  }

  setVariantDefault(index: number): void {
    this.itemForm.update((f) => ({
      ...f,
      variants: f.variants.map((v, i) => ({ ...v, isDefault: i === index })),
    }));
  }

  patchVariantRow(index: number, field: keyof VariantRow, value: unknown): void {
    this.itemForm.update((f) => ({
      ...f,
      variants: f.variants.map((v, i) => (i === index ? { ...v, [field]: value } : v)),
    }));
  }

  // ── Options per dish and prices per branch ──
  isItemToppingSelected(id: string): boolean { return this.itemForm().toppingIds.includes(id); }

  toggleItemTopping(id: string): void {
    this.itemForm.update((f) => ({
      ...f,
      toppingIds: f.toppingIds.includes(id) ? f.toppingIds.filter((x) => x !== id) : [...f.toppingIds, id],
    }));
  }

  /** The prices typed in, as a list; empty boxes mean "usual price". */
  private priceList(prices: Record<string, number | null>): { locationId: string; price: number }[] {
    return Object.entries(prices)
      .filter(([, v]) => v !== null && v !== undefined && `${v}` !== '' && Number(v) >= 0)
      .map(([locationId, v]) => ({ locationId, price: Number(v) }));
  }

  setItemBranchPrice(locId: string, value: number | null): void {
    this.itemForm.update((f) => ({ ...f, locationPrices: { ...f.locationPrices, [locId]: value } }));
  }

  setToppingBranchPrice(locId: string, value: number | null): void {
    this.toppingForm.update((f) => ({ ...f, locationPrices: { ...f.locationPrices, [locId]: value } }));
  }

  // ── Rules for choices ──
  private buildRuleRows(): void {
    const saved = new Map(this.optionGroups().map((g) => [g.key, g]));
    const keys = new Set<string>([...saved.keys(), ...this.toppings().map((t) => t.group).filter((g): g is string => !!g)]);
    this.ruleRows.set([...keys].sort().map((key) => {
      const g = saved.get(key);
      return {
        key, saved: !!g,
        title: g?.title ?? '', titleRu: g?.titleRu ?? '', titleKk: g?.titleKk ?? '',
        required: g?.required ?? false, minSelect: g?.minSelect ?? 0, maxSelect: g ? g.maxSelect : 1,
      };
    }));
  }

  patchRule(key: string, field: keyof RuleRow, value: unknown): void {
    this.ruleRows.update((rows) => rows.map((r) => (r.key === key ? { ...r, [field]: value } : r)));
  }

  saveRule(row: RuleRow): void {
    const max = row.maxSelect === null || `${row.maxSelect}` === '' ? null : Number(row.maxSelect);
    this.api.adminSaveOptionGroup({
      key: row.key, title: row.title || null, titleRu: row.titleRu || null, titleKk: row.titleKk || null,
      required: row.required, minSelect: Number(row.minSelect) || 0, maxSelect: max,
    }).subscribe({
      next: (saved) => {
        this.optionGroups.update((l) => [...l.filter((g) => g.key !== saved.key), saved]);
        this.buildRuleRows();
        this.toast.success(this.langService.t('menu.ruleSaved'));
      },
    });
  }

  resetRule(row: RuleRow): void {
    this.api.adminDeleteOptionGroup(row.key).subscribe({
      next: () => {
        this.optionGroups.update((l) => l.filter((g) => g.key !== row.key));
        this.buildRuleRows();
        this.toast.success(this.langService.t('menu.ruleSaved'));
      },
    });
  }

  isItemLocationSelected(locId: string): boolean {
    return this.itemForm().locationIds.includes(locId);
  }

  toggleItemLocation(locId: string): void {
    this.itemForm.update((f) => {
      const ids = f.locationIds.includes(locId)
        ? f.locationIds.filter((id) => id !== locId)
        : [...f.locationIds, locId];
      return { ...f, locationIds: ids };
    });
  }

  readonly allergenCodes = ALLERGEN_CODES;
  readonly nutritionFields: { key: 'calories' | 'protein' | 'fat' | 'carbs' | 'weightGrams'; label: string; step: number }[] = [
    { key: 'calories', label: 'menu.calories', step: 1 },
    { key: 'protein', label: 'menu.protein', step: 0.1 },
    { key: 'fat', label: 'menu.fat', step: 0.1 },
    { key: 'carbs', label: 'menu.carbs', step: 0.1 },
    { key: 'weightGrams', label: 'menu.weight', step: 1 },
  ];

  /** Tick or untick one allergen of the dish being edited. */
  toggleAllergen(code: string, on: boolean): void {
    this.itemForm.update((f) => ({
      ...f, allergens: on ? [...new Set([...f.allergens, code])] : f.allergens.filter((a) => a !== code),
    }));
  }

  patchItemForm(field: keyof MenuItemForm, value: unknown): void {
    this.itemForm.update((f) => ({ ...f, [field]: value }));
  }

  saveItem(): void {
    const f = this.itemForm();
    if (!f.name || !f.nameRu || !f.nameKk) {
      this.toast.error(this.langService.t('menu.nameAll3'));
      return;
    }
    if (!f.price && f.variants.length === 0) {
      this.toast.error(this.langService.t('menu.priceOrSizes'));
      return;
    }
    if (f.soldByWeight && f.variants.length > 0) {
      this.toast.error(this.langService.t('menu.weightNoSizes'));
      return;
    }
    if (f.variants.some((v) => !v.label || !v.price)) {
      this.toast.error(this.langService.t('menu.sizeIncomplete'));
      return;
    }
    this.saving.set(true);
    const payload = {
      name: f.name,
      nameRu: f.nameRu,
      nameKk: f.nameKk,
      description: f.description || undefined,
      descriptionRu: f.descriptionRu || undefined,
      descriptionKk: f.descriptionKk || undefined,
      price: f.price,
      categoryId: f.categoryId,
      imageUrl: f.imageUrl || undefined,
      isAvailable: f.isAvailable,
      isNew: f.isNew,
      barcode: f.barcode.trim() || null,
      soldByWeight: f.soldByWeight,
      locationIds: f.locationIds,
      toppingIds: f.toppingIds,
      locationPrices: this.priceList(f.locationPrices),
      variants: f.variants.length > 0
        ? f.variants.map((v, i) => ({
            label: v.label, labelRu: v.labelRu || undefined, labelKk: v.labelKk || undefined,
            price: v.price, sortOrder: i, isDefault: v.isDefault,
            iikoProductSizeId: v.iikoProductSizeId || undefined,
          }))
        : undefined,
      iikoProductId: f.iikoProductId || undefined,
      iikoProductSizeId: f.iikoProductSizeId || undefined,
      nutrition: {
        calories: f.calories, protein: f.protein, fat: f.fat, carbs: f.carbs, weightGrams: f.weightGrams,
        allergens: f.allergens,
      },
    };
    const obs = f.id ? this.api.adminUpdateMenuItem(f.id, payload) : this.api.adminCreateMenuItem(payload);
    obs.subscribe({
      next: (item) => {
        if (f.id) {
          this.items.update((list) => list.map((i) => (i.id === item.id ? item : i)));
        } else {
          this.items.update((list) => [...list, item]);
        }
        this.saving.set(false);
        this.showItemDialog.set(false);
        this.toast.success(this.langService.t('menu.itemSaved'));
      },
      error: () => this.saving.set(false),
    });
  }

  async deleteItem(item: MenuItem): Promise<void> {
    if (!await this.confirmSvc.confirm(this.langService.t('menu.deleteItemTitle'), this.langService.t('common.deleteNamed', { name: item.name }))) return;
    this.api.adminDeleteMenuItem(item.id).subscribe({
      next: () => {
        this.items.update((list) => list.filter((i) => i.id !== item.id));
        this.toast.success(this.langService.t('menu.itemDeleted'));
      },
      error: () => {},
    });
  }

  saveCat(): void {
    const f = this.catInput();
    if (!f.name.trim() || !f.nameRu.trim() || !f.nameKk.trim()) {
      this.toast.error(this.langService.t('menu.catNameAll3'));
      return;
    }
    this.saving.set(true);
    const payload = {
      name: f.name.trim(),
      nameRu: f.nameRu.trim(),
      nameKk: f.nameKk.trim(),
    };
    const obs = f.id
      ? this.api.adminUpdateCategory(f.id, payload)
      : this.api.adminCreateCategory(payload);
    obs.subscribe({
      next: (cat) => {
        if (f.id) {
          this.categories.update((list) => list.map((c) => (c.id === cat.id ? cat : c)));
        } else {
          this.categories.update((list) => [...list, cat]);
        }
        this.saving.set(false);
        this.showCatDialog.set(false);
        this.toast.success(f.id ? this.langService.t('menu.catUpdated') : this.langService.t('menu.catCreated'));
      },
      error: () => this.saving.set(false),
    });
  }

  async deleteCat(cat: MenuCategory): Promise<void> {
    if (!await this.confirmSvc.confirm(this.langService.t('menu.deleteCatTitle'), this.langService.t('menu.deleteCatNamed', { name: this.localizedCatName(cat) }))) return;
    this.api.adminDeleteCategory(cat.id).subscribe({
      next: () => {
        this.categories.update((list) => list.filter((c) => c.id !== cat.id));
        if (this.selectedCategoryId() === cat.id) this.selectedCategoryId.set('');
        this.toast.success(this.langService.t('menu.catDeleted'));
      },
      error: () => {},
    });
  }

  openCatDialog(cat?: MenuCategory): void {
    this.catInput.set({
      id: cat?.id,
      name: cat?.name ?? '',
      nameRu: cat?.nameRu ?? '',
      nameKk: cat?.nameKk ?? '',
    });
    this.showCatDialog.set(true);
  }

  openToppingDialog(topping?: MenuTopping): void {
    this.toppingForm.set({
      id: topping?.id,
      name: topping?.name ?? '',
      nameRu: topping?.nameRu ?? '',
      nameKk: topping?.nameKk ?? '',
      price: topping?.price ?? 0,
      isAvailable: topping?.isAvailable ?? true,
      categoryIds: topping?.categoryIds ? [...topping.categoryIds] : [],
      group: topping?.group ?? '',
      locationPrices: Object.fromEntries((topping?.locationPrices ?? []).map((p) => [p.locationId, p.price])),
    });
    this.showToppingDialog.set(true);
  }

  isToppingCategorySelected(catId: string): boolean {
    return this.toppingForm().categoryIds.includes(catId);
  }

  toggleToppingCategory(catId: string): void {
    this.toppingForm.update((f) => {
      const ids = f.categoryIds.includes(catId)
        ? f.categoryIds.filter((id) => id !== catId)
        : [...f.categoryIds, catId];
      return { ...f, categoryIds: ids };
    });
  }

  patchToppingForm(field: keyof ToppingForm, value: unknown): void {
    this.toppingForm.update((f) => ({ ...f, [field]: value }));
  }

  saveTopping(): void {
    const f = this.toppingForm();
    if (!f.name || !f.nameRu || !f.nameKk) {
      this.toast.error(this.langService.t('menu.toppingNameAll3'));
      return;
    }
    this.saving.set(true);
    const payload = {
      name: f.name,
      nameRu: f.nameRu,
      nameKk: f.nameKk,
      price: f.price,
      isAvailable: f.isAvailable,
      categoryIds: f.categoryIds,
      group: f.group || undefined,
      locationPrices: this.priceList(f.locationPrices),
    };
    const obs = f.id ? this.api.adminUpdateTopping(f.id, payload) : this.api.adminCreateTopping(payload);
    obs.subscribe({
      next: (topping) => {
        if (f.id) {
          this.toppings.update((list) => list.map((t) => (t.id === topping.id ? topping : t)));
        } else {
          this.toppings.update((list) => [...list, topping]);
        }
        this.saving.set(false);
        this.showToppingDialog.set(false);
        this.buildRuleRows();
        this.toast.success(this.langService.t('menu.toppingSaved'));
      },
      error: () => this.saving.set(false),
    });
  }

  async deleteTopping(topping: MenuTopping): Promise<void> {
    if (!await this.confirmSvc.confirm(this.langService.t('menu.deleteToppingTitle'), this.langService.t('menu.deleteToppingNamed', { name: topping.name }))) return;
    this.api.adminDeleteTopping(topping.id).subscribe({
      next: () => {
        this.toppings.update((list) => list.filter((t) => t.id !== topping.id));
        this.toast.success(this.langService.t('menu.toppingDeleted'));
      },
      error: () => {},
    });
  }
}
