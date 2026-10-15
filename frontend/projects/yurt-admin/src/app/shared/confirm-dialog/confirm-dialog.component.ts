import { Component, inject } from '@angular/core';
import { ConfirmService } from './confirm.service';

@Component({
  selector: 'admin-confirm-dialog',
  standalone: true,
  template: `
    @if (svc.state()) {
      @let s = svc.state()!;
      <div
        class="fixed inset-0 bg-black/50 z-[200] flex items-center justify-center p-4"
        (click)="svc.resolve(false)"
        (keydown.escape)="svc.resolve(false)"
      >
        <div
          class="bg-white rounded-3xl shadow-2xl w-full max-w-sm p-6"
          role="dialog"
          aria-modal="true"
          aria-labelledby="confirm-dialog-title"
          aria-describedby="confirm-dialog-message"
          (click)="$event.stopPropagation()"
        >
          <div class="w-12 h-12 rounded-2xl bg-red-100 flex items-center justify-center text-2xl mx-auto mb-4">
            🗑️
          </div>
          <h3 id="confirm-dialog-title" class="text-lg font-bold text-stone-800 text-center mb-2">{{ s.title }}</h3>
          <p id="confirm-dialog-message" class="text-stone-500 text-sm text-center mb-6">{{ s.message }}</p>
          <div class="flex gap-3">
            <button
              (click)="svc.resolve(false)"
              class="flex-1 py-3 rounded-2xl border-2 border-stone-200 text-stone-600 font-semibold text-sm hover:bg-stone-50 transition-colors"
            >
              Cancel
            </button>
            <button
              (click)="svc.resolve(true)"
              class="flex-1 py-3 rounded-2xl bg-red-500 hover:bg-red-600 text-white font-semibold text-sm transition-colors active:scale-[0.98]"
            >
              {{ s.confirmLabel ?? 'Delete' }}
            </button>
          </div>
        </div>
      </div>
    }
  `,
})
export class ConfirmDialogComponent {
  readonly svc = inject(ConfirmService);
}
