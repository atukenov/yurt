import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { Forecast, Location } from 'shared-models';
import { LocalDatePipe, LocalNumberPipe } from 'shared-ui';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { barPercent, confidenceKey, dateInDays, hourLabel, openHours, plainAmount } from './forecast-helpers';

/**
 * Sales forecast and prep plan of a branch (own_pos): how many of each dish to expect on a day, hour by hour, and how
 * much of each ingredient those dishes will use next to what the branch has now. Learned from the same weekday of
 * past weeks. Item counts only, no money, so every staff role sees it.
 */
@Component({
  selector: 'app-forecast',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe, LocalDatePipe, LocalNumberPipe],
  styles: [`@media print { .no-print { display: none !important; } }`],
  template: `
    <div class="mx-auto max-w-5xl p-6">
      <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-xl font-bold text-slate-800">{{ 'fc.title' | t }}</h1>
          <p class="text-sm text-slate-500">{{ 'fc.hint' | t }}</p>
        </div>
        <button type="button" (click)="print()" class="no-print rounded-xl border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-600 hover:bg-slate-50">
          {{ 'fc.print' | t }}
        </button>
      </div>

      <div class="no-print mb-4 flex flex-wrap items-end gap-3 rounded-2xl border border-slate-200 bg-surface p-4">
        <label class="text-sm text-slate-600">
          <span class="mb-1 block">{{ 'stock.branch' | t }}</span>
          <select [(ngModel)]="locationId" (ngModelChange)="pickLocation()" class="rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-brand-400">
            @for (loc of locations(); track loc.id) {
              <option [value]="loc.id">{{ loc.name }}</option>
            }
          </select>
        </label>
        <div class="text-sm text-slate-600">
          <span class="mb-1 block">{{ 'fc.day' | t }}</span>
          <div class="flex items-center gap-1">
            <button type="button" (click)="setDay(0)" class="rounded-xl border px-3 py-2 text-sm font-semibold" [class]="date === today ? 'border-brand-500 bg-brand-50 text-slate-800' : 'border-slate-200 text-slate-600'">{{ 'fc.today' | t }}</button>
            <button type="button" (click)="setDay(1)" class="rounded-xl border px-3 py-2 text-sm font-semibold" [class]="date === tomorrow ? 'border-brand-500 bg-brand-50 text-slate-800' : 'border-slate-200 text-slate-600'">{{ 'fc.tomorrow' | t }}</button>
            <input type="date" [(ngModel)]="date" (ngModelChange)="load()" class="rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-brand-400" [attr.aria-label]="'fc.day' | t" />
          </div>
        </div>
        <label class="text-sm text-slate-600">
          <span class="mb-1 block">{{ 'fc.adjust' | t }}</span>
          <select [(ngModel)]="adjust" (ngModelChange)="load()" class="rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-brand-400">
            @for (a of adjustOptions; track a) {
              <option [ngValue]="a">{{ a > 0 ? '+' + a : a }}%</option>
            }
          </select>
        </label>
        <label class="text-sm text-slate-600">
          <span class="mb-1 block">{{ 'fc.buffer' | t }}</span>
          <select [(ngModel)]="buffer" (ngModelChange)="load()" class="rounded-xl border border-slate-200 px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-brand-400">
            @for (b of bufferOptions; track b) {
              <option [ngValue]="b">{{ b }}%</option>
            }
          </select>
        </label>
      </div>

      @if (error()) {
        <p class="rounded-2xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{{ error() }}</p>
      } @else if (loading()) {
        <p class="py-8 text-center text-slate-500">{{ 'label.loading' | t }}</p>
      } @else if (data(); as f) {
        <div class="mb-4 grid gap-3 sm:grid-cols-3">
          <div class="rounded-2xl border border-slate-200 bg-surface p-4">
            <div class="text-sm text-slate-500">{{ ('fc.wd.' + f.weekday) | t }}, {{ f.date + 'T12:00:00' | ldate: 'longDate' }}</div>
            <div class="mt-1 text-3xl font-bold text-slate-800">{{ f.totalItems | lnumber }}</div>
            <div class="text-sm text-slate-500">{{ 'fc.totalItems' | t }}</div>
          </div>
          <div class="rounded-2xl border border-slate-200 bg-surface p-4">
            <div class="text-sm text-slate-500">{{ 'fc.peak' | t }}</div>
            <div class="mt-1 text-3xl font-bold text-slate-800">{{ f.peakHour == null ? '—' : hour(f.peakHour) }}</div>
          </div>
          <div class="rounded-2xl border border-slate-200 bg-surface p-4">
            <div class="text-sm text-slate-500">{{ 'fc.trust' | t }}</div>
            <div class="mt-1 text-xl font-bold text-slate-800">{{ confidence(f.confidence) | t }}</div>
            <div class="text-sm text-slate-500">{{ 'fc.daysUsed' | t: { count: f.daysUsed } }}</div>
          </div>
        </div>

        @if (f.daysUsed === 0) {
          <p class="rounded-2xl border border-slate-200 bg-surface p-8 text-center text-slate-500">{{ 'fc.empty' | t }}</p>
        } @else {
          @if (fewDays(f)) {
            <p class="mb-4 rounded-2xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">{{ 'fc.fewDays' | t }}</p>
          }

          <section class="mb-4 rounded-2xl border border-slate-200 bg-surface p-4">
            <h2 class="mb-3 font-semibold text-slate-800">{{ 'fc.byHour' | t }}</h2>
            <div class="flex h-32 items-end gap-1" role="img" [attr.aria-label]="'fc.byHour' | t">
              @for (h of hours(); track h) {
                <div class="flex h-full min-w-0 flex-1 flex-col justify-end text-center">
                  <span class="text-[10px] text-slate-500">{{ f.hourly[h] }}</span>
                  <div class="rounded-t bg-brand-500" [style.height.%]="bar(f.hourly[h])"></div>
                </div>
              }
            </div>
            <div class="mt-1 flex gap-1">
              @for (h of hours(); track h) {
                <span class="min-w-0 flex-1 text-center text-[10px] text-slate-500">{{ h }}</span>
              }
            </div>
          </section>

          <section class="mb-4 overflow-x-auto rounded-2xl border border-slate-200 bg-surface">
            <h2 class="px-4 pt-4 font-semibold text-slate-800">{{ 'fc.dishes' | t }}</h2>
            <table class="w-full text-sm">
              <thead>
                <tr class="text-left text-slate-500">
                  <th class="px-4 py-2">{{ 'fc.dish' | t }}</th>
                  <th class="px-2 py-2 text-right">{{ 'fc.morning' | t }}</th>
                  <th class="px-2 py-2 text-right">{{ 'fc.lunch' | t }}</th>
                  <th class="px-2 py-2 text-right">{{ 'fc.afternoon' | t }}</th>
                  <th class="px-2 py-2 text-right">{{ 'fc.evening' | t }}</th>
                  <th class="px-4 py-2 text-right">{{ 'fc.prepare' | t }}</th>
                </tr>
              </thead>
              <tbody>
                @for (d of f.dishes; track d.menuItemId + (d.variantId ?? '')) {
                  <tr class="border-t border-slate-100">
                    <td class="px-4 py-2 text-slate-800">{{ d.name }}@if (d.size) { <span class="text-slate-500"> · {{ d.size }}</span> }</td>
                    <td class="px-2 py-2 text-right text-slate-600">{{ d.morning }}</td>
                    <td class="px-2 py-2 text-right text-slate-600">{{ d.lunch }}</td>
                    <td class="px-2 py-2 text-right text-slate-600">{{ d.afternoon }}</td>
                    <td class="px-2 py-2 text-right text-slate-600">{{ d.evening }}</td>
                    <td class="px-4 py-2 text-right font-semibold text-slate-800">{{ d.expected }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </section>

          <section class="mb-4 overflow-x-auto rounded-2xl border border-slate-200 bg-surface">
            <h2 class="px-4 pt-4 font-semibold text-slate-800">{{ 'fc.ingredients' | t }}</h2>
            <p class="px-4 text-sm text-slate-500">{{ 'fc.ingredientsHint' | t }}</p>
            @if (f.ingredients.length === 0) {
              <p class="p-4 text-sm text-slate-500">{{ 'fc.noRecipes' | t }}</p>
            } @else {
              <table class="w-full text-sm">
                <thead>
                  <tr class="text-left text-slate-500">
                    <th class="px-4 py-2">{{ 'fc.ingredient' | t }}</th>
                    <th class="px-2 py-2 text-right">{{ 'fc.needed' | t }}</th>
                    <th class="px-2 py-2 text-right">{{ 'fc.inStock' | t }}</th>
                    <th class="px-4 py-2 text-right">{{ 'fc.missing' | t }}</th>
                  </tr>
                </thead>
                <tbody>
                  @for (i of f.ingredients; track i.ingredientId) {
                    <tr class="border-t border-slate-100" [class.bg-amber-50]="i.missing > 0">
                      <td class="px-4 py-2 text-slate-800">{{ i.name }}</td>
                      <td class="px-2 py-2 text-right text-slate-600">{{ amount(i.needed) }} {{ i.unit }}</td>
                      <td class="px-2 py-2 text-right text-slate-600">{{ i.inStock == null ? '—' : amount(i.inStock) + ' ' + i.unit }}</td>
                      <td class="px-4 py-2 text-right font-semibold" [class]="i.missing > 0 ? 'text-amber-700' : 'text-slate-400'">
                        {{ i.missing > 0 ? amount(i.missing) + ' ' + i.unit : '—' }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </section>

          <p class="text-xs text-slate-500">
            {{ 'fc.basedOn' | t }}
            @for (h of f.history; track h.date) {
              <span class="mr-2 whitespace-nowrap">{{ h.date + 'T12:00:00' | ldate: 'shortDate' }} ({{ h.items }})</span>
            }
          </p>
        }
      }
    </div>
  `,
})
export class ForecastComponent implements OnInit {
  private api = inject(YurtApiService);
  private lang = inject(AdminLangService);

