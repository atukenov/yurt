import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { forkJoin } from 'rxjs';
import { YurtApiService } from 'shared-api';
import { Ingredient, SaveSupplier, Supplier, SUPPLIER_PAYMENT_TERMS, SupplierPaymentTerms } from 'shared-models';
import { Currency2Pipe, LocalNumberPipe, ToastService } from 'shared-ui';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';

interface ProductRow {
  ingredientId: string;
  packSize: number | null;
  packName: string;
  packPrice: number | null;
}

interface Form {
  id: string | null;
  name: string;
  contactName: string;
  phone: string;
  bin: string;
  paymentTerms: SupplierPaymentTerms;
  payInDays: number | null;
  note: string;
  isActive: boolean;
  products: ProductRow[];
}

/**
 * The cafe's suppliers (cafe admins): contact, BIN, how they are paid, what they sell in which pack at what usual
 * price, and how much the cafe owes them. What a supplier sells drives the "what to order" list.
 */
@Component({
  selector: 'app-suppliers-panel',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe, LocalNumberPipe, Currency2Pipe],
  templateUrl: './suppliers-panel.component.html',
})
export class SuppliersPanelComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);

  readonly terms = SUPPLIER_PAYMENT_TERMS;
  suppliers = signal<Supplier[]>([]);
  ingredients = signal<Ingredient[]>([]);
  loading = signal(true);
  busy = signal(false);
  form = signal<Form | null>(null);

  owed = computed(() => this.suppliers().reduce((s, x) => s + (x.owed ?? 0), 0));
  overdue = computed(() => this.suppliers().reduce((s, x) => s + (x.overdue ?? 0), 0));

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    // Ingredients are cafe-wide; any branch gives the full list with units.
    this.api.getAdminLocations().subscribe((locs) => {
      forkJoin({ suppliers: this.api.getSuppliers(), ingredients: this.api.getIngredients(locs[0]?.id ?? '') }).subscribe({
        next: (r) => {
          this.suppliers.set(r.suppliers);
          this.ingredients.set(r.ingredients);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
    });
  }

  add(): void {
    this.form.set({
      id: null, name: '', contactName: '', phone: '', bin: '', paymentTerms: 'cash', payInDays: null, note: '',
      isActive: true, products: [],
    });
  }

  edit(s: Supplier): void {
    this.form.set({
      id: s.id, name: s.name, contactName: s.contactName ?? '', phone: s.phone ?? '', bin: s.bin ?? '',
      paymentTerms: s.paymentTerms, payInDays: s.payInDays ?? null, note: s.note ?? '', isActive: s.isActive,
      products: s.products.map((p) => ({ ingredientId: p.ingredientId, packSize: p.packSize, packName: p.packName ?? '', packPrice: p.packPrice ?? null })),
    });
  }

  close(): void {
    this.form.set(null);
  }

  addProduct(f: Form): void {
    f.products.push({ ingredientId: '', packSize: 1, packName: '', packPrice: null });
  }

  removeProduct(f: Form, i: number): void {
    f.products.splice(i, 1);
  }

  unitOf(id: string): string {
    return this.ingredients().find((i) => i.id === id)?.unit ?? '';
  }

  taken(f: Form, row: ProductRow): Set<string> {
    return new Set(f.products.filter((p) => p !== row).map((p) => p.ingredientId));
  }

  /** Price of one unit (1 g, 1 ml…) from the pack, to compare suppliers. */
  perUnit(row: ProductRow): number | null {
    return row.packPrice !== null && row.packSize ? row.packPrice / row.packSize : null;
  }

  private body(f: Form): SaveSupplier {
    const clean = (v: string) => v.trim() || null;
    return {
      name: f.name.trim(), contactName: clean(f.contactName), phone: clean(f.phone), bin: clean(f.bin),
      paymentTerms: f.paymentTerms, payInDays: f.paymentTerms === 'later' ? (f.payInDays ?? 0) : null,
      note: clean(f.note), isActive: f.isActive,
      products: f.products
        .filter((p) => p.ingredientId && (Number(p.packSize) || 0) > 0)
        .map((p) => ({
          ingredientId: p.ingredientId, packSize: Number(p.packSize), packName: clean(p.packName),
          packPrice: p.packPrice === null || `${p.packPrice}` === '' ? null : Number(p.packPrice),
        })),
    };
  }

  save(f: Form): void {
    if (!f.name.trim()) return;
    this.busy.set(true);
    const call = f.id ? this.api.updateSupplier(f.id, this.body(f)) : this.api.createSupplier(this.body(f));
    call.subscribe({
      next: () => {
        this.busy.set(false);
        this.toast.success(this.lang.t('common.saved'));
        this.form.set(null);
        this.load();
      },
      error: (e) => this.failed(e),
    });
  }

  remove(f: Form): void {
    if (!f.id || !confirm(this.lang.t('buy.sup.confirmDelete', { name: f.name }))) return;
    this.api.deleteSupplier(f.id).subscribe({
      next: (deleted) => {
        this.toast.success(this.lang.t(deleted ? 'buy.sup.deleted' : 'buy.sup.switchedOff'));
        this.form.set(null);
        this.load();
      },
      error: (e) => this.failed(e),
    });
  }

  private failed(e: unknown): void {
    this.busy.set(false);
    if (!(e instanceof HttpErrorResponse)) this.toast.error(this.lang.t('common.saveFailed'));
  }
}
