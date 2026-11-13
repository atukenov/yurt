import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { Currency2Pipe, LocalDatePipe } from 'shared-ui';
import { Location, TipReport } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';

/** yyyy-MM-dd of a date, in the browser's own day. */
export function dayText(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** First day of the month the date is in. */
export function monthStart(d: Date): string {
  return dayText(new Date(d.getFullYear(), d.getMonth(), 1));
}

/**
 * Tips (CEO only, module own_pos): how much each staff member got and how much each cashier shift took in tips, for a
 * period. Tips are money on top of the bill: they are not part of sales, profit or the tax receipt.
 */
@Component({
  selector: 'app-tips',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe, Currency2Pipe, LocalDatePipe],
  template: `
    <div class="p-4 md:p-6 max-w-5xl mx-auto space-y-5">
      <div>
        <h1 class="text-2xl font-bold text-slate-800">{{ 'tips.title' | t }}</h1>
        <p class="text-slate-500 text-sm mt-0.5">{{ 'tips.hint' | t }}</p>
      </div>

      <div class="flex flex-wrap items-center gap-3">
        <label class="text-sm font-semibold text-slate-700" for="tips-from">{{ 'tips.from' | t }}</label>
        <input id="tips-from" type="date" [ngModel]="from()" (ngModelChange)="setFrom($event)"
          class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm" />
        <label class="text-sm font-semibold text-slate-700" for="tips-to">{{ 'tips.to' | t }}</label>
        <input id="tips-to" type="date" [ngModel]="to()" (ngModelChange)="setTo($event)"
          class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm" />
        @if (locations().length > 1) {
          <select [ngModel]="locationId()" (ngModelChange)="setLocation($event)" [attr.aria-label]="'tips.branch' | t"
            class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm">
            <option value="">{{ 'tips.allBranches' | t }}</option>
            @for (l of locations(); track l.id) {
              <option [value]="l.id">{{ l.name }}</option>
            }
          </select>
        }
      </div>

      @if (!report()) {
        <div class="h-40 bg-slate-100 rounded-2xl animate-pulse"></div>
      } @else {
        @let r = report()!;
        <div class="grid gap-3 sm:grid-cols-3">
          <div class="rounded-xl bg-slate-50 p-3">
            <p class="text-xs text-slate-500">{{ 'tips.total' | t }}</p>
            <p class="text-xl font-bold text-slate-800">{{ r.total | currency2 }}</p>
          </div>
          <div class="rounded-xl bg-slate-50 p-3">
            <p class="text-xs text-slate-500">{{ 'tips.cash' | t }}</p>
            <p class="text-xl font-bold text-slate-800">{{ r.cash | currency2 }}</p>
          </div>
          <div class="rounded-xl bg-slate-50 p-3">
            <p class="text-xs text-slate-500">{{ 'tips.nonCash' | t }}</p>
            <p class="text-xl font-bold text-slate-800">{{ r.nonCash | currency2 }}</p>
          </div>
        </div>

        @if (r.orders === 0) {
          <p class="text-sm text-slate-500">{{ 'tips.none' | t }}</p>
        } @else {
          <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5">
            <h2 class="text-sm font-semibold text-slate-700 mb-2">{{ 'tips.byStaff' | t }}</h2>
            <div class="overflow-x-auto">
              <table class="w-full text-sm">
                <thead>
                  <tr class="border-b border-slate-100 text-slate-500 text-xs uppercase tracking-wide">
                    <th class="text-left px-3 py-2">{{ 'tips.staff' | t }}</th>
                    <th class="text-right px-3 py-2">{{ 'tips.orders' | t }}</th>
                    <th class="text-right px-3 py-2">{{ 'tips.cash' | t }}</th>
                    <th class="text-right px-3 py-2">{{ 'tips.nonCash' | t }}</th>
                    <th class="text-right px-3 py-2">{{ 'tips.total' | t }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (s of r.byStaff; track s.staffId ?? 'none') {
                    <tr class="border-b border-slate-50">
                      <td class="px-3 py-2">{{ s.name || ('tips.unassigned' | t) }}</td>
                      <td class="px-3 py-2 text-right text-slate-500">{{ s.orders }}</td>
                      <td class="px-3 py-2 text-right">{{ s.cash | currency2 }}</td>
                      <td class="px-3 py-2 text-right">{{ s.nonCash | currency2 }}</td>
                      <td class="px-3 py-2 text-right font-semibold">{{ s.total | currency2 }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </section>

          <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5">
            <h2 class="text-sm font-semibold text-slate-700 mb-2">{{ 'tips.byShift' | t }}</h2>
            <div class="overflow-x-auto">
              <table class="w-full text-sm">
                <tbody>
                  @for (s of r.byShift; track s.shiftId) {
                    <tr class="border-b border-slate-50">
                      <td class="px-3 py-2">{{ s.openedAt | ldate: 'dd.MM HH:mm' }}@if (s.closedAt) { – {{ s.closedAt | ldate: 'HH:mm' }} } @else { · {{ 'tips.shiftOpen' | t }} }</td>
                      <td class="px-3 py-2 text-slate-500">{{ s.locationName }}</td>
                      <td class="px-3 py-2 text-right font-semibold">{{ s.total | currency2 }}</td>
                    </tr>
                  } @empty {
                    <tr><td class="px-3 py-2 text-slate-500">{{ 'tips.noShifts' | t }}</td></tr>
                  }
                </tbody>
              </table>
            </div>
          </section>
        }
        <p class="text-xs text-slate-500">{{ 'tips.note' | t }}</p>
      }
    </div>
  `,
})
export class TipsComponent implements OnInit {
  private readonly api = inject(YurtApiService);

  readonly from = signal(monthStart(new Date()));
  readonly to = signal(dayText(new Date()));
  readonly locationId = signal('');
  readonly locations = signal<Location[]>([]);
  readonly report = signal<TipReport | null>(null);

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe({ next: (l) => this.locations.set(l), error: () => undefined });
    this.load();
  }

  setFrom(v: string): void { this.from.set(v); this.load(); }
  setTo(v: string): void { this.to.set(v); this.load(); }
  setLocation(v: string): void { this.locationId.set(v); this.load(); }

  private load(): void {
    if (!this.from() || !this.to() || this.to() < this.from()) return;
    this.report.set(null);
    this.api.getTipReport(this.from(), this.to(), this.locationId() || null).subscribe({
      next: (r) => this.report.set(r),
      error: () => undefined,
    });
  }
}
