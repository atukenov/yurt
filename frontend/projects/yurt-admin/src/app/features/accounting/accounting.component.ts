import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { Location } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';

export type AccountingPreset = 'lastMonth' | 'thisMonth' | 'lastQuarter' | 'lastYear';

function day(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** The first and last day of a ready-made period, as yyyy-MM-dd. Quarters and years are calendar ones. */
export function presetRange(preset: AccountingPreset, now = new Date()): { from: string; to: string } {
  const y = now.getFullYear();
  const m = now.getMonth();
  switch (preset) {
    case 'thisMonth':
      return { from: day(new Date(y, m, 1)), to: day(now) };
    case 'lastMonth':
      return { from: day(new Date(y, m - 1, 1)), to: day(new Date(y, m, 0)) };
    case 'lastQuarter': {
      const q = Math.floor(m / 3);
      return { from: day(new Date(y, (q - 1) * 3, 1)), to: day(new Date(y, q * 3, 0)) };
    }
    case 'lastYear':
      return { from: day(new Date(y - 1, 0, 1)), to: day(new Date(y - 1, 11, 31)) };
  }
}

/** Days from `from` to `to`, both included; 0 when the range is wrong. */
export function daysBetween(from: string, to: string): number {
  const a = Date.parse(from + 'T00:00:00Z');
  const b = Date.parse(to + 'T00:00:00Z');
  if (isNaN(a) || isNaN(b) || b < a) return 0;
  return Math.round((b - a) / 86_400_000) + 1;
}

/** The server builds at most this many days in one file. */
export const MAX_DAYS = 366;

/**
 * Export for accounting (CEO only): pick the days and a branch, then download one Excel file for the accountant or the
 * same period as a 1C exchange file.
 */
@Component({
  selector: 'app-accounting',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe],
  template: `
    <div class="p-4 md:p-6 max-w-3xl mx-auto space-y-5">
      <div>
        <h1 class="text-2xl font-bold text-slate-800">{{ 'acc.title' | t }}</h1>
        <p class="text-slate-500 text-sm mt-0.5">{{ 'acc.hint' | t }}</p>
      </div>

      <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5 space-y-4">
        <div class="flex flex-wrap gap-2">
          @for (p of presets; track p) {
            <button type="button" (click)="usePreset(p)"
              class="px-3 py-1.5 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-700 text-sm font-semibold">
              {{ 'acc.preset.' + p | t }}
            </button>
          }
        </div>

        <div class="flex flex-wrap items-end gap-3">
          <label class="flex flex-col gap-1 text-sm font-semibold text-slate-700">
            {{ 'acc.from' | t }}
            <input type="date" [(ngModel)]="from" class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm font-normal" />
          </label>
          <label class="flex flex-col gap-1 text-sm font-semibold text-slate-700">
            {{ 'acc.to' | t }}
            <input type="date" [(ngModel)]="to" class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm font-normal" />
          </label>
          @if (locations().length > 1) {
            <label class="flex flex-col gap-1 text-sm font-semibold text-slate-700">
              {{ 'profit.branch' | t }}
              <select [(ngModel)]="locationId" class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm font-normal">
                <option value="">{{ 'profit.allBranches' | t }}</option>
                @for (l of locations(); track l.id) {
                  <option [value]="l.id">{{ l.name }}</option>
                }
              </select>
            </label>
          }
        </div>

        @if (rangeError(); as e) {
          <p class="text-sm text-tone-red" role="alert">{{ e | t: { count: maxDays } }}</p>
        }

        <div class="grid gap-3 sm:grid-cols-2">
          <button type="button" (click)="download('excel')" [disabled]="!!busy() || !!rangeError()"
            class="text-left rounded-xl border border-slate-200 hover:border-brand-500 p-4 disabled:opacity-50">
            <span class="block font-semibold text-slate-800">{{ busy() === 'excel' ? ('acc.preparing' | t) : ('acc.excel' | t) }}</span>
            <span class="block text-xs text-slate-500 mt-1">{{ 'acc.excelHint' | t }}</span>
          </button>
          <button type="button" (click)="download('1c')" [disabled]="!!busy() || !!rangeError()"
            class="text-left rounded-xl border border-slate-200 hover:border-brand-500 p-4 disabled:opacity-50">
            <span class="block font-semibold text-slate-800">{{ busy() === '1c' ? ('acc.preparing' | t) : ('acc.oneC' | t) }}</span>
            <span class="block text-xs text-slate-500 mt-1">{{ 'acc.oneCHint' | t }}</span>
          </button>
        </div>
      </section>

      <section class="text-sm text-slate-600 space-y-1">
        <p class="font-semibold text-slate-700">{{ 'acc.whatTitle' | t }}</p>
        <ul class="list-disc pl-5 space-y-0.5">
          @for (k of sheets; track k) {
            <li>{{ 'acc.sheet.' + k | t }}</li>
          }
        </ul>
        <p class="text-xs text-slate-500 pt-1">{{ 'acc.note' | t }}</p>
      </section>
    </div>
  `,
})
export class AccountingComponent implements OnInit {
  private api = inject(YurtApiService);
  private lang = inject(AdminLangService);

  readonly presets: AccountingPreset[] = ['lastMonth', 'thisMonth', 'lastQuarter', 'lastYear'];
  readonly sheets = ['sales', 'orders', 'items', 'shifts', 'deliveries', 'writeoffs'];
  readonly maxDays = MAX_DAYS;

  from = presetRange('lastMonth').from;
  to = presetRange('lastMonth').to;
  locationId = '';
  locations = signal<Location[]>([]);
  busy = signal<'' | 'excel' | '1c'>('');

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe({ next: (l) => this.locations.set(l) });
  }

  usePreset(p: AccountingPreset): void {
    ({ from: this.from, to: this.to } = presetRange(p));
  }

  /** Translation key of what is wrong with the chosen days, or null. */
  rangeError(): string | null {
    const days = daysBetween(this.from, this.to);
    if (!days) return 'acc.badRange';
    return days > MAX_DAYS ? 'acc.tooLong' : null;
  }

  download(kind: 'excel' | '1c'): void {
    if (this.rangeError()) return;
    this.busy.set(kind);
    this.api.exportAccounting(kind, this.from, this.to, this.locationId || null, this.lang.lang()).subscribe({
      next: (blob) => {
        this.busy.set('');
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = kind === 'excel' ? `accounting-${this.from}-${this.to}.xlsx` : `1c-${this.from}-${this.to}.xml`;
        a.click();
        setTimeout(() => URL.revokeObjectURL(a.href), 1000);
      },
      error: () => this.busy.set(''),
    });
  }
}
