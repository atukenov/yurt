import { BluetoothConnector, BluetoothLink } from './bluetooth-printing';

/**
 * The tablet app's own Bluetooth plugin (frontend/admin-native: YurtPrinterPlugin.java for Android,
 * YurtPrinterPlugin.swift for iPad). It reaches printers Chrome cannot: classic Bluetooth printers on Android, and
 * any Bluetooth Low Energy printer on an iPad.
 */
export interface YurtPrinterPlugin {
  /** Shows the native list of printers (paired ones on Android, nearby ones on iPad) and connects the one tapped. */
  pick(options: PickTexts): Promise<{ deviceId: string; name: string }>;
  /** Connects again to a printer picked before, without a tap. */
  connect(options: { deviceId: string }): Promise<{ deviceId: string; name: string }>;
  /** Sends ESC/POS bytes, as base64. */
  write(options: { deviceId: string; data: string }): Promise<void>;
  disconnect(options: { deviceId: string }): Promise<void>;
  addListener(event: 'disconnected', handler: (e: { deviceId: string }) => void): Promise<{ remove(): Promise<void> }>;
}

/** Texts of the native list, in the app language. */
export interface PickTexts {
  title: string;
  cancel: string;
  /** Shown when there is nothing to pick. */
  none: string;
}

export function toBase64(data: Uint8Array): string {
  let raw = '';
  for (let at = 0; at < data.length; at += 0x8000) raw += String.fromCharCode(...data.subarray(at, at + 0x8000));
  return btoa(raw);
}

/** Bluetooth through the tablet app's plugin, in place of the browser's Web Bluetooth. */
export class NativeBluetoothConnector implements BluetoothConnector {
  constructor(
    private plugin: YurtPrinterPlugin,
    private texts: () => PickTexts,
  ) {}

  supported(): boolean {
    return true;
  }

  async pick(): Promise<BluetoothLink> {
    try {
      const { deviceId, name } = await this.plugin.pick(this.texts());
      return this.link(deviceId, name);
    } catch (e) {
      // Closing the list without picking: same error name as Chrome uses, so the Printers page stays quiet.
      if ((e as { code?: string })?.code === 'CANCELLED') throw Object.assign(new Error('Closed'), { name: 'NotFoundError' });
      throw e;
    }
  }

  async reconnect(deviceId: string): Promise<BluetoothLink | null> {
    const { name } = await this.plugin.connect({ deviceId });
    return this.link(deviceId, name);
  }

  private link(deviceId: string, name: string): BluetoothLink {
    const plugin = this.plugin;
    let listener: Promise<{ remove(): Promise<void> }> | null = null;
    return {
      name: name || 'Bluetooth printer',
      deviceId,
      write: (data) => plugin.write({ deviceId, data: toBase64(data) }),
      disconnect: () => {
        void listener?.then((l) => l.remove());
        listener = null;
        void plugin.disconnect({ deviceId }).catch(() => undefined);
      },
      onDisconnect: (handler) => {
        let fired = false;
        listener = plugin.addListener('disconnected', (e) => {
          if (e.deviceId !== deviceId || fired) return;
          fired = true;
          void listener?.then((l) => l.remove());
          handler();
        });
      },
    };
  }
}
