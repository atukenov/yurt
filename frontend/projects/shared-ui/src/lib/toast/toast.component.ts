import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService, ToastType } from './toast.service';

@Component({
  selector: 'yurt-toast-container',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div
      class="fixed right-0 z-50 flex flex-col items-end gap-2 pointer-events-none px-4"
      style="top: max(1rem, env(safe-area-inset-top))"
      role="status"
      aria-live="polite"
      aria-atomic="false"
    >
      @for (toast of toastService.toasts(); track toast.id) {
        <div
          [class]="toastClass(toast.type)"
          class="pointer-events-auto max-w-sm w-full shadow-lg rounded-2xl px-4 py-3 flex items-center gap-3 text-sm font-medium"
        >
          <span aria-hidden="true">{{ toastIcon(toast.type) }}</span>
          <span>{{ toast.message }}</span>
          <button
            (click)="toastService.remove(toast.id)"
            aria-label="Dismiss"
            class="ml-auto opacity-70 hover:opacity-100"
          >
            &times;
          </button>
        </div>
      }
    </div>
  `,
})
export class ToastContainerComponent {
  readonly toastService = inject(ToastService);

  toastClass(type: ToastType): string {
    const map: Record<ToastType, string> = {
      success: 'bg-teal-600 text-white',
      error: 'bg-red-600 text-white',
      warning: 'bg-amber-500 text-stone-900',
      info: 'bg-slate-700 text-white',
    };
    return map[type];
  }

  toastIcon(type: ToastType): string {
    const map: Record<ToastType, string> = {
      success: '\u2713',
      error: '\u2715',
      warning: '\u26a0',
      info: '\u2139',
    };
    return map[type];
  }
}
