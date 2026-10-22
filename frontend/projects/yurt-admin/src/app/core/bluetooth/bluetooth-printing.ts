/**
 * Bluetooth printers (Phase 6, `own_pos`): the admin app on a tablet in the cafe is paired with a thermal printer over
 * Bluetooth Low Energy. Every few seconds it asks the server for the jobs of the printers it is connected to, sends the
 * ESC/POS bytes over Bluetooth and says how each one went, like the print agent does for network printers.
 *
 * This file has no Angular in it, so the sending logic can be tested with a pretend printer.
 */

/** A connected printer: the tablet writes bytes to it. */
export interface BluetoothLink {
  /** The name the printer shows over Bluetooth, such as "MPT-II". */
  readonly name: string;
  /** The device id the browser gives the printer; kept so the tablet can reconnect after a reload. */
  readonly deviceId: string;
  write(data: Uint8Array): Promise<void>;
  disconnect(): void;
  /** Called once when the printer goes away (switched off, out of range). */
  onDisconnect(handler: () => void): void;
}

/** Finds printers: the browser's own Web Bluetooth, or a pretend one in tests. */
export interface BluetoothConnector {
  supported(): boolean;
  /** Shows the browser's list of nearby devices; must run right after a tap. */
  pick(): Promise<BluetoothLink>;
  /** Connects again to a printer picked before, without a tap. Null when the browser no longer knows it. */
  reconnect(deviceId: string): Promise<BluetoothLink | null>;
}

/** A job from the server: which printer, and the bytes as base64. */
export interface BluetoothJob {
  id: string;
  printerId: string;
  kind: string;
  data: string;
}

export interface BluetoothPrintApi {
  claim(printerIds: string[]): Promise<BluetoothJob[]>;
  report(jobId: string, ok: boolean, error?: string): Promise<void>;
}

/** What one connected printer is doing, for the Printers page. */
export interface BluetoothPrinterState {
  name: string;
  printed: number;
  lastError?: string;
}

/**
 * Services that cheap Bluetooth receipt printers use for "send bytes to print". A browser only lets a page use
 * services it names up front, so every one we know is listed. Untested on real printers so far.
 */
export const PRINTER_SERVICES = [
  '000018f0-0000-1000-8000-00805f9b34fb', // most 58 mm and 80 mm Chinese printers (Xprinter, MPT, Goojprt)
  'e7810a71-73ae-499d-8c15-faa9aef0c3f2', // many POS printers ("Printer001")
  '49535343-fe7d-4ae8-8fa9-9fafd205e455', // printers built on the Microchip ISSC module
  '6e400001-b5a3-f393-e0a9-e50e24dcca9e', // Nordic UART
];

/** Bytes per Bluetooth write. Small enough for most printers; long jobs go out in many writes. */
export const CHUNK_SIZE = 100;

/** Pause between writes when the printer does not confirm each one, so its buffer does not overflow. */
export const UNCONFIRMED_PAUSE_MS = 20;

/** The part of a Web Bluetooth characteristic that sending needs (a pretend one in tests). */
export interface WritableCharacteristic {
  properties: { write: boolean; writeWithoutResponse: boolean };
  writeValueWithResponse(value: BufferSource): Promise<void>;
  writeValueWithoutResponse(value: BufferSource): Promise<void>;
}

/** The first characteristic that accepts writes, in the order of the services given. */
export function pickWritable<T extends WritableCharacteristic>(characteristics: T[]): T | null {
  return (
    characteristics.find((c) => c.properties.write) ??
    characteristics.find((c) => c.properties.writeWithoutResponse) ??
    null
  );
}

/**
 * Sends the bytes in small parts. A printer that confirms each write sets the pace itself; one that does not gets a
 * short pause between parts.
 */
export async function writeInChunks(
  c: WritableCharacteristic,
  data: Uint8Array,
  chunkSize = CHUNK_SIZE,
  pause: (ms: number) => Promise<void> = sleep,
): Promise<void> {
  const confirmed = c.properties.write;
  for (let at = 0; at < data.length; at += chunkSize) {
    const part = data.slice(at, at + chunkSize);
    if (confirmed) await c.writeValueWithResponse(part);
    else {
      await c.writeValueWithoutResponse(part);
      await pause(UNCONFIRMED_PAUSE_MS);
    }
  }
}

export function fromBase64(data: string): Uint8Array {
  const raw = atob(data);
  const bytes = new Uint8Array(raw.length);
  for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
  return bytes;
}

function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function withTimeout<T>(work: Promise<T>, ms: number, message: string): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(message)), ms);
    work.then(
      (v) => {
        clearTimeout(timer);
        resolve(v);
      },
      (e) => {
        clearTimeout(timer);
        reject(e);
      },
    );
  });
}

/**
 * Takes the jobs of the connected printers from the server and prints them. Only printers that are connected right now
 * are named to the server, so a tablet never takes a job it cannot print, and two tablets with different printers do
 * not get in each other's way.
 */
export class BluetoothPrintPump {
  private links = new Map<string, BluetoothLink>();
  private states = new Map<string, BluetoothPrinterState>();
  private timer: ReturnType<typeof setTimeout> | null = null;
  private running = false;
  private busy = false;

  constructor(
    private api: BluetoothPrintApi,
    private options: { intervalMs?: number; sendTimeoutMs?: number; onChange?: () => void } = {},
  ) {}

  /** Printers connected on this tablet, by printer id. */
  get connected(): ReadonlyMap<string, BluetoothPrinterState> {
    return this.states;
  }

