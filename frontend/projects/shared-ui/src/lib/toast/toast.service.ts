import { Injectable, signal } from '@angular/core';

export type ToastType = 'success' | 'error' | 'info' | 'warning';

export interface Toast {
  id: string;
  message: string;
  type: ToastType;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly toasts = signal<Toast[]>([]);
  private timers = new Map<string, ReturnType<typeof setTimeout>>();

  show(message: string, type: ToastType = 'info', duration = 3500): void {
    const id = crypto.randomUUID();
    this.toasts.update((t) => [...t, { id, message, type }]);
    const timer = setTimeout(() => this.remove(id), duration);
    this.timers.set(id, timer);
  }

  remove(id: string): void {
    this.toasts.update((t) => t.filter((x) => x.id !== id));
    const timer = this.timers.get(id);
    if (timer) {
      clearTimeout(timer);
      this.timers.delete(id);
    }
  }

  success(msg: string): void {
    this.show(msg, 'success');
  }
  error(msg: string): void {
    this.show(msg, 'error');
  }
  warning(msg: string): void {
    this.show(msg, 'warning');
  }
  info(msg: string): void {
    this.show(msg, 'info');
  }
}
