import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthStateService, YurtApiService } from 'shared-api';
import { StaffPinPerson } from 'shared-models';
import { ButtonComponent, ToastService } from 'shared-ui';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';

/**
 * Staff PIN (own_pos): my own PIN for the till, where it switches the till to me and clocks me in and out. Managers can
 * also set or clear the PIN of the people they look after (an Admin their workers, the CEO everybody).
 */
@Component({
  selector: 'app-staff-pin',
  standalone: true,
  imports: [FormsModule, ButtonComponent, AdminTranslatePipe],
  template: `
    <div class="p-4 md:p-6 max-w-2xl mx-auto space-y-6">
      <div>
        <h1 class="text-2xl font-bold text-slate-800">{{ 'staffPin.title' | t }}</h1>
        <p class="text-sm text-slate-500 mt-1">{{ 'staffPin.intro' | t }}</p>
      </div>

      <form class="bg-surface rounded-2xl border border-slate-100 p-5 shadow-sm space-y-4 max-w-md" (ngSubmit)="saveMine()">
        <p class="text-sm font-semibold" [class]="iHavePin() ? 'text-teal-700' : 'text-slate-600'">
          {{ (iHavePin() ? 'staffPin.mineSet' : 'staffPin.mineNotSet') | t }}
        </p>
        <label class="block">
          <span class="block text-sm font-medium text-slate-700 mb-1.5">{{ 'staffPin.newPin' | t }}</span>
          <input type="password" name="pin" inputmode="numeric" maxlength="6" autocomplete="off" [(ngModel)]="pin"
            class="w-full border border-slate-200 rounded-xl px-4 py-3 text-lg tracking-[0.4em] text-center focus:ring-2 focus:ring-brand-400 outline-none" />
          <span class="block text-xs text-slate-500 mt-1">{{ 'staffPin.rule' | t }}</span>
        </label>
        <label class="block">
          <span class="block text-sm font-medium text-slate-700 mb-1.5">{{ 'auth.currentPw' | t }}</span>
          <input type="password" name="password" autocomplete="current-password" [(ngModel)]="password"
            class="w-full border border-slate-200 rounded-xl px-4 py-3 text-sm focus:ring-2 focus:ring-brand-400 outline-none" />
        </label>
        <yurt-button type="submit" variant="primary" size="md" [fullWidth]="true" [loading]="saving()" [disabled]="!validMine()">
          {{ 'staffPin.save' | t }}
        </yurt-button>
      </form>

      @if (isAdmin && people().length) {
        <section class="bg-surface rounded-2xl border border-slate-100 p-5 shadow-sm">
          <h2 class="font-semibold text-slate-800">{{ 'staffPin.team' | t }}</h2>
          <p class="text-xs text-slate-500 mb-3">{{ 'staffPin.teamHint' | t }}</p>
          @for (p of people(); track p.id) {
            <div class="flex flex-wrap items-center gap-2 mb-2">
              <span class="w-44 text-sm font-semibold text-slate-700">
                {{ p.username }}
                <span class="block text-xs font-normal text-slate-500">{{ ('role.' + p.role) | t }} · {{ (p.hasPin ? 'staffPin.has' : 'staffPin.hasNot') | t }}</span>
              </span>
              @if (p.isActive) {
                <input type="password" inputmode="numeric" maxlength="6" autocomplete="off" [(ngModel)]="edits[p.id]" [name]="'pin-' + p.id"
                  [attr.aria-label]="'staffPin.newPin' | t" class="w-28 px-3 py-1.5 rounded-xl border border-slate-200 text-sm text-center tracking-widest" />
                <button type="button" (click)="setFor(p)" [disabled]="busy() || !/^[0-9]{4,6}$/.test(edits[p.id] ?? '')"
                  class="px-3 py-1.5 rounded-xl bg-brand-500 text-on-brand text-xs font-semibold disabled:opacity-50">{{ 'action.save' | t }}</button>
              }
              @if (p.hasPin) {
                <button type="button" (click)="clear(p)" [disabled]="busy()"
                  class="px-3 py-1.5 rounded-xl bg-slate-100 text-slate-700 text-xs font-semibold disabled:opacity-50">{{ 'staffPin.clear' | t }}</button>
              }
            </div>
          }
        </section>
      }
    </div>
  `,
})
export class StaffPinComponent implements OnInit {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);

  readonly isAdmin = this.auth.isAdmin;
  iHavePin = signal(false);
  people = signal<StaffPinPerson[]>([]);
  saving = signal(false);
  busy = signal(false);
  pin = '';
  password = '';
  edits: Record<string, string> = {};

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.api.getMyStaffPin().subscribe({ next: (s) => this.iHavePin.set(s.iHavePin), error: () => {} });
    if (this.isAdmin) this.api.getStaffPinPeople().subscribe({ next: (l) => this.people.set(l), error: () => {} });
  }

  validMine(): boolean {
    return /^[0-9]{4,6}$/.test(this.pin) && this.password.length > 0;
  }

  saveMine(): void {
    if (!this.validMine() || this.saving()) return;
    this.saving.set(true);
    this.api.setMyStaffPin(this.password, this.pin).subscribe({
      next: () => {
        this.saving.set(false);
        this.pin = '';
        this.password = '';
        this.toast.success(this.lang.t('staffPin.saved'));
        this.load();
      },
      error: () => this.saving.set(false),
    });
  }

  setFor(p: StaffPinPerson): void {
    this.busy.set(true);
    this.api.setStaffPinFor(p.id, this.edits[p.id]).subscribe({
      next: () => {
        this.busy.set(false);
        this.edits[p.id] = '';
        this.toast.success(this.lang.t('staffPin.saved'));
        this.load();
      },
      error: () => this.busy.set(false),
    });
  }

  clear(p: StaffPinPerson): void {
    this.busy.set(true);
    this.api.clearStaffPin(p.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.toast.success(this.lang.t('staffPin.cleared'));
        this.load();
      },
      error: () => this.busy.set(false),
    });
  }
}
