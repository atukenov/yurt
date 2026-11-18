import { Injectable, signal } from '@angular/core';
import { parseScaleReading, ScaleReading, splitScaleLines } from 'shared-api';

const BAUD_KEY = 'yurt_pos_scale_baud';
/** A reading older than this is not used: the scale stopped talking or was unplugged. */
const FRESH_MS = 3000;
export const BAUD_RATES = [2400, 4800, 9600, 19200, 38400, 115200];

/** A serial port as the browser gives it (Web Serial is not in TypeScript's own typings yet). */
interface SerialPortLike {
  open(options: { baudRate: number }): Promise<void>;
  close(): Promise<void>;
  readable: ReadableStream<Uint8Array> | null;
}

/**
 * A scale plugged into the till by USB or a serial cable, read with Web Serial (Chrome and Edge, https or localhost).
 * Most shop scales send their weight as a line of text about every half second. The till shows the latest reading and
 * takes it for the item being weighed. Nothing here is needed to sell: without a scale the cashier types the weight.
 */
@Injectable({ providedIn: 'root' })
export class ScaleService {
  readonly supported = typeof navigator !== 'undefined' && 'serial' in navigator;
  readonly connected = signal(false);
  readonly baud = signal(this.savedBaud());
  /** The scale could not be opened or stopped talking; the till shows the plain "Scale" button again. */
  readonly failed = signal(false);
  /** The latest reading and when it came. */
  readonly last = signal<{ reading: ScaleReading; at: number } | null>(null);

  private port: SerialPortLike | null = null;
  private reader: ReadableStreamDefaultReader<Uint8Array> | null = null;

  private savedBaud(): number {
    try {
      const n = Number(localStorage.getItem(BAUD_KEY));
      return BAUD_RATES.includes(n) ? n : 9600;
    } catch {
      return 9600;
    }
  }

  setBaud(rate: number): void {
    this.baud.set(rate);
    try { localStorage.setItem(BAUD_KEY, String(rate)); } catch { /* not saved: fine */ }
  }

  /** Shows the browser's list of serial devices (must run right after a tap) and starts reading the chosen one. */
  async connect(): Promise<void> {
    if (!this.supported) return;
    this.failed.set(false);
    try {
      const serial = (navigator as unknown as { serial: { requestPort(): Promise<SerialPortLike> } }).serial;
      await this.start(await serial.requestPort());
    } catch (e) {
      // The cashier closing the list is not an error.
      if ((e as { name?: string })?.name !== 'NotFoundError') this.failed.set(true);
    }
  }

  /** Connects again to a scale allowed before, without a tap (after a reload). */
  async reconnect(): Promise<void> {
    if (!this.supported || this.connected()) return;
    try {
      const serial = (navigator as unknown as { serial: { getPorts(): Promise<SerialPortLike[]> } }).serial;
      const [port] = await serial.getPorts();
      if (port) await this.start(port);
    } catch { /* the scale is not there: the cashier connects it by hand */ }
  }

  async disconnect(): Promise<void> {
    const reader = this.reader;
    const port = this.port;
    this.reader = null;
    this.port = null;
    this.connected.set(false);
    this.last.set(null);
    try { await reader?.cancel(); } catch { /* already closed */ }
    try { await port?.close(); } catch { /* already closed */ }
  }

  /** The weight to use now: the latest reading, only while it is fresh, still and above zero. */
  usable(now = Date.now()): ScaleReading | null {
    const l = this.last();
    if (!l || now - l.at > FRESH_MS || !l.reading.stable || l.reading.grams <= 0) return null;
    return l.reading;
  }

  private async start(port: SerialPortLike): Promise<void> {
    await port.open({ baudRate: this.baud() });
    this.port = port;
    this.connected.set(true);
    void this.readLoop(port);
  }

  private async readLoop(port: SerialPortLike): Promise<void> {
    const decoder = new TextDecoder();
    let pending = '';
    try {
      this.reader = port.readable!.getReader();
      for (;;) {
        const { value, done } = await this.reader.read();
        if (done) break;
        pending += decoder.decode(value, { stream: true });
        const { lines, rest } = splitScaleLines(pending);
        pending = rest.slice(-200); // never grow without bound if the scale sends no line breaks
        for (const line of lines) {
          const reading = parseScaleReading(line);
          if (reading) this.last.set({ reading, at: Date.now() });
        }
      }
    } catch {
      this.failed.set(true);
    }
    if (this.port === port) await this.disconnect();
  }
}
