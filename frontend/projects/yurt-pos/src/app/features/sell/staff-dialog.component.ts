import { Component, inject, input, OnInit, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthStateService, YurtApiService } from 'shared-api';
import { OnTheClock } from 'shared-models';
import { LocalDatePipe } from 'shared-ui';
import { PosLangService } from '../../core/lang.service';
import { PosTranslatePipe } from '../../core/translate.pipe';

/**
 * Staff PIN at the till: a cashier types their own PIN to take over the till (the next sale is theirs) or to clock in
 * and out, with no password between guests. Needs the internet: the server checks the PIN.
 */
@Component({
  selector: 'pos-staff-dialog',
  imports: [FormsModule, LocalDatePipe, PosTranslatePipe],
  template: `
    <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" (click)="closed.emit()">
      <div class="w-full max-w-sm space-y-4 rounded-2xl bg-surface p-5" (click)="$event.stopPropagation()" role="dialog" aria-modal="true"
           [attr.aria-label]="'staff.title' | t">
        <div class="flex items-center justify-between">
          <h2 class="text-lg font-bold text-slate-900">{{ 'staff.title' | t }}</h2>
          <button type="button" (click)="closed.emit()" class="rounded-lg px-3 py-2 text-sm ring-1 ring-slate-200">{{ 'sales.close' | t }}</button>
        </div>
        <p class="text-sm text-slate-600">{{ 'staff.hint' | t }}</p>

        <input type="password" name="pin" inputmode="numeric" maxlength="6" autocomplete="off" autofocus [(ngModel)]="pin"
               [attr.aria-label]="'staff.pin' | t" [placeholder]="'staff.pin' | t"
               class="w-full rounded-xl border border-slate-300 px-4 py-3 text-center text-2xl tracking-[0.5em]" />

        @if (message()) {
          <p class="rounded-lg px-3 py-2 text-sm" [class]="messageIsError() ? 'bg-red-50 text-red-800' : 'bg-green-50 text-green-800'" role="status">
            {{ message() }}
          </p>
        }

        <div class="grid grid-cols-2 gap-2">
          <button type="button" (click)="switchTo()" [disabled]="busy() || !valid()"
                  class="rounded-xl bg-brand-500 px-4 py-3 text-base font-semibold text-on-brand disabled:opacity-50">
            {{ 'staff.switch' | t }}
          </button>
          <button type="button" (click)="punch()" [disabled]="busy() || !valid()"
                  class="rounded-xl px-4 py-3 text-base font-semibold text-slate-800 ring-1 ring-slate-300 disabled:opacity-50">
            {{ 'staff.clock' | t }}
          </button>
        </div>

        <div>
          <h3 class="text-sm font-semibold text-slate-700">{{ 'staff.onClock' | t }}</h3>
          @if (!onClock().length) {
            <p class="text-sm text-slate-500">{{ 'staff.nobody' | t }}</p>
          }
          @for (p of onClock(); track p.entryId) {
            <p class="text-sm text-slate-700">{{ p.username }} · {{ 'staff.since' | t }} {{ p.clockInAt | ldate: 'time24' }}</p>
          }
        </div>
      </div>
    </div>
  `,
})
export class StaffDialogComponent implements OnInit {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private lang = inject(PosLangService);

  /** The branch this till is at, written on the time entry. */
  readonly branchId = input<string | null>(null);
  readonly closed = output<void>();
  /** The till now belongs to this person. */
  readonly switched = output<string>();

  pin = '';
  readonly busy = signal(false);
  readonly message = signal('');
  readonly messageIsError = signal(false);
  readonly onClock = signal<OnTheClock[]>([]);

  ngOnInit(): void {
    this.loadOnClock();
  }

  valid(): boolean {
    return /^[0-9]{4,6}$/.test(this.pin);
  }

  private loadOnClock(): void {
    this.api.getOnTheClock().subscribe({ next: (l) => this.onClock.set(l), error: () => {} });
  }

  switchTo(): void {
    if (!this.valid() || this.busy()) return;
    this.busy.set(true);
    this.api.switchStaff(this.pin, this.auth.currentUser?.refreshToken ?? null).subscribe({
      next: (res) => {
        this.busy.set(false);
        this.auth.setUser({
          accessToken: res.accessToken, refreshToken: res.refreshToken, userId: res.userId,
          displayName: res.displayName, userType: 'admin', role: res.role, cafe: res.cafe ?? null,
        });
        this.pin = '';
        this.switched.emit(res.displayName);
      },
      error: (e: HttpErrorResponse) => this.fail(e),
    });
  }

  punch(): void {
    if (!this.valid() || this.busy()) return;
    this.busy.set(true);
    this.api.punchClock(this.pin, this.branchId()).subscribe({
      next: (r) => {
        this.busy.set(false);
        this.pin = '';
        const time = new Date(r.at).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        const text = r.action === 'in'
          ? this.lang.t('staff.clockedIn', { name: r.username, time })
          : this.lang.t('staff.clockedOut', { name: r.username, time, hours: (r.hoursThisShift ?? 0).toFixed(1) });
        this.show(r.forgotLast ? `${text} ${this.lang.t('staff.forgot')}` : text, false);
        this.loadOnClock();
      },
      error: (e: HttpErrorResponse) => this.fail(e),
    });
  }

  private show(text: string, error: boolean): void {
    this.message.set(text);
    this.messageIsError.set(error);
  }

  private fail(e: HttpErrorResponse): void {
    this.busy.set(false);
    this.pin = '';
    if (e.status === 0) return this.show(this.lang.t('staff.offline'), true);
    const title = (e.error as { title?: string } | null)?.title;
    this.show(title || this.lang.t('staff.failed'), true);
  }
}
