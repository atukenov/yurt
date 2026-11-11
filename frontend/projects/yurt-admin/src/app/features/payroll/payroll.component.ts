import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { LocalDatePipe, LocalNumberPipe, ToastService } from 'shared-ui';
import { PayrollSheet, PayrollStaff } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';

/** Today's month as "yyyy-MM" in the browser's own time zone. */
export function currentMonth(now = new Date()): string {
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
}

/**
 * Monthly pay (CEO only): a fixed salary per person, bonuses and deductions for the month, and a pay sheet the CEO
 * closes after everyone is paid. Nothing here is worked out from hours.
 */
@Component({
  selector: 'app-payroll',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe, LocalNumberPipe, LocalDatePipe],
  template: `
    <div class="p-4 md:p-6 max-w-5xl mx-auto space-y-6">
      <div>
        <h1 class="text-2xl font-bold text-slate-800">{{ 'payroll.title' | t }}</h1>
        <p class="text-slate-500 text-sm mt-0.5">{{ 'payroll.hint' | t }}</p>
      </div>

      <!-- Pay sheet of the chosen month -->
      <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5">
        <div class="flex flex-wrap items-center gap-3 mb-4">
          <label class="text-sm font-semibold text-slate-700" for="payroll-month">{{ 'payroll.month' | t }}</label>
          <input id="payroll-month" type="month" [ngModel]="month()" (ngModelChange)="setMonth($event)"
            class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm" />
          @if (sheet(); as s) {
            @if (s.closed) {
              <span class="text-xs font-semibold px-2 py-0.5 rounded-full bg-green-100 text-green-700">
                {{ 'payroll.closed' | t }} · {{ s.closedBy }} · {{ s.closedAt | ldate: 'shortDate' }}
              </span>
            }
          }
        </div>

        @if (!sheet()) {
          <div class="h-24 bg-slate-100 rounded-2xl animate-pulse"></div>
        } @else if (!sheet()!.lines.length) {
          <p class="text-slate-500 text-sm py-6 text-center">{{ 'payroll.empty' | t }}</p>
        } @else {
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b border-slate-100 text-slate-500 text-xs uppercase tracking-wide">
                  <th class="text-left px-3 py-2">{{ 'payroll.person' | t }}</th>
                  <th class="text-right px-3 py-2">{{ 'payroll.salary' | t }}</th>
                  <th class="text-right px-3 py-2">{{ 'payroll.bonuses' | t }}</th>
                  <th class="text-right px-3 py-2">{{ 'payroll.deductions' | t }}</th>
                  <th class="text-right px-3 py-2">{{ 'payroll.total' | t }}</th>
                </tr>
              </thead>
              <tbody>
                @for (l of sheet()!.lines; track l.adminUserId) {
                  <tr class="border-b border-slate-50 align-top">
                    <td class="px-3 py-2">
                      <p class="font-medium text-slate-800">{{ l.username }}</p>
                      <p class="text-xs text-slate-500">{{ ('role.' + l.role) | t }}</p>
                      @for (a of l.adjustments; track a.id) {
                        <p class="text-xs text-slate-500 mt-1">
                          <span [class]="a.isDeduction ? 'text-tone-red' : 'text-green-700'">
                            {{ a.isDeduction ? '−' : '+' }}{{ a.amount | lnumber: '1.0-2' }}
                          </span>
                          · {{ a.reason }}
                          @if (!sheet()!.closed) {
                            <button type="button" (click)="removeAdjustment(a.id)" class="ml-1 underline text-slate-500 hover:text-slate-700">
                              {{ 'action.delete' | t }}
                            </button>
                          }
                        </p>
                      }
                    </td>
                    <td class="px-3 py-2 text-right">{{ l.salary | lnumber: '1.0-2' }}</td>
                    <td class="px-3 py-2 text-right text-green-700">{{ l.bonuses | lnumber: '1.0-2' }}</td>
                    <td class="px-3 py-2 text-right text-tone-red">{{ l.deductions | lnumber: '1.0-2' }}</td>
                    <td class="px-3 py-2 text-right font-semibold text-slate-800">{{ l.total | lnumber: '1.0-2' }}</td>
                  </tr>
                }
              </tbody>
              <tfoot>
                <tr class="font-semibold text-slate-800">
                  <td class="px-3 py-2">{{ 'payroll.totals' | t }}</td>
                  <td class="px-3 py-2 text-right">{{ sheet()!.totalSalary | lnumber: '1.0-2' }}</td>
                  <td class="px-3 py-2 text-right">{{ sheet()!.totalBonuses | lnumber: '1.0-2' }}</td>
                  <td class="px-3 py-2 text-right">{{ sheet()!.totalDeductions | lnumber: '1.0-2' }}</td>
                  <td class="px-3 py-2 text-right">{{ sheet()!.totalToPay | lnumber: '1.0-2' }}</td>
                </tr>
              </tfoot>
            </table>
          </div>
        }

        @if (sheet(); as s) {
          <div class="mt-4 flex flex-wrap items-center gap-3">
            @if (s.closed) {
              <button type="button" (click)="reopen()" [disabled]="busy()"
                class="px-4 py-2 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-700 text-sm font-semibold disabled:opacity-50">
                {{ 'payroll.reopen' | t }}
              </button>
            } @else {
              <button type="button" (click)="close()" [disabled]="busy() || !s.lines.length"
                class="px-4 py-2 rounded-xl bg-brand-500 hover:bg-brand-600 text-on-brand text-sm font-semibold disabled:opacity-50">
                {{ 'payroll.close' | t }}
              </button>
              <p class="text-xs text-slate-500">{{ 'payroll.closeHint' | t }}</p>
            }
          </div>
        }
      </section>

      <!-- Bonus or deduction -->
      @if (sheet() && !sheet()!.closed) {
        <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5">
          <h2 class="font-semibold text-slate-800 mb-3">{{ 'payroll.add' | t }}</h2>
          <form class="grid gap-3 md:grid-cols-5" (ngSubmit)="addAdjustment()">
            <select name="person" [(ngModel)]="adjUser" [attr.aria-label]="'payroll.person' | t" class="px-3 py-2 rounded-xl border border-slate-200 text-sm">
              <option value="">{{ 'payroll.person' | t }}</option>
              @for (p of activeStaff(); track p.id) {
                <option [value]="p.id">{{ p.username }}</option>
              }
            </select>
            <select name="kind" [(ngModel)]="adjKind" [attr.aria-label]="'payroll.kind' | t" class="px-3 py-2 rounded-xl border border-slate-200 text-sm">
              <option value="bonus">{{ 'payroll.bonus' | t }}</option>
              <option value="deduction">{{ 'payroll.deduction' | t }}</option>
            </select>
            <input name="amount" type="number" min="0" step="any" [(ngModel)]="adjAmount" [attr.aria-label]="'payroll.amount' | t"
              [placeholder]="'payroll.amount' | t" class="px-3 py-2 rounded-xl border border-slate-200 text-sm" />
            <input name="reason" type="text" maxlength="200" [(ngModel)]="adjReason" [attr.aria-label]="'payroll.reason' | t"
              [placeholder]="'payroll.reasonHint' | t" class="px-3 py-2 rounded-xl border border-slate-200 text-sm md:col-span-1" />
            <button type="submit" [disabled]="busy() || !adjUser || !adjAmount || !adjReason.trim()"
              class="px-4 py-2 rounded-xl bg-brand-500 hover:bg-brand-600 text-on-brand text-sm font-semibold disabled:opacity-50">
              {{ 'action.add' | t }}
            </button>
          </form>
        </section>
      }

      <!-- Salaries -->
      <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5">
        <h2 class="font-semibold text-slate-800">{{ 'payroll.salaries' | t }}</h2>
        <p class="text-xs text-slate-500 mb-3">{{ 'payroll.salariesHint' | t }}</p>
        @for (p of staff(); track p.id) {
          <div class="flex flex-wrap items-center gap-2 mb-2">
            <span class="w-48 text-sm font-semibold text-slate-700">
              {{ p.username }}
              <span class="block text-xs font-normal text-slate-500">{{ ('role.' + p.role) | t }}@if (!p.isActive) { · {{ 'workers.inactive' | t }} }</span>
            </span>
            <input type="number" min="0" step="any" [(ngModel)]="salaryEdits[p.id]" [name]="'salary-' + p.id"
              [attr.aria-label]="'payroll.salary' | t" class="w-40 px-3 py-1.5 rounded-xl border border-slate-200 text-sm" />
            <button type="button" (click)="saveSalary(p)" [disabled]="busy() || (salaryEdits[p.id] ?? 0) === p.monthlySalary"
              class="px-3 py-1.5 rounded-xl bg-brand-500 text-on-brand text-xs font-semibold disabled:opacity-50">
              {{ 'action.save' | t }}
            </button>
          </div>
        }
      </section>
    </div>
  `,
})
export class PayrollComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);

  month = signal(currentMonth());
  sheet = signal<PayrollSheet | null>(null);
  staff = signal<PayrollStaff[]>([]);
  busy = signal(false);
  activeStaff = computed(() => this.staff().filter((p) => p.isActive));
  salaryEdits: Record<string, number> = {};

  adjUser = '';
  adjKind: 'bonus' | 'deduction' = 'bonus';
  adjAmount: number | null = null;
  adjReason = '';

  ngOnInit(): void {
    this.api.getPayrollStaff().subscribe({
      next: (s) => {
        this.staff.set(s);
        this.salaryEdits = Object.fromEntries(s.map((p) => [p.id, p.monthlySalary]));
      },
    });
    this.loadSheet();
  }

  setMonth(value: string): void {
    if (!/^\d{4}-\d{2}$/.test(value)) return;
    this.month.set(value);
    this.loadSheet();
  }

  private loadSheet(): void {
    this.sheet.set(null);
    this.api.getPayrollMonth(this.month()).subscribe({ next: (s) => this.sheet.set(s) });
  }

  saveSalary(p: PayrollStaff): void {
    const value = Number(this.salaryEdits[p.id] ?? 0);
    this.busy.set(true);
    this.api.setSalary(p.id, value).subscribe({
      next: (saved) => {
        this.staff.update((l) => l.map((x) => (x.id === saved.id ? saved : x)));
        this.busy.set(false);
        this.toast.success(this.lang.t('payroll.savedToast'));
        this.loadSheet();
      },
      error: () => this.busy.set(false),
    });
  }

  addAdjustment(): void {
    if (!this.adjUser || !this.adjAmount || !this.adjReason.trim()) return;
    this.busy.set(true);
    this.api
      .addPayAdjustment({
        adminUserId: this.adjUser, month: this.month(), kind: this.adjKind,
        amount: Number(this.adjAmount), reason: this.adjReason.trim(),
      })
      .subscribe({
        next: () => {
          this.adjAmount = null;
          this.adjReason = '';
          this.busy.set(false);
          this.toast.success(this.lang.t('payroll.addedToast'));
          this.loadSheet();
        },
        error: () => this.busy.set(false),
      });
  }

  removeAdjustment(id: string): void {
    this.api.deletePayAdjustment(id).subscribe({ next: () => this.loadSheet() });
  }

  close(): void {
    this.busy.set(true);
    this.api.closePayrollMonth(this.month()).subscribe({
      next: (s) => {
        this.sheet.set(s);
        this.busy.set(false);
        this.toast.success(this.lang.t('payroll.closedToast'));
      },
      error: () => this.busy.set(false),
    });
  }

  reopen(): void {
    this.busy.set(true);
    this.api.reopenPayrollMonth(this.month()).subscribe({
      next: (s) => {
        this.sheet.set(s);
        this.busy.set(false);
        this.toast.success(this.lang.t('payroll.reopenedToast'));
      },
      error: () => this.busy.set(false),
    });
  }
}
