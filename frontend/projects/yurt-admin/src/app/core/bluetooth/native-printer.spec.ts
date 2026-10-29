import { BluetoothJob, BluetoothPrintPump } from './bluetooth-printing';
import { NativeBluetoothConnector, PickTexts, toBase64, YurtPrinterPlugin } from './native-printer';

/** A pretend tablet-app plugin: keeps what was sent, can fire "disconnected". */
class FakePlugin implements YurtPrinterPlugin {
  pickedWith: PickTexts[] = [];
  pickError: unknown = null;
  writes: { deviceId: string; data: string }[] = [];
  disconnects: string[] = [];
  listeners: ((e: { deviceId: string }) => void)[] = [];
  removed = 0;

  async pick(options: PickTexts): Promise<{ deviceId: string; name: string }> {
    this.pickedWith.push(options);
    if (this.pickError) throw this.pickError;
    return { deviceId: 'AA:BB:CC:DD:EE:FF', name: 'XP-58' };
  }
  async connect(options: { deviceId: string }): Promise<{ deviceId: string; name: string }> {
    if (options.deviceId === 'gone') throw new Error('Unknown printer.');
    return { deviceId: options.deviceId, name: 'XP-58' };
  }
  async write(options: { deviceId: string; data: string }): Promise<void> {
    this.writes.push(options);
  }
  async disconnect(options: { deviceId: string }): Promise<void> {
    this.disconnects.push(options.deviceId);
  }
  addListener(_event: 'disconnected', handler: (e: { deviceId: string }) => void): Promise<{ remove(): Promise<void> }> {
    this.listeners.push(handler);
    return Promise.resolve({
      remove: async () => {
        this.removed++;
        this.listeners = this.listeners.filter((l) => l !== handler);
      },
    });
  }
  fire(deviceId: string): void {
    for (const l of [...this.listeners]) l({ deviceId });
  }
}

const texts: PickTexts = { title: 'Выберите принтер', cancel: 'Отмена', none: 'Нет устройств' };

describe('Tablet app Bluetooth (native plugin)', () => {
  it('encodes bytes as base64, also long jobs', () => {
    expect(toBase64(new TextEncoder().encode('Latte'))).toBe(btoa('Latte'));
    const long = new Uint8Array(100_000).map((_, i) => i % 256);
    expect(atob(toBase64(long)).length).toBe(100_000);
    expect(atob(toBase64(long)).charCodeAt(70_000)).toBe(70_000 % 256);
  });

  it('picks a printer with the texts in the app language and sends jobs through the plugin', async () => {
    const plugin = new FakePlugin();
    const link = await new NativeBluetoothConnector(plugin, () => texts).pick();
    expect(plugin.pickedWith).toEqual([texts]);
    expect(link.name).toBe('XP-58');
    expect(link.deviceId).toBe('AA:BB:CC:DD:EE:FF');

    const receipt = toBase64(new TextEncoder().encode('Чек'));
    const claims: BluetoothJob[] = [{ id: 'j1', printerId: 'p1', kind: 'Receipt', data: receipt }];
    const reports: { jobId: string; ok: boolean }[] = [];
    const pump = new BluetoothPrintPump({
      claim: async () => claims.splice(0),
      report: async (jobId, ok) => {
        reports.push({ jobId, ok });
      },
    });
    pump.attach('p1', link);
    expect(await pump.tick()).toBe(1);
    expect(plugin.writes).toEqual([{ deviceId: 'AA:BB:CC:DD:EE:FF', data: receipt }]);
    expect(reports).toEqual([{ jobId: 'j1', ok: true }]);
  });

  it('closing the list is quiet, like closing Chrome\'s list', async () => {
    const plugin = new FakePlugin();
    plugin.pickError = Object.assign(new Error('Closed without picking a printer.'), { code: 'CANCELLED' });
    await expect(new NativeBluetoothConnector(plugin, () => texts).pick()).rejects.toMatchObject({ name: 'NotFoundError' });

    plugin.pickError = Object.assign(new Error('Нет устройств'), { code: 'NO_DEVICES' });
    await expect(new NativeBluetoothConnector(plugin, () => texts).pick()).rejects.toThrow('Нет устройств');
  });

  it('tells the pump once when its own printer goes away, and not about other printers', async () => {
    const plugin = new FakePlugin();
    const connector = new NativeBluetoothConnector(plugin, () => texts);
    const link = await connector.reconnect('AA:BB:CC:DD:EE:FF');
    const pump = new BluetoothPrintPump({ claim: async () => [], report: async () => undefined });
    pump.attach('p1', link!);

    plugin.fire('11:22:33:44:55:66');
    expect(pump.connected.has('p1')).toBe(true);
    plugin.fire('AA:BB:CC:DD:EE:FF');
    plugin.fire('AA:BB:CC:DD:EE:FF');
    expect(pump.connected.has('p1')).toBe(false);
    await Promise.resolve();
    expect(plugin.removed).toBe(1);
  });

  it('disconnecting closes the printer in the plugin and stops listening', async () => {
    const plugin = new FakePlugin();
    const pump = new BluetoothPrintPump({ claim: async () => [], report: async () => undefined });
    pump.attach('p1', await new NativeBluetoothConnector(plugin, () => texts).reconnect('AA:BB:CC:DD:EE:FF') as never);
    pump.detach('p1');
    await Promise.resolve();
    await Promise.resolve();
    expect(plugin.disconnects).toEqual(['AA:BB:CC:DD:EE:FF']);
    expect(plugin.listeners).toEqual([]);
  });

  it('a printer the tablet no longer knows fails to reconnect', async () => {
    await expect(new NativeBluetoothConnector(new FakePlugin(), () => texts).reconnect('gone')).rejects.toThrow('Unknown printer.');
  });
});
