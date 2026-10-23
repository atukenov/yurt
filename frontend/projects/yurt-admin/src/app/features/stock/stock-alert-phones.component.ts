import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { StockAlertPhone } from 'shared-models';
import { ToastService } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';

/**
 * Phones that get a push when an order takes stock down to its warn-at number (own_pos). The push goes to the cafe
 * app signed in with that number, so a manager only needs the app on their phone.
 */
@Component({
  selector: 'app-stock-alert-phones',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe],
  template: `
    <details class="mb-4 rounded-xl border border-slate-200 bg-surface p-3 text-sm">
      <summary class="cursor-pointer font-semibold text-slate-700">
        {{ 'stock.phones.title' | t }}
        @if (phones().length) {
          <span class="font-normal text-slate-500">· {{ phones().length }}</span>
        }
      </summary>
      <p class="mt-2 text-slate-500">{{ 'stock.phones.hint' | t }}</p>
      <div class="mt-2 flex flex-wrap items-center gap-2">
        <input
          type="text"
          inputmode="tel"
          [(ngModel)]="input"
          [placeholder]="'+77001234567, +77007654321'"
          [attr.aria-label]="'stock.phones.title' | t"
          class="min-w-0 flex-1 rounded-lg border border-slate-200 px-3 py-1.5 outline-none focus:ring-2 focus:ring-brand-400"
        />
        <button
          type="button"
          (click)="save()"
          [disabled]="saving()"
          class="rounded-lg bg-brand-500 px-3 py-1.5 font-semibold text-on-brand hover:bg-brand-600 disabled:opacity-40"
        >
          {{ 'stock.save' | t }}
        </button>
      </div>
      @if (phones().length) {
        <ul class="mt-2 flex flex-wrap gap-2">
          @for (p of phones(); track p.phone) {
            <li
              class="rounded-full px-2 py-0.5 text-xs font-semibold"
              [class]="p.hasApp ? 'bg-green-100 text-green-800' : 'bg-slate-100 text-slate-600'"
            >
              {{ p.phone }} · {{ (p.hasApp ? 'stock.phones.ready' : 'stock.phones.noApp') | t }}
            </li>
          }
        </ul>
      }
    </details>
  `,
})
export class StockAlertPhonesComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);

  phones = signal<StockAlertPhone[]>([]);
  saving = signal(false);
  input = '';

  ngOnInit(): void {
    this.api.getStockAlertPhones().subscribe((list) => this.show(list));
  }

  save(): void {
    const phones = this.input
      .split(/[,;\n]/)
      .map((p) => p.trim())
      .filter((p) => p.length > 0);
    this.saving.set(true);
    this.api.setStockAlertPhones(phones).subscribe({
      next: (list) => {
        this.show(list);
        this.saving.set(false);
        this.toast.success(this.lang.t('common.saved'));
      },
      error: (err) => {
        this.saving.set(false);
        this.toast.error(
          err?.status === 403
            ? 'Only an admin can change this'
            : (err?.error?.title ?? this.lang.t('common.saveFailed')),
        );
      },
    });
  }

  private show(list: StockAlertPhone[]): void {
    this.phones.set(list);
    this.input = list.map((p) => p.phone).join(', ');
  }
}
