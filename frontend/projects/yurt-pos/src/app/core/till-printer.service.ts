import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { BluetoothPrinterState, BluetoothPrintPump, WebBluetoothConnector, YurtApiService } from 'shared-api';
import { Printer } from 'shared-models';
import { receiptBytes, TillReceipt } from './escpos';

const PAIRED_KEY = 'yurt_pos_printer';
const printersKey = (branch: string) => `yurt_pos_printers:${branch}`;

function read<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : fallback;
  } catch {
    return fallback;
  }
}

function write(key: string, value: unknown): void {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch {
    // No storage: the till asks again after a reload.
  }
}

/**
 * The receipt printer next to the till, over Bluetooth (Chrome's Web Bluetooth, Bluetooth Low Energy printers). It is one
 * of the branch's printers from the admin app, so the server's own receipts (for sales made online) come to it as
 * jobs, exactly as on an admin tablet; a sale made while the internet was down is printed by the till itself, because
 * the server never sees it until later.
 */
@Injectable({ providedIn: 'root' })
export class TillPrinterService {
  private api = inject(YurtApiService);
  private connector = new WebBluetoothConnector();
  private pump = new BluetoothPrintPump(
    {
      claim: (ids) => firstValueFrom(this.api.claimBluetoothJobs(ids)),
      report: (id, ok, error) => firstValueFrom(this.api.reportBluetoothJob(id, ok, error)).then(() => undefined),
    },
    { onChange: () => this.connected.set(new Map(this.pump.connected)) },
  );

  /** The branch's Bluetooth printers (from the admin app), kept on the tablet for offline starts. */
  readonly printers = signal<Printer[]>([]);
  /** Printers connected right now, by printer id. */
  readonly connected = signal<ReadonlyMap<string, BluetoothPrinterState>>(new Map());
  readonly error = signal<string | null>(null);

  private branch: string | null = null;

  supported(): boolean {
    return this.connector.supported();
  }

  /** Loads the branch's printers and reconnects the one this tablet used before. Called when the till knows its branch. */
  async start(branch: string): Promise<void> {
    if (this.branch !== branch) {
      this.branch = branch;
      this.printers.set(read<Printer[]>(printersKey(branch), []));
    }
    this.pump.start();
    try {
      const all = await firstValueFrom(this.api.getPrinters());
      const mine = all.filter((p) => p.locationId === branch && p.viaBluetooth);
      this.printers.set(mine);
      write(printersKey(branch), mine);
    } catch {
      // Offline: the saved list stays.
    }
    const paired = read<{ printerId: string; deviceId: string } | null>(this.pairedKey(branch), null);
    if (!paired || !this.supported() || this.pump.connected.has(paired.printerId)) return;
    try {
      const link = await this.connector.reconnect(paired.deviceId);
      if (link) this.pump.attach(paired.printerId, link);
    } catch {
      // Printer off or out of range: staff press Connect when it is back.
    }
  }

  /** Shows Chrome's Bluetooth list and connects the chosen printer to this branch printer. Must run on a tap. */
  async connect(printerId: string): Promise<void> {
    this.error.set(null);
    try {
      const link = await this.connector.pick();
      for (const id of [...this.pump.connected.keys()]) this.pump.detach(id);
      this.pump.attach(printerId, link);
      if (this.branch) write(this.pairedKey(this.branch), { printerId, deviceId: link.deviceId });
      this.pump.start();
    } catch (e) {
      // Closing Chrome's list without choosing is not an error worth showing.
      const message = e instanceof Error ? e.message : '';
      if (!/cancel|chosen|dismiss/i.test(message)) this.error.set(message || 'Could not connect the printer.');
    }
  }

  disconnect(printerId: string): void {
    this.pump.detach(printerId);
    if (this.branch) write(this.pairedKey(this.branch), null);
  }

  /** The connected printer that prints receipts (a receipt printer first), with its settings from the admin app. */
  private receiptPrinter(): Printer | null {
    const list = this.printers().filter((p) => this.pump.connected.has(p.id));
    return list.find((p) => p.kind === 'Receipt') ?? list[0] ?? null;
  }

  hasPrinter(): boolean {
    return this.receiptPrinter() !== null;
  }

  /**
   * Prints the receipt of a sale made without internet, and opens the drawer for cash. Returns null when it printed (or
   * there is no printer connected, which is not an error), else the reason it did not.
   */
  async printOffline(receipt: TillReceipt, cash: boolean): Promise<string | null> {
    const printer = this.receiptPrinter();
    if (!printer) return null;
    const bytes = receiptBytes(
      { lineWidth: printer.lineWidth, codeTable: printer.codeTable, language: printer.language, cashDrawer: printer.cashDrawer },
      receipt, cash,
    );
    try {
      await this.pump.printNow(printer.id, bytes);
      return null;
    } catch (e) {
      return e instanceof Error && e.message ? e.message : 'Could not print.';
    }
  }

  private pairedKey(branch: string): string {
    return `${PAIRED_KEY}:${branch}`;
  }
}
