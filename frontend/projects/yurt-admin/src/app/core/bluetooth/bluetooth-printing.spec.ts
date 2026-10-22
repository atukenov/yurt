import {
  BluetoothJob,
  BluetoothLink,
  BluetoothPrintPump,
  fromBase64,
  pickWritable,
  WebBluetoothConnector,
  WritableCharacteristic,
  writeInChunks,
} from './bluetooth-printing';

/** A pretend Bluetooth printer: keeps what it was sent, can be told to fail or to go away. */
class FakeLink implements BluetoothLink {
  received: Uint8Array[] = [];
  fail: string | null = null;
  disconnected = false;
  private gone: (() => void) | null = null;
  constructor(readonly name = 'MPT-II', readonly deviceId = 'dev-1') {}
  async write(data: Uint8Array): Promise<void> {
    if (this.fail) throw new Error(this.fail);
    this.received.push(data);
  }
  disconnect(): void {
    this.disconnected = true;
  }
  onDisconnect(handler: () => void): void {
    this.gone = handler;
  }
  switchOff(): void {
    this.gone?.();
  }
}

class FakeApi {
  waiting: BluetoothJob[] = [];
  claimedWith: string[][] = [];
  reports: { jobId: string; ok: boolean; error?: string }[] = [];
  async claim(printerIds: string[]): Promise<BluetoothJob[]> {
    this.claimedWith.push(printerIds);
    const mine = this.waiting.filter((j) => printerIds.includes(j.printerId));
    this.waiting = this.waiting.filter((j) => !mine.includes(j));
    return mine;
  }
  async report(jobId: string, ok: boolean, error?: string): Promise<void> {
    this.reports.push({ jobId, ok, error });
  }
}

const job = (id: string, printerId: string, text: string): BluetoothJob => ({ id, printerId, kind: 'Ticket', data: btoa(text) });

class FakeCharacteristic implements WritableCharacteristic {
  writes: { bytes: number[]; confirmed: boolean }[] = [];
  constructor(public properties: { write: boolean; writeWithoutResponse: boolean }) {}
  async writeValueWithResponse(value: BufferSource): Promise<void> {
    this.writes.push({ bytes: [...new Uint8Array(value as ArrayBuffer)], confirmed: true });
  }
  async writeValueWithoutResponse(value: BufferSource): Promise<void> {
    this.writes.push({ bytes: [...new Uint8Array(value as ArrayBuffer)], confirmed: false });
  }
}

