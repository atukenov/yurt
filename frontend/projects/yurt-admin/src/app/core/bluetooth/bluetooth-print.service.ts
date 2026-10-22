import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { YurtApiService } from 'shared-api';
import {
  BluetoothConnector,
  BluetoothPrinterState,
  BluetoothPrintPump,
  WebBluetoothConnector,
} from './bluetooth-printing';

/** Which device each Bluetooth printer was paired with on this tablet, so it reconnects after a reload. */
const PAIRED_KEY = 'yurt_admin_bluetooth_printers';

/**
 * Bluetooth printers on this tablet (own_pos). The admin shell starts it after sign-in; it keeps printing while staff
 * move between pages, as long as the admin app stays open on the tablet.
 */
@Injectable({ providedIn: 'root' })
export class BluetoothPrintService {
  private api = inject(YurtApiService);
  private connector: BluetoothConnector = new WebBluetoothConnector();

  /** Connected printers by printer id: name, pages printed, last error. */
  readonly connected = signal<ReadonlyMap<string, BluetoothPrinterState>>(new Map());

  private pump = new BluetoothPrintPump(
    {
      claim: (printerIds) => firstValueFrom(this.api.claimBluetoothJobs(printerIds)),
      report: (jobId, ok, error) => firstValueFrom(this.api.reportBluetoothJob(jobId, ok, error)).then(() => undefined),
    },
    { onChange: () => this.connected.set(new Map(this.pump.connected)) },
  );

  supported(): boolean {
    return this.connector.supported();
  }

  /** Starts printing and reconnects the printers paired before. */
  start(): void {
    this.pump.start();
    if (!this.supported()) return;
    for (const [printerId, deviceId] of Object.entries(this.paired())) {
      if (this.pump.connected.has(printerId)) continue;
      this.connector
        .reconnect(deviceId)
        .then((link) => link && this.pump.attach(printerId, link))
        .catch(() => {
          // Printer off or out of range: staff tap Connect when it is back.
        });
    }
  }

  stop(): void {
    this.pump.stop();
    for (const id of [...this.pump.connected.keys()]) this.pump.detach(id);
  }

  /** Shows the browser's Bluetooth list and connects the chosen printer to this printer entry. Run on a tap. */
  async connect(printerId: string): Promise<string> {
    const link = await this.connector.pick();
    this.pump.attach(printerId, link);
    this.savePaired({ ...this.paired(), [printerId]: link.deviceId });
    this.pump.start();
    return link.name;
  }

  disconnect(printerId: string): void {
    this.pump.detach(printerId);
    const paired = this.paired();
    delete paired[printerId];
    this.savePaired(paired);
  }

  private paired(): Record<string, string> {
    try {
      const raw = localStorage.getItem(PAIRED_KEY);
      const value = raw ? JSON.parse(raw) : {};
      return value && typeof value === 'object' ? value : {};
    } catch {
      return {};
    }
  }

  private savePaired(value: Record<string, string>): void {
    try {
      localStorage.setItem(PAIRED_KEY, JSON.stringify(value));
    } catch {
      // No storage (private window): the tablet asks again after a reload.
    }
  }
}
