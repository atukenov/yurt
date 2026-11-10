import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { YurtApiService } from 'shared-api';
import { Location, StockChange, WRITE_OFF_REASONS, WriteOffReason } from 'shared-models';
import { LocalDatePipe, LocalNumberPipe, ToastService } from 'shared-ui';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';

/** Something the branch counts and can write off or send away. */
export interface MovableItem {
  kind: 'dish' | 'ingredient';
  id: string;
  name: string;
  unit: string | null;
  left: number;
}

/** True when taking `amount` would take the count below zero (the server refuses it too). */
export function goesBelowZero(item: MovableItem | null, amount: number | null): boolean {
  return !!item && amount !== null && amount > item.left;
}

/**
 * Write-offs and moves between branches (stock pack, `own_pos`). Open to staff, not only admins, so a barista can
 * record a spilled jug or send milk to the other branch without the owner. Every change is in the stock history.
 */
@Component({
  selector: 'app-stock-moves',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe, LocalDatePipe, LocalNumberPipe],
  templateUrl: './stock-moves.component.html',
})
export class StockMovesComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);

  private readonly LOC_KEY = 'yurt_stock_location';
  readonly reasons = WRITE_OFF_REASONS;
  private readonly RECENT_TYPES = 'writeoff,transfer_out,transfer_in';

  locations = signal<Location[]>([]);
  items = signal<MovableItem[]>([]);
  recent = signal<StockChange[]>([]);
  loading = signal(true);
  saving = signal(false);

  mode = signal<'writeoff' | 'transfer'>('writeoff');
  locationId = '';
  itemKey = '';
  amount: number | null = null;
  reason: WriteOffReason = 'spilled';
  toLocationId = '';
  note = '';

  otherLocations = computed(() => this.locations().filter((l) => l.id !== this.locationIdSig()));
  private locationIdSig = signal('');

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe((locs) => {
      this.locations.set(locs);
      const saved = localStorage.getItem(this.LOC_KEY) ?? '';
      this.locationId = locs.some((l) => l.id === saved) ? saved : (locs[0]?.id ?? '');
      this.load();
    });
  }

  load(): void {
    this.locationIdSig.set(this.locationId);
    if (!this.locationId) {
      this.loading.set(false);
      return;
    }
    localStorage.setItem(this.LOC_KEY, this.locationId);
    if (this.toLocationId === this.locationId) this.toLocationId = '';
    this.loading.set(true);
    forkJoin({
      ingredients: this.api.getIngredients(this.locationId),
      dishes: this.api.getAdminStock(this.locationId),
    }).subscribe({
      next: ({ ingredients, dishes }) => {
        const counted: MovableItem[] = [
          ...ingredients
            .filter((i) => i.quantity !== null)
            .map((i) => ({
              kind: 'ingredient' as const,
              id: i.id,
              name: i.name,
              unit: i.unit,
              left: i.quantity ?? 0,
            })),
          ...dishes
            .filter((d) => d.quantity !== null)
            .map((d) => ({
              kind: 'dish' as const,
              id: d.menuItemId,
              name: d.name,
              unit: null,
              left: d.quantity ?? 0,
            })),
        ].sort((a, b) => a.name.localeCompare(b.name));
        this.items.set(counted);
        if (!counted.some((i) => this.key(i) === this.itemKey)) this.itemKey = '';
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
    this.api
      .getStockHistory(this.locationId, 30, this.RECENT_TYPES)
      .subscribe((rows) => this.recent.set(rows));
  }

  key(item: MovableItem): string {
    return `${item.kind}:${item.id}`;
  }

  get selected(): MovableItem | null {
    return this.items().find((i) => this.key(i) === this.itemKey) ?? null;
  }

  get amountValue(): number | null {
    const n = Number(this.amount);
    return this.amount === null || `${this.amount}` === '' || Number.isNaN(n) || n <= 0 ? null : n;
  }

  get tooMuch(): boolean {
    return goesBelowZero(this.selected, this.amountValue);
  }

  get wholeOnly(): boolean {
    const n = this.amountValue;
    return this.selected?.kind === 'dish' && n !== null && !Number.isInteger(n);
  }

  get canSubmit(): boolean {
    return (
      !this.saving() &&
      !!this.selected &&
      this.amountValue !== null &&
      !this.tooMuch &&
      !this.wholeOnly &&
      (this.mode() === 'writeoff' || !!this.toLocationId)
    );
  }

  submit(): void {
    const item = this.selected;
    const quantity = this.amountValue;
    if (!item || quantity === null || !this.canSubmit) return;
    this.saving.set(true);
    const note = this.note.trim() || null;
    const request =
      this.mode() === 'writeoff'
        ? this.api.writeOffStock({
            locationId: this.locationId,
            kind: item.kind,
            itemId: item.id,
            quantity,
            reason: this.reason,
            note,
          })
        : this.api.transferStock({
            fromLocationId: this.locationId,
            toLocationId: this.toLocationId,
            kind: item.kind,
            itemId: item.id,
            quantity,
            note,
          });
    request.subscribe({
      next: (res) => {
        this.saving.set(false);
        this.toast.success(
          this.lang.t(this.mode() === 'writeoff' ? 'moves.writtenOff' : 'moves.moved', {
            name: res.name,
            left: `${res.left}${res.unit ? ' ' + res.unit : ''}`,
          }),
        );
        this.amount = null;
        this.note = '';
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.load(); // someone else may have used it meanwhile: show the real count
      },
    });
  }

  describe(row: StockChange): string {
    if (row.type === 'writeoff') return this.lang.t(`moves.reason.${row.reason ?? 'other'}`);
    if (row.type === 'transfer_out')
      return this.lang.t('moves.sentTo', { branch: row.otherLocationName ?? '' });
    if (row.type === 'transfer_in')
      return this.lang.t('moves.receivedFrom', { branch: row.otherLocationName ?? '' });
    return '';
  }
}
