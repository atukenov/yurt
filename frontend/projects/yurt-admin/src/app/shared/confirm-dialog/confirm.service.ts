import { Injectable, signal } from '@angular/core';

export interface ConfirmState {
  title: string;
  message: string;
  confirmLabel?: string;
}

@Injectable({ providedIn: 'root' })
export class ConfirmService {
  readonly state = signal<ConfirmState | null>(null);

  private resolver: ((value: boolean) => void) | null = null;

  /** Without a `confirmLabel` the button says Delete in the admin's language. */
  confirm(title: string, message: string, confirmLabel?: string): Promise<boolean> {
    this.state.set({ title, message, confirmLabel });
    return new Promise((resolve) => {
      this.resolver = resolve;
    });
  }

  resolve(confirmed: boolean): void {
    this.state.set(null);
    this.resolver?.(confirmed);
    this.resolver = null;
  }
}