  attach(printerId: string, link: BluetoothLink): void {
    this.links.get(printerId)?.disconnect();
    this.links.set(printerId, link);
    this.states.set(printerId, { name: link.name, printed: 0 });
    link.onDisconnect(() => {
      if (this.links.get(printerId) !== link) return;
      this.links.delete(printerId);
      this.states.delete(printerId);
      this.changed();
    });
    this.changed();
    if (this.running) this.schedule(0);
  }

  detach(printerId: string): void {
    const link = this.links.get(printerId);
    this.links.delete(printerId);
    this.states.delete(printerId);
    link?.disconnect();
    this.changed();
  }

  start(): void {
    if (this.running) return;
    this.running = true;
    this.schedule(0);
  }

  stop(): void {
    this.running = false;
    if (this.timer) clearTimeout(this.timer);
    this.timer = null;
  }

  /** One round: take the waiting jobs and print them. Returns how many printed. */
  async tick(): Promise<number> {
    if (this.busy || this.links.size === 0) return 0;
    this.busy = true;
    let printed = 0;
    try {
      const jobs = await this.api.claim([...this.links.keys()]);
      for (const job of jobs) if (await this.send(job)) printed++;
    } catch {
      // The server or the network is down: try again next round. Jobs taken but not reported are marked failed by
      // the server after a while.
    } finally {
      this.busy = false;
    }
    return printed;
  }

  private async send(job: BluetoothJob): Promise<boolean> {
    const link = this.links.get(job.printerId);
    let error: string | undefined;
    if (!link) error = 'The tablet lost the Bluetooth printer.';
    else {
      try {
        await withTimeout(link.write(fromBase64(job.data)), this.options.sendTimeoutMs ?? 30_000, 'The Bluetooth printer did not answer.');
      } catch (e) {
        error = (e instanceof Error && e.message) || 'Could not print over Bluetooth.';
      }
    }
    const state = this.states.get(job.printerId);
    if (state) {
      if (error) state.lastError = error;
      else {
        state.printed++;
        state.lastError = undefined;
      }
      this.changed();
    }
    try {
      await this.api.report(job.id, !error, error);
    } catch {
      // The server marks a job it never heard back about as failed.
    }
    return !error;
  }

  private schedule(ms: number): void {
    if (this.timer) clearTimeout(this.timer);
    this.timer = setTimeout(async () => {
      this.timer = null;
      await this.tick();
      if (this.running) this.schedule(this.options.intervalMs ?? 3000);
    }, ms);
  }

  private changed(): void {
    this.options.onChange?.();
  }
}

/** Minimal Web Bluetooth types, so the app does not need the full type package. */
interface WebBluetoothDevice {
  id: string;
  name?: string;
  gatt?: {
    connected: boolean;
    connect(): Promise<{ getPrimaryServices(): Promise<{ uuid: string; getCharacteristics(): Promise<WritableCharacteristic[]> }[]> }>;
    disconnect(): void;
  };
  addEventListener(type: 'gattserverdisconnected', handler: () => void): void;
}

interface WebBluetooth {
  requestDevice(options: { acceptAllDevices: boolean; optionalServices: string[] }): Promise<WebBluetoothDevice>;
  getDevices?(): Promise<WebBluetoothDevice[]>;
}

/**
 * The browser's own Bluetooth (Chrome on Android, Windows, macOS and ChromeOS). Safari on iPad and iPhone has none.
 * Only Bluetooth Low Energy printers work this way; printers that only speak "classic" Bluetooth do not show up.
 */
export class WebBluetoothConnector implements BluetoothConnector {
  constructor(private nav: { bluetooth?: WebBluetooth } = navigator as unknown as { bluetooth?: WebBluetooth }) {}

  supported(): boolean {
    return !!this.nav.bluetooth;
  }

  async pick(): Promise<BluetoothLink> {
    if (!this.nav.bluetooth) throw new Error('This browser cannot use Bluetooth.');
    const device = await this.nav.bluetooth.requestDevice({ acceptAllDevices: true, optionalServices: PRINTER_SERVICES });
    return this.open(device);
  }

  async reconnect(deviceId: string): Promise<BluetoothLink | null> {
    // getDevices is missing in some Chrome versions; then staff tap Connect again after a reload.
    const devices = (await this.nav.bluetooth?.getDevices?.()) ?? [];
    const device = devices.find((d) => d.id === deviceId);
    return device ? this.open(device) : null;
  }

  private async open(device: WebBluetoothDevice): Promise<BluetoothLink> {
    if (!device.gatt) throw new Error('This device cannot be used as a printer.');
    const server = await device.gatt.connect();
    const services = await server.getPrimaryServices();
    const known = services
      .filter((s) => PRINTER_SERVICES.includes(s.uuid.toLowerCase()))
      .sort((a, b) => PRINTER_SERVICES.indexOf(a.uuid.toLowerCase()) - PRINTER_SERVICES.indexOf(b.uuid.toLowerCase()));
    let characteristic: WritableCharacteristic | null = null;
    for (const service of known) {
      characteristic = pickWritable(await service.getCharacteristics());
      if (characteristic) break;
    }
    if (!characteristic) {
      device.gatt.disconnect();
      throw new Error('This device does not look like a Bluetooth printer.');
    }
    const c = characteristic;
    const gatt = device.gatt;
    return {
      name: device.name || 'Bluetooth printer',
      deviceId: device.id,
      write: (data) => writeInChunks(c, data),
      disconnect: () => gatt.disconnect(),
      onDisconnect: (handler) => device.addEventListener('gattserverdisconnected', handler),
    };
  }
}
