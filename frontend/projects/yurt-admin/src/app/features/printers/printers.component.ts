import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { YurtApiService } from 'shared-api';
import { Location, Printer, PrintAgent, PrintJob, SavePrinter, Station } from 'shared-models';
import { ToastService, LocalDatePipe } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';
import { environment } from '../../../environments/environment';
import { BluetoothPrintService } from '../../core/bluetooth/bluetooth-print.service';

/**
 * Printers (Phase 6, `own_pos`): the address of each branch's kitchen and receipt printers (or Bluetooth through a
 * tablet), a test print, and the latest print jobs so staff can see what printed and what did not.
 */
@Component({
  selector: 'app-printers',
  standalone: true,
  imports: [LocalDatePipe, FormsModule, AdminTranslatePipe],
  templateUrl: './printers.component.html',
})
export class PrintersComponent implements OnInit, OnDestroy {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);
  readonly bluetooth = inject(BluetoothPrintService);

  printers = signal<Printer[]>([]);
  jobs = signal<PrintJob[]>([]);
  locations = signal<Location[]>([]);
  stations = signal<Station[]>([]);
  agents = signal<PrintAgent[]>([]);
  /** A key just made, shown once so staff can copy it to the agent. */
  newKey = signal<{ locationId: string; key: string } | null>(null);
  readonly apiUrl = environment.apiUrl;
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
    this.api.getPrintAgents().subscribe((a) => this.agents.set(a));
  }

  loadJobs(): void {
    this.api.getPrintJobs(30).subscribe((j) => this.jobs.set(j));
  }

  agentOf(locationId: string): PrintAgent | undefined {
    return this.agents().find((a) => a.locationId === locationId);
  }

  /** The agent asks for jobs every few seconds, so one seen in the last minute is running. */
  agentOnline(a?: PrintAgent): boolean {
    return !!a?.lastSeenAt && Date.now() - new Date(a.lastSeenAt).getTime() < 60_000;
  }

  createKey(locationId: string): void {
    if (this.agentOf(locationId) && !confirm(this.lang.t('printers.agent.confirmNewKey'))) return;
    this.api.createPrintAgentKey(locationId).subscribe({
      next: (k) => {
        this.newKey.set(k);
        this.load();
      },
      error: (e: HttpErrorResponse) => this.toast.error(this.message(e, 'Could not make a key')),
    });
  }

  removeKey(locationId: string): void {
    if (!confirm(this.lang.t('printers.agent.confirmRemove'))) return;
    this.api.deletePrintAgentKey(locationId).subscribe({ next: () => this.load() });
  }

  copyKey(): void {
    const key = this.newKey()?.key;
    if (key) void navigator.clipboard?.writeText(key).then(() => this.toast.success(this.lang.t('printers.agent.copied')));
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

  /** The form can be saved: a network printer needs an address, a Bluetooth one does not. */
  canSave(): boolean {
    return !!this.form.name.trim() && !!this.form.locationId && (this.form.viaBluetooth || !!this.form.host.trim());
  }

  /** Pairs this tablet with a Bluetooth printer. The browser shows its list of nearby devices. */
  async connectBluetooth(p: Printer): Promise<void> {
    this.busyId.set(p.id);
    try {
      await this.bluetooth.connect(p.id);
    } catch (e) {
      // Closing the browser's list without picking is not an error worth showing.
      if (!(e instanceof Error && e.name === 'NotFoundError')) {
        this.toast.error(`${this.lang.t('printers.bt.connectFailed')}${e instanceof Error && e.message ? ': ' + e.message : ''}`);
      }
    } finally {
      this.busyId.set(null);
    }
  }

  save(): void {
    const id = this.editingId();
    if (id === null) return;
    const body: SavePrinter = {
      ...this.form,
      host: this.form.viaBluetooth ? '' : this.form.host,
      viaAgent: this.form.viaBluetooth ? false : this.form.viaAgent,
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
      next: (r) => {
        this.busyId.set(null);
        this.toast.success(this.lang.t(r.printed ? 'printers.testSent' : p.viaBluetooth ? 'printers.testQueuedBt' : 'printers.testQueued'));
        if (!r.printed) this.loadJobs();
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
      viaAgent: false,
      cashDrawer: false,
      reprintWithTaxQr: false,
      viaBluetooth: false,
    };
  }
}
