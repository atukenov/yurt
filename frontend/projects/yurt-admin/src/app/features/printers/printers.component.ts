import { DatePipe } from '@angular/common';
import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { YurtApiService } from 'shared-api';
import { Location, Printer, PrintJob, SavePrinter, Station } from 'shared-models';
import { ToastService } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';

/**
 * Network printers (Phase 6, `own_pos`): the address of each branch's kitchen and receipt printers, a test print, and
 * the latest print jobs so staff can see what printed and what did not.
 */
@Component({
  selector: 'app-printers',
  standalone: true,
  imports: [FormsModule, DatePipe, AdminTranslatePipe],
  templateUrl: './printers.component.html',
})
export class PrintersComponent implements OnInit, OnDestroy {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);

  printers = signal<Printer[]>([]);
  jobs = signal<PrintJob[]>([]);
  locations = signal<Location[]>([]);
  stations = signal<Station[]>([]);
  loading = signal(true);
  busyId = signal<string | null>(null);

  /** The printer being edited ('' = a new one), or null when the form is closed. */
  editingId = signal<string | null>(null);
  form: SavePrinter = this.blank();

  private refresh: ReturnType<typeof setInterval> | null = null;

  byLocation = computed(() =>
    this.locations()
      .map((l) => ({ location: l, printers: this.printers().filter((p) => p.locationId === l.id) }))
      .filter((g) => g.printers.length > 0),
  );

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe((locs) => this.locations.set(locs));
    this.api.getStationRouting().subscribe((r) => this.stations.set(r.stations));
    this.load();
    // Keep the job list fresh while the page is open.
    this.refresh = setInterval(() => this.loadJobs(), 10_000);
  }

  ngOnDestroy(): void {
    if (this.refresh) clearInterval(this.refresh);
  }

  load(): void {
    this.api.getPrinters().subscribe({
      next: (p) => {
        this.printers.set(p);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
    this.loadJobs();
  }

  loadJobs(): void {
    this.api.getPrintJobs(30).subscribe((j) => this.jobs.set(j));
  }

  stationName(id?: string | null): string {
    return this.stations().find((s) => s.id === id)?.name ?? this.lang.t('printers.allItems');
  }

  add(): void {
    this.form = this.blank();
    this.editingId.set('');
  }

  edit(p: Printer): void {
    const { id: _, ...rest } = p;
    this.form = { ...rest, stationId: rest.stationId ?? null };
    this.editingId.set(p.id);
  }

  save(): void {
    const id = this.editingId();
    if (id === null) return;
    const body: SavePrinter = {
      ...this.form,
      port: Number(this.form.port),
      lineWidth: Number(this.form.lineWidth),
      codeTable: Number(this.form.codeTable),
      stationId: this.form.kind === 'Kitchen' ? this.form.stationId || null : null,
    };
    this.busyId.set('form');
    (id ? this.api.updatePrinter(id, body) : this.api.createPrinter(body)).subscribe({
      next: () => {
        this.busyId.set(null);
        this.editingId.set(null);
        this.load();
      },
      error: (e: HttpErrorResponse) => {
        this.busyId.set(null);
        this.toast.error(this.message(e, 'Could not save'));
      },
    });
  }

  remove(p: Printer): void {
    if (!confirm(this.lang.t('printers.confirmDelete'))) return;
    this.api.deletePrinter(p.id).subscribe({ next: () => this.load() });
  }

  test(p: Printer): void {
    this.busyId.set(p.id);
    this.api.testPrinter(p.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.toast.success(this.lang.t('printers.testSent'));
      },
      error: (e: HttpErrorResponse) => {
        this.busyId.set(null);
        this.toast.error(this.message(e, 'The printer did not answer'));
      },
    });
  }

  jobOrder(j: PrintJob): string {
    return j.orderId ? '#' + j.orderId.replace(/-/g, '').slice(0, 8).toUpperCase() : '';
  }

  private message(e: HttpErrorResponse, fallback: string): string {
    // The API answers with a problem whose title is the reason, such as "Could not reach the printer at …".
    const body = e.error as { title?: string } | null;
    return (body && typeof body === 'object' && body.title) || fallback;
  }

  private blank(): SavePrinter {
    return {
      locationId: this.locations()[0]?.id ?? '',
      name: '',
      kind: 'Kitchen',
      stationId: null,
      host: '',
      port: 9100,
      lineWidth: 48,
      codeTable: 17,
      language: (this.lang.lang() === 'en' ? 'en' : this.lang.lang() === 'kk' ? 'kk' : 'ru'),
      autoPrint: true,
    };
  }
}
