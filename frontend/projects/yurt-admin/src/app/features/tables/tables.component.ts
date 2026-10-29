import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { DiningTable, Location } from 'shared-models';
import { ToastService } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';
import { ReceiptService } from '../../core/receipt.service';

/** Most tables a single "1-N" entry may add at once (the server allows up to 200 per branch). */
const MAX_RANGE = 100;

/**
 * Tables and their QR codes (`table_ordering`). Staff add the tables of a branch, print the QR codes and stick them on
 * the tables. A guest who scans one orders to that table, and staff see the table on the order.
 */
@Component({
  selector: 'app-tables',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe],
  templateUrl: './tables.component.html',
})
export class TablesComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);
  private receipts = inject(ReceiptService);

  locations = signal<Location[]>([]);
  locationId = signal('');
  tables = signal<DiningTable[]>([]);
  loading = signal(false);
  busyId = signal<string | null>(null);

  /** "12" adds one table, "1-12" adds tables 1 to 12, "Terrace 1" adds one table with that name. */
  newTables = '';
  editingId = signal<string | null>(null);
  editLabel = '';

  readonly branchName = computed(() => this.locations().find((l) => l.id === this.locationId())?.name ?? '');
  /** No QR codes while the cafe has no customer app address to point them at. */
  readonly noLink = computed(() => this.tables().length > 0 && this.tables().every((t) => !t.link));

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe((locs) => {
      this.locations.set(locs);
      if (locs.length) this.pick(locs[0].id);
    });
  }

  pick(id: string): void {
    this.locationId.set(id);
    this.editingId.set(null);
    this.load();
  }

  load(): void {
    const id = this.locationId();
    if (!id) return;
    this.loading.set(true);
    this.api.getTables(id).subscribe({
      next: (t) => {
        this.tables.set(t);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  /** The table names an entry stands for, without the ones that already exist. */
  labelsFrom(entry: string): string[] {
    const text = entry.trim();
    const range = /^(\d+)\s*[-–]\s*(\d+)$/.exec(text);
    let labels: string[];
    if (range) {
      const [from, to] = [Number(range[1]), Number(range[2])].sort((a, b) => a - b);
      labels = Array.from({ length: Math.min(to - from + 1, MAX_RANGE) }, (_, i) => String(from + i));
    } else labels = text ? [text] : [];
    const taken = new Set(this.tables().map((t) => t.label.toLowerCase()));
    return labels.filter((l) => !taken.has(l.toLowerCase()));
  }

  add(): void {
    const labels = this.labelsFrom(this.newTables);
    if (!labels.length) {
      this.toast.warning(this.lang.t('tables.nothingToAdd'));
      return;
    }
    this.busyId.set('new');
    this.api.createTables(this.locationId(), labels).subscribe({
      next: (created) => {
        this.busyId.set(null);
        this.newTables = '';
        this.toast.success(this.lang.t('tables.added', { count: created.length }));
        this.load();
      },
      error: () => this.busyId.set(null),
    });
  }

  startRename(t: DiningTable): void {
    this.editingId.set(t.id);
    this.editLabel = t.label;
  }

  rename(t: DiningTable): void {
    const label = this.editLabel.trim();
    if (!label || label === t.label) {
      this.editingId.set(null);
      return;
    }
    this.busyId.set(t.id);
    this.api.renameTable(t.id, label).subscribe({
      next: (saved) => {
        this.busyId.set(null);
        this.editingId.set(null);
        this.tables.update((list) => list.map((x) => (x.id === saved.id ? saved : x)));
      },
      error: () => this.busyId.set(null),
    });
  }

  newCode(t: DiningTable): void {
    if (!confirm(this.lang.t('tables.confirmNewCode', { table: t.label }))) return;
    this.busyId.set(t.id);
    this.api.newTableCode(t.id).subscribe({
      next: (saved) => {
        this.busyId.set(null);
        this.tables.update((list) => list.map((x) => (x.id === saved.id ? saved : x)));
        this.toast.success(this.lang.t('tables.newCodeDone'));
      },
      error: () => this.busyId.set(null),
    });
  }

  remove(t: DiningTable): void {
    if (!confirm(this.lang.t('tables.confirmDelete', { table: t.label }))) return;
    this.busyId.set(t.id);
    this.api.deleteTable(t.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.tables.update((list) => list.filter((x) => x.id !== t.id));
      },
      error: () => this.busyId.set(null),
    });
  }

  print(tables: DiningTable[]): void {
    this.receipts.printTableQrCodes(tables, this.branchName());
  }
}
