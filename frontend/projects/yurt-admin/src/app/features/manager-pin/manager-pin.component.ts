import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { ManagerPinStatus } from 'shared-models';
import { ButtonComponent, ToastService } from 'shared-ui';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';

/**
 * My manager PIN (own_pos). With it a manager approves cancels, refunds and discounts at the till and in the orders
 * screen; each manager has their own, so the audit log shows who approved.
 */
@Component({
  selector: 'app-manager-pin',
  standalone: true,
  imports: [FormsModule, ButtonComponent, AdminTranslatePipe],
  template: `
    <div class="max-w-md">
      <h1 class="text-2xl font-bold text-slate-800">{{ 'managerPin.title' | t }}</h1>
      <p class="text-sm text-slate-500 mt-1">{{ 'managerPin.intro' | t }}</p>

      @if (status(); as s) {
        @if (!s.anyManagerHasPin) {
          <p class="mt-4 text-sm rounded-xl bg-amber-50 border border-amber-200 text-amber-800 px-4 py-3">
            {{ 'managerPin.noneYet' | t }}
          </p>
        }
        <p class="mt-4 text-sm font-semibold" [class]="s.iHavePin ? 'text-teal-700' : 'text-slate-600'">
          {{ (s.iHavePin ? 'managerPin.mineSet' : 'managerPin.mineNotSet') | t }}
        </p>
      }

      <form class="mt-4 bg-surface rounded-2xl border border-slate-100 p-5 shadow-sm space-y-4" (ngSubmit)="save()">
        <label class="block">
          <span class="block text-sm font-medium text-slate-700 mb-1.5">{{ 'managerPin.newPin' | t }}</span>
          <input type="password" name="pin" inputmode="numeric" maxlength="6" autocomplete="off" [(ngModel)]="pin"
            class="w-full border border-slate-200 rounded-xl px-4 py-3 text-lg tracking-[0.4em] text-center focus:ring-2 focus:ring-brand-400 outline-none" />
          <span class="block text-xs text-slate-500 mt-1">{{ 'managerPin.rule' | t }}</span>
        </label>
        <label class="block">
          <span class="block text-sm font-medium text-slate-700 mb-1.5">{{ 'auth.currentPw' | t }}</span>
          <input type="password" name="password" autocomplete="current-password" [(ngModel)]="password"
            class="w-full border border-slate-200 rounded-xl px-4 py-3 text-sm focus:ring-2 focus:ring-brand-400 outline-none" />
        </label>
        <yurt-button type="submit" variant="primary" size="md" [fullWidth]="true" [loading]="saving()" [disabled]="!ready()">
          {{ 'managerPin.save' | t }}
        </yurt-button>
      </form>
    </div>
  `,
})
export class ManagerPinComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);

  status = signal<ManagerPinStatus | null>(null);
  saving = signal(false);
  pin = '';
  password = '';

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.api.getManagerPinStatus().subscribe({ next: (s) => this.status.set(s), error: () => {} });
  }

  ready(): boolean {
    return /^[0-9]{4,6}$/.test(this.pin) && this.password.length > 0;
  }

  save(): void {
    if (!this.ready() || this.saving()) return;
    this.saving.set(true);
    this.api.setManagerPin(this.password, this.pin).subscribe({
      next: () => {
        this.saving.set(false);
        this.pin = '';
        this.password = '';
        this.toast.success(this.lang.t('managerPin.saved'));
        this.load();
      },
      error: () => this.saving.set(false),
    });
  }
}
