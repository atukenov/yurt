import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CdkDropList, CdkDrag, CdkDragHandle, CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { YurtApiService } from 'shared-api';
import { MenuItem, MenuCategory, MenuTopping, Location, MenuItemVariant, IikoNomenclatureProduct } from 'shared-models';
import { ButtonComponent, ToastService, Currency2Pipe } from 'shared-ui';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { ConfirmService } from '../../shared/confirm-dialog/confirm.service';

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
  locationIds: string[];
  variants: VariantRow[];
  iikoProductId: string;
  iikoProductSizeId: string;
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
  imports: [CommonModule, FormsModule, CdkDropList, CdkDrag, CdkDragHandle, ButtonComponent, Currency2Pipe, AdminTranslatePipe],
  templateUrl: './menu-management.component.html',
  styleUrl: './menu-management.component.css',
})
export class MenuManagementComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private confirmSvc = inject(ConfirmService);
  readonly langService = inject(AdminLangService);

  activeTab = signal<'items' | 'toppings'>('items');
  mobileCatOpen = signal(false);

  items = signal<MenuItem[]>([]);
  categories = signal<MenuCategory[]>([]);
  toppings = signal<MenuTopping[]>([]);
  locations = signal<Location[]>([]);
  loading = signal(true);
  saving = signal(false);
  showItemDialog = signal(false);
  showCatDialog = signal(false);
  showToppingDialog = signal(false);
  selectedCategoryId = signal('');
  catInput = signal<CatInput>({ name: '', nameRu: '', nameKk: '' });

  itemForm = signal<MenuItemForm>({
    name: '', nameRu: '', nameKk: '',
    description: '', descriptionRu: '', descriptionKk: '',
    price: 0, categoryId: '', imageUrl: '', isAvailable: true, isNew: false, locationIds: [], variants: [],
    iikoProductId: '', iikoProductSizeId: '',
  });

  // iiko product catalog for the mapping dropdowns (Phase 3 order-push feature).
  iikoNomenclature = signal<IikoNomenclatureProduct[]>([]);

  selectedIikoProduct = computed(() =>
    this.iikoNomenclature().find((p) => p.productId === this.itemForm().iikoProductId));

  toppingForm = signal<ToppingForm>({
    name: '', nameRu: '', nameKk: '',
    price: 0, isAvailable: true, categoryIds: [], group: '',
  });

  itemSearch = signal('');
  toppingSearch = signal('');

  filteredItems = computed(() => {
    const catId = this.selectedCategoryId();
    const q = this.itemSearch().toLowerCase().trim();
    let list = catId ? this.items().filter((i) => i.categoryId === catId) : this.items();
    if (q) list = list.filter((i) =>
      i.name.toLowerCase().includes(q) ||
      (i.nameRu ?? '').toLowerCase().includes(q) ||
      (i.nameKk ?? '').toLowerCase().includes(q));
    return list;
  });

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
    this.api.adminGetToppings().subscribe((toppings) => this.toppings.set(toppings));
    this.api.getAdminLocations().subscribe((locs) => this.locations.set(locs));
    this.api.getIikoNomenclature().subscribe({
      next: (products) => this.iikoNomenclature.set(products),
      error: () => this.iikoNomenclature.set([]), // iiko disabled/unreachable — mapping picker just stays empty
    });
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
      locationIds: item?.locationIds ? [...item.locationIds] : [],
      variants: item?.variants
        ? item.variants.map((v: MenuItemVariant) => ({
            label: v.label, labelRu: (v as any).labelRu ?? '', labelKk: (v as any).labelKk ?? '',
            price: v.price, sortOrder: v.sortOrder, isDefault: v.isDefault,
            iikoProductSizeId: v.iikoProductSizeId ?? '',
          }))
        : [],
      iikoProductId: item?.iikoProductId ?? '',
      iikoProductSizeId: item?.iikoProductSizeId ?? '',
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

  patchItemForm(field: keyof MenuItemForm, value: unknown): void {
    this.itemForm.update((f) => ({ ...f, [field]: value }));
  }

  saveItem(): void {
    const f = this.itemForm();
    if (!f.name || !f.nameRu || !f.nameKk) {
      this.toast.error('Name in all 3 languages is required (EN, RU, KZ)');
      return;
    }
    if (!f.price && f.variants.length === 0) {
      this.toast.error('Enter a price or add size variants');
      return;
    }
    if (f.variants.some((v) => !v.label || !v.price)) {
      this.toast.error('Each size variant must have a label and price');
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
      locationIds: f.locationIds,
      variants: f.variants.length > 0
        ? f.variants.map((v, i) => ({
            label: v.label, labelRu: v.labelRu || undefined, labelKk: v.labelKk || undefined,
            price: v.price, sortOrder: i, isDefault: v.isDefault,
            iikoProductSizeId: v.iikoProductSizeId || undefined,
          }))
        : undefined,
      iikoProductId: f.iikoProductId || undefined,
      iikoProductSizeId: f.iikoProductSizeId || undefined,
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
        this.toast.success('Item saved');
      },
      error: () => this.saving.set(false),
    });
  }

  async deleteItem(item: MenuItem): Promise<void> {
    if (!await this.confirmSvc.confirm('Delete Item', `Delete "${item.name}"?`)) return;
    this.api.adminDeleteMenuItem(item.id).subscribe({
      next: () => {
        this.items.update((list) => list.filter((i) => i.id !== item.id));
        this.toast.success('Item deleted');
      },
      error: () => {},
    });
  }

  saveCat(): void {
    const f = this.catInput();
    if (!f.name.trim() || !f.nameRu.trim() || !f.nameKk.trim()) {
      this.toast.error('Category name in all 3 languages is required (EN, RU, KZ)');
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
        this.toast.success(f.id ? 'Category updated' : 'Category created');
      },
      error: () => this.saving.set(false),
    });
  }

  async deleteCat(cat: MenuCategory): Promise<void> {
    if (!await this.confirmSvc.confirm('Delete Category', `Delete category "${this.localizedCatName(cat)}"?`)) return;
    this.api.adminDeleteCategory(cat.id).subscribe({
      next: () => {
        this.categories.update((list) => list.filter((c) => c.id !== cat.id));
        if (this.selectedCategoryId() === cat.id) this.selectedCategoryId.set('');
        this.toast.success('Category deleted');
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
      this.toast.error('Topping name in all 3 languages is required (EN, RU, KZ)');
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
        this.toast.success('Topping saved');
      },
      error: () => this.saving.set(false),
    });
  }

  async deleteTopping(topping: MenuTopping): Promise<void> {
    if (!await this.confirmSvc.confirm('Delete Topping', `Delete topping "${topping.name}"?`)) return;
    this.api.adminDeleteTopping(topping.id).subscribe({
      next: () => {
        this.toppings.update((list) => list.filter((t) => t.id !== topping.id));
        this.toast.success('Topping deleted');
      },
      error: () => {},
    });
  }
}
