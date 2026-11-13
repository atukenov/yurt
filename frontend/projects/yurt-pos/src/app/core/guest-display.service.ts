import { Injectable, signal } from '@angular/core';
import { GuestView, IDLE_VIEW, parseView } from './guest-display';

const KEY = 'yurt_pos_guest_view';
const CHANNEL = 'yurt-pos-guest';

/**
 * Carries what the till shows to the guest screen: a second browser window on the other monitor / tablet on the
 * same computer. It uses only the browser itself (a broadcast channel plus the last view saved in the browser), so
 * it works with no internet, like the rest of the till.
 */
@Injectable({ providedIn: 'root' })
export class GuestDisplayService {
  private channel: BroadcastChannel | null = null;

  /** On the guest screen: what to draw now. */
  readonly view = signal<GuestView>({ ...IDLE_VIEW, lang: 'ru', at: new Date().toISOString() });

  /** Till side: send the current view. */
  publish(view: GuestView): void {
    try { localStorage.setItem(KEY, JSON.stringify(view)); } catch { /* private mode: the live channel still works */ }
    try {
      this.channel ??= typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel(CHANNEL);
      this.channel?.postMessage(view);
    } catch { /* no channel: the saved view is picked up by the other window's storage event */ }
  }

  /** Guest screen side: show the saved view, then follow the till. */
  listen(fallbackLang: GuestView['lang']): () => void {
    let raw: string | null = null;
    try { raw = localStorage.getItem(KEY); } catch { /* none saved */ }
    this.view.set(parseView(raw, fallbackLang));

    const onStorage = (e: StorageEvent) => {
      if (e.key === KEY) this.view.set(parseView(e.newValue, fallbackLang));
    };
    window.addEventListener('storage', onStorage);
    let channel: BroadcastChannel | null = null;
    try {
      channel = typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel(CHANNEL);
      if (channel) channel.onmessage = (e) => this.view.set(parseView(JSON.stringify(e.data), fallbackLang));
    } catch { /* storage events are enough */ }
    return () => { window.removeEventListener('storage', onStorage); channel?.close(); };
  }
}