  private readonly LOC_KEY = 'yurt_stock_location';
  readonly adjustOptions = [-30, -20, -10, 0, 10, 20, 30, 50];
  readonly bufferOptions = [0, 5, 10, 15, 20, 30];

  readonly today = dateInDays(0);
  readonly tomorrow = dateInDays(1);

  locations = signal<Location[]>([]);
  data = signal<Forecast | null>(null);
  loading = signal(false);
  error = signal('');
  locationId = '';
  date = this.today;
  adjust = 0;
  buffer = 10;

  readonly hour = hourLabel;
  readonly amount = plainAmount;
  readonly confidence = confidenceKey;

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe((locs) => {
      this.locations.set(locs);
      const saved = localStorage.getItem(this.LOC_KEY) ?? '';
      this.locationId = locs.some((l) => l.id === saved) ? saved : (locs[0]?.id ?? '');
      this.load();
    });
  }

  pickLocation(): void {
    localStorage.setItem(this.LOC_KEY, this.locationId);
    this.load();
  }

  setDay(offset: number): void {
    this.date = dateInDays(offset);
    this.load();
  }

  load(): void {
    if (!this.locationId || !this.date) return;
    this.loading.set(true);
    this.error.set('');
    this.api.getForecast(this.locationId, this.date, this.adjust, this.buffer).subscribe({
      next: (f) => {
        this.data.set(f);
        this.loading.set(false);
      },
      error: (e) => {
        this.data.set(null);
        this.error.set(this.lang.t('fc.loadFailed'));
        this.loading.set(false);
      },
    });
  }

  fewDays(f: Forecast): boolean {
    return f.daysUsed > 0 && f.daysUsed < 3;
  }

  hours(): number[] {
    return openHours(this.data()?.hourly ?? []);
  }

  bar(value: number): number {
    return barPercent(value, Math.max(0, ...(this.data()?.hourly ?? [])));
  }

  print(): void {
    window.print();
  }
}