describe('Bluetooth printing', () => {
  it('prints the jobs of connected printers and reports back', async () => {
    const api = new FakeApi();
    const pump = new BluetoothPrintPump(api);
    const link = new FakeLink();
    api.waiting = [job('j1', 'p1', 'Latte'), job('j2', 'other', 'Not mine')];

    expect(await pump.tick()).toBe(0); // Nothing connected: the server is not even asked.
    expect(api.claimedWith).toEqual([]);

    pump.attach('p1', link);
    expect(await pump.tick()).toBe(1);
    expect(api.claimedWith).toEqual([['p1']]);
    expect(new TextDecoder().decode(link.received[0])).toBe('Latte');
    expect(api.reports).toEqual([{ jobId: 'j1', ok: true, error: undefined }]);
    expect(pump.connected.get('p1')).toEqual({ name: 'MPT-II', printed: 1, lastError: undefined });
    expect(api.waiting.map((j) => j.id)).toEqual(['j2']); // Another tablet's printer: left for it.
  });

  it('reports a failed print with the reason', async () => {
    const api = new FakeApi();
    const pump = new BluetoothPrintPump(api);
    const link = new FakeLink();
    link.fail = 'GATT operation failed';
    pump.attach('p1', link);
    api.waiting = [job('j1', 'p1', 'x')];

    expect(await pump.tick()).toBe(0);
    expect(api.reports).toEqual([{ jobId: 'j1', ok: false, error: 'GATT operation failed' }]);
    expect(pump.connected.get('p1')?.lastError).toBe('GATT operation failed');
  });

  it('gives up on a printer that never answers', async () => {
    const api = new FakeApi();
    const pump = new BluetoothPrintPump(api, { sendTimeoutMs: 20 });
    const link = new FakeLink();
    link.write = () => new Promise(() => {});
    pump.attach('p1', link);
    api.waiting = [job('j1', 'p1', 'x')];

    await pump.tick();
    expect(api.reports).toEqual([{ jobId: 'j1', ok: false, error: 'The Bluetooth printer did not answer.' }]);
  });

  it('stops asking for a printer that was switched off or disconnected', async () => {
    const api = new FakeApi();
    const changes: number[] = [];
    const pump = new BluetoothPrintPump(api, { onChange: () => changes.push(pump.connected.size) });
    const a = new FakeLink('A', 'dev-a');
    const b = new FakeLink('B', 'dev-b');
    pump.attach('pa', a);
    pump.attach('pb', b);
    a.switchOff();
    await pump.tick();
    expect(api.claimedWith).toEqual([['pb']]);

    pump.detach('pb');
    expect(b.disconnected).toBe(true);
    expect(await pump.tick()).toBe(0);
    expect(api.claimedWith.length).toBe(1);
    expect(changes).toEqual([1, 2, 1, 0]);
  });

  it('keeps going when the server cannot be reached', async () => {
    const pump = new BluetoothPrintPump({
      claim: () => Promise.reject(new Error('offline')),
      report: () => Promise.resolve(),
    });
    pump.attach('p1', new FakeLink());
    expect(await pump.tick()).toBe(0);
  });

  it('sends long jobs in parts, paced by the printer when it confirms writes', async () => {
    const confirming = new FakeCharacteristic({ write: true, writeWithoutResponse: true });
    await writeInChunks(confirming, new Uint8Array(250).map((_, i) => i), 100);
    expect(confirming.writes.map((w) => w.bytes.length)).toEqual([100, 100, 50]);
    expect(confirming.writes.every((w) => w.confirmed)).toBe(true);
    expect(confirming.writes[2].bytes[0]).toBe(200);

    const pauses: number[] = [];
    const quiet = new FakeCharacteristic({ write: false, writeWithoutResponse: true });
    await writeInChunks(quiet, new Uint8Array(30), 20, async (ms) => void pauses.push(ms));
    expect(quiet.writes.map((w) => [w.bytes.length, w.confirmed])).toEqual([[20, false], [10, false]]);
    expect(pauses).toEqual([20, 20]);
  });

  it('picks a characteristic that accepts writes', () => {
    const read = new FakeCharacteristic({ write: false, writeWithoutResponse: false });
    const quiet = new FakeCharacteristic({ write: false, writeWithoutResponse: true });
    const confirming = new FakeCharacteristic({ write: true, writeWithoutResponse: false });
    expect(pickWritable([read, quiet, confirming])).toBe(confirming);
    expect(pickWritable([read, quiet])).toBe(quiet);
    expect(pickWritable([read])).toBeNull();
  });

  it('decodes job bytes from base64, including bytes above 127', () => {
    expect([...fromBase64(btoa(String.fromCharCode(27, 64, 0xa0, 0xff)))]).toEqual([27, 64, 0xa0, 0xff]);
  });

  it('connects through Web Bluetooth to the first known printer service', async () => {
    const characteristic = new FakeCharacteristic({ write: true, writeWithoutResponse: false });
    let requested: { optionalServices: string[] } | null = null;
    const device = {
      id: 'dev-9',
      name: 'Printer001',
      gatt: {
        connected: true,
        connect: async () => ({
          getPrimaryServices: async () => [
            { uuid: '0000180a-0000-1000-8000-00805f9b34fb', getCharacteristics: async () => [characteristic] },
            { uuid: 'E7810A71-73AE-499D-8C15-FAA9AEF0C3F2', getCharacteristics: async () => [characteristic] },
          ],
        }),
        disconnect: () => {},
      },
      addEventListener: () => {},
    };
    const connector = new WebBluetoothConnector({
      bluetooth: {
        requestDevice: async (o) => {
          requested = o;
          return device;
        },
        getDevices: async () => [device],
      },
    });
    expect(connector.supported()).toBe(true);
    const link = await connector.pick();
    expect(requested!.optionalServices).toContain('000018f0-0000-1000-8000-00805f9b34fb');
    expect(link.name).toBe('Printer001');
    await link.write(new Uint8Array([1, 2, 3]));
    expect(characteristic.writes[0].bytes).toEqual([1, 2, 3]);
    expect((await connector.reconnect('dev-9'))?.deviceId).toBe('dev-9');
    expect(await connector.reconnect('missing')).toBeNull();
    expect(new WebBluetoothConnector({}).supported()).toBe(false);
  });

  it('refuses a device that is not a printer', async () => {
    let disconnected = false;
    const device = {
      id: 'watch',
      gatt: {
        connected: true,
        connect: async () => ({ getPrimaryServices: async () => [] }),
        disconnect: () => (disconnected = true),
      },
      addEventListener: () => {},
    };
    const connector = new WebBluetoothConnector({ bluetooth: { requestDevice: async () => device } });
    await expect(connector.pick()).rejects.toThrow('does not look like a Bluetooth printer');
    expect(disconnected).toBe(true);
  });
});
