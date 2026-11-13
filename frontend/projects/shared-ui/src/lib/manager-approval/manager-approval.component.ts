import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonComponent } from '../button/button.component';

/** What the manager typed: their PIN and why the action is needed. */
export interface ManagerApproval {
  pin: string;
  reason: string;
}

/**
 * The manager PIN dialog (module own_pos): cancels, refunds and discounts ask for a manager's PIN and a reason.
 * The apps give it their own words (admin and till have separate language files). Extra fields, such as the size of a
 * discount, go inside the tag and show above the reason. The server checks the PIN; this only collects it.
 */
@Component({
  selector: 'yurt-manager-approval',
  standalone: true,
  imports: [FormsModule, ButtonComponent],
  template: `
    <div class="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4 overflow-y-auto" (keydown.escape)="closed.emit()">
      <form
        class="bg-surface rounded-2xl shadow-2xl w-full max-w-sm p-6 m-auto max-h-full overflow-y-auto"
        role="dialog"
        aria-modal="true"
        aria-labelledby="manager-approval-title"
        (ngSubmit)="submit()"
      >
        <h3 id="manager-approval-title" class="text-lg font-bold text-slate-800">{{ title }}</h3>
        @if (hint) {
          <p class="text-sm text-slate-500 mt-1">{{ hint }}</p>
        }
        <div class="mt-4 space-y-3">
          <ng-content />
          <label class="block">
            <span class="block text-xs font-semibold text-slate-600 mb-1">{{ reasonLabel }}</span>
            <textarea
              name="reason"
              [(ngModel)]="reason"
              rows="2"
              maxlength="200"
              class="w-full border border-slate-200 rounded-xl px-3 py-2 text-sm focus:ring-2 focus:ring-brand-400 outline-none resize-none"
            ></textarea>
          </label>
          <label class="block">
            <span class="block text-xs font-semibold text-slate-600 mb-1">{{ pinLabel }}</span>
            <input
              name="pin"
              type="password"
              inputmode="numeric"
              autocomplete="off"
              maxlength="6"
              [(ngModel)]="pin"
              class="w-full border border-slate-200 rounded-xl px-3 py-2 text-lg tracking-[0.4em] text-center focus:ring-2 focus:ring-brand-400 outline-none"
            />
          </label>
        </div>
        <div class="flex gap-3 mt-5">
          <yurt-button variant="ghost" size="md" [fullWidth]="true" (clicked)="closed.emit()">{{ cancelLabel }}</yurt-button>
          <yurt-button
            type="submit"
            [variant]="destructive ? 'destructive' : 'primary'"
            size="md"
            [fullWidth]="true"
            [loading]="busy"
            [disabled]="!ready"
          >{{ confirmLabel }}</yurt-button>
        </div>
      </form>
    </div>
  `,
})
export class ManagerApprovalComponent {
  @Input() title = '';
  @Input() hint = '';
  @Input() reasonLabel = '';
  @Input() pinLabel = '';
  @Input() confirmLabel = '';
  @Input() cancelLabel = '';
  @Input() destructive = true;
  /** The action is being sent; the buttons wait. */
  @Input() busy = false;
  /** The parent can block the button for its own extra fields (e.g. a discount with no amount). */
  @Input() extraReady = true;
  @Output() approved = new EventEmitter<ManagerApproval>();
  @Output() closed = new EventEmitter<void>();

  pin = '';
  reason = '';

  get ready(): boolean {
    return this.extraReady && /^[0-9]{4,6}$/.test(this.pin) && this.reason.trim().length > 0;
  }

  submit(): void {
    if (!this.ready || this.busy) return;
    this.approved.emit({ pin: this.pin, reason: this.reason.trim() });
  }
}
