import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { forkJoin, Observable, switchMap } from 'rxjs';
import { BrandService, YurtApiService } from 'shared-api';
import { Ingredient, Purchase, PurchaseStatus, SavePurchase, Supplier } from 'shared-models';
import { Currency2Pipe, LocalDatePipe, LocalNumberPipe, ToastService } from 'shared-ui';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { PictureUploadComponent } from '../../shared/picture-upload/picture-upload.component';
import { invoiceTotal, isoDay, orderText, whatsappLink } from './purchasing-helpers';

interface EditLine {
  ingredientId: string;
  quantity: number | null;
  total: number | null;
}

interface Form {
  id: string | null;
  status: 'ordered' | 'draft';
  supplierId: string;
  number: string;
  date: string;
  note: string;
  photoUrl: string;
  lines: EditLine[];
}

/**
 * Orders to suppliers and delivery invoices of a branch. A delivery is typed in as one document (supplier, number,
 * date, lines with amount and price) and posted: everything goes on the shelf at once and costs are updated. An order
 * made on the "what to order" list is opened here and posted the same way when the goods come.
 */
@Component({
  selector: 'app-deliveries-panel',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe, LocalDatePipe, LocalNumberPipe, Currency2Pipe, PictureUploadComponent],
  templateUrl: './deliveries-panel.component.html',
})
export class DeliveriesPanelComponent {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);
  private brand = inject(BrandService);

  locationId = input.required<string>();
  isAdmin = input(false);
  /** Open this order or invoice right away (just made on the "what to order" list). */
  openId = input<string | null>(null);

  readonly statuses: PurchaseStatus[] = ['ordered', 'draft', 'posted', 'cancelled'];
  list = signal<Purchase[]>([]);
  suppliers = signal<Supplier[]>([]);
  ingredients = signal<Ingredient[]>([]);
  loading = signal(true);
  busy = signal(false);
  statusFilter = '';
  unpaidOnly = false;

  /** The order or invoice being typed in (not posted yet). */
  form = signal<Form | null>(null);
  /** A posted or cancelled invoice, shown read-only. */
  viewing = signal<Purchase | null>(null);

  activeSuppliers = computed(() => this.suppliers().filter((s) => s.isActive || s.id === this.form()?.supplierId));
  owed = computed(() => this.list().filter((p) => p.status === 'posted' && !p.isPaid).reduce((s, p) => s + p.total, 0));

  constructor() {
    effect(() => {
      const loc = this.locationId();
      untracked(() => this.loadAll(loc));
    });
    effect(() => {
      const id = this.openId();
      if (id) untracked(() => this.open(id));
    });
  }

  private loadAll(locationId: string): void {
    this.form.set(null);
    this.viewing.set(null);
    forkJoin({ suppliers: this.api.getSuppliers(), ingredients: this.api.getIngredients(locationId) }).subscribe((r) => {
      this.suppliers.set(r.suppliers);
      this.ingredients.set(r.ingredients);
    });
    this.loadList();
  }

  loadList(): void {
    this.loading.set(true);
    this.api
      .getPurchases({ locationId: this.locationId(), status: this.statusFilter || undefined, unpaid: this.unpaidOnly || undefined })
      .subscribe({
        next: (list) => {
          this.list.set(list);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  // ── open / new ──

  newDelivery(): void {
    this.viewing.set(null);
    const first = this.suppliers().find((s) => s.isActive);
    this.form.set({
      id: null, status: 'draft', supplierId: first?.id ?? '', number: '', date: isoDay(), note: '', photoUrl: '',
      lines: [{ ingredientId: '', quantity: null, total: null }],
    });
  }

  open(id: string): void {
    this.api.getPurchase(id).subscribe((p) => this.show(p));
  }

  private show(p: Purchase): void {
    if (p.status === 'ordered' || p.status === 'draft') {
      this.viewing.set(null);
      this.form.set({
        id: p.id, status: p.status, supplierId: p.supplierId, number: p.number ?? '', date: p.date.slice(0, 10),
        note: p.note ?? '', photoUrl: p.photoUrl ?? '',
        lines: p.lines.map((l) => ({ ingredientId: l.ingredientId, quantity: l.quantity, total: l.total ?? null })),
      });
    } else {
      this.form.set(null);
      this.viewing.set(p);
    }
  }

  close(): void {
    this.form.set(null);
    this.viewing.set(null);
  }

  // ── lines ──

  addLine(f: Form): void {
    f.lines.push({ ingredientId: '', quantity: null, total: null });
  }

  removeLine(f: Form, i: number): void {
    f.lines.splice(i, 1);
  }

  unitOf(id: string): string {
    return this.ingredients().find((i) => i.id === id)?.unit ?? '';
  }

  /** Picking an ingredient the supplier sells fills in one pack and its usual price. */
  picked(f: Form, line: EditLine): void {
    const product = this.suppliers().find((s) => s.id === f.supplierId)?.products.find((p) => p.ingredientId === line.ingredientId);
    if (product && line.quantity === null) {
      line.quantity = product.packSize;
      line.total = product.packPrice ?? null;
    }
  }

  unitPrice(line: EditLine): number | null {
    return line.total !== null && line.quantity ? line.total / line.quantity : null;
  }

  total(f: Form): number {
    return invoiceTotal(f.lines);
  }

  /** Ingredients already on another line, so each is listed once. */
  taken(f: Form, line: EditLine): Set<string> {
    return new Set(f.lines.filter((l) => l !== line).map((l) => l.ingredientId));
  }

  // ── save / post ──

  private body(f: Form, status: 'ordered' | 'draft'): SavePurchase {
    return {
      supplierId: f.supplierId, locationId: this.locationId(), status, number: f.number.trim() || null,
      date: f.date || null, note: f.note.trim() || null, photoUrl: f.photoUrl.trim() || null,
      lines: f.lines
        .filter((l) => l.ingredientId && (Number(l.quantity) || 0) > 0)
        .map((l) => ({
          ingredientId: l.ingredientId, quantity: Number(l.quantity),
          total: l.total === null || `${l.total}` === '' ? null : Number(l.total),
        })),
    };
  }

  private saveCall(f: Form, status: 'ordered' | 'draft'): Observable<Purchase> {
    const body = this.body(f, status);
    return f.id ? this.api.updatePurchase(f.id, body) : this.api.createPurchase(body);
  }

  canSave(f: Form): boolean {
    return !!f.supplierId && this.body(f, f.status).lines.length > 0 && !this.busy();
  }

  save(f: Form, status: 'ordered' | 'draft'): void {
    this.busy.set(true);
    this.saveCall(f, status).subscribe({
      next: (p) => {
        this.busy.set(false);
        this.toast.success(this.lang.t('common.saved'));
        this.show(p);
        this.loadList();
      },
      error: (e) => this.failed(e),
    });
  }

  /** Saves what is typed and puts it on the shelf. */
  post(f: Form): void {
    if (!confirm(this.lang.t('buy.inv.confirmPost', { count: this.body(f, 'draft').lines.length }))) return;
    this.busy.set(true);
    this.saveCall(f, 'draft')
      .pipe(switchMap((p) => this.api.postPurchase(p.id)))
      .subscribe({
        next: (r) => {
          this.busy.set(false);
          this.toast.success(this.lang.t('buy.inv.posted'));
          this.show(r.purchase);
          this.loadList();
        },
        error: (e) => this.failed(e),
      });
  }

  remove(f: Form): void {
    if (!f.id) return this.close();
    if (!confirm(this.lang.t('buy.inv.confirmDelete'))) return;
    this.api.deletePurchase(f.id).subscribe({
      next: () => {
        this.close();
        this.loadList();
      },
      error: (e) => this.failed(e),
    });
  }

  sendOrder(f: Form): void {
    const supplier = this.suppliers().find((s) => s.id === f.supplierId);
    const lines = this.body(f, 'ordered').lines.map((l) => {
      const product = supplier?.products.find((p) => p.ingredientId === l.ingredientId);
      const name = this.ingredients().find((i) => i.id === l.ingredientId)?.name ?? '';
      const packs = product && product.packSize > 0 && product.packName ? Math.ceil(l.quantity / product.packSize - 1e-9) : null;
      return { name, unit: this.unitOf(l.ingredientId), quantity: l.quantity, packs, packName: product?.packName };
    });
    window.open(whatsappLink(supplier?.phone, orderText(this.lang.t('buy.msg.hello'), lines, this.brand.name())), '_blank', 'noopener');
  }

  // ── posted invoices (admins) ──

  setPaid(p: Purchase, paid: boolean): void {
    this.api.setPurchasePaid(p.id, paid).subscribe({
      next: (u) => {
        this.viewing.set(u);
        this.loadList();
      },
      error: (e) => this.failed(e),
    });
  }

  cancelInvoice(p: Purchase): void {
    if (!confirm(this.lang.t('buy.inv.confirmCancel'))) return;
    this.busy.set(true);
    this.api.cancelPurchase(p.id).subscribe({
      next: (u) => {
        this.busy.set(false);
        this.toast.success(this.lang.t('buy.inv.cancelled'));
        this.viewing.set(u);
        this.loadList();
      },
      error: (e) => this.failed(e),
    });
  }

  private failed(e: unknown): void {
    this.busy.set(false);
    // The shared error toast already shows the server's reason (for example "only 300 ml left").
    if (!(e instanceof HttpErrorResponse)) this.toast.error(this.lang.t('common.saveFailed'));
  }

  statusClass(s: PurchaseStatus): string {
    return {
      ordered: 'bg-sky-100 text-sky-800',
      draft: 'bg-slate-100 text-slate-700',
      posted: 'bg-emerald-100 text-emerald-800',
      cancelled: 'bg-red-100 text-red-700',
    }[s];
  }
}
