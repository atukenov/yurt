import { Component, computed, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { BrandService, YurtApiService } from 'shared-api';
import { Promotion } from 'shared-models';
import { Currency2Pipe } from 'shared-ui';
import { kgText } from '../../core/cart';
import { GuestDisplayService } from '../../core/guest-display.service';
import { payPromptKey } from '../../core/guest-display';
import { PosLangService } from '../../core/lang.service';
import { PosTranslatePipe } from '../../core/translate.pipe';

const PROMO_KEY = 'yurt_pos_promos';

/**
 * The guest-facing screen (open `/guest` in a second window on the customer monitor). Shows the order being rung up,
 * the totals, the tip and what to do to pay, a thank-you, and the cafe's promotions when nothing is being rung up.
 * It needs no sign-in: it only draws what the till on the same computer sends it, so it works offline too.
 */
@Component({
  selector: 'pos-guest',
  imports: [Currency2Pipe, PosTranslatePipe],
  template: `
    <main class="flex min-h-screen flex-col bg-surface text-slate-900" [attr.lang]="lang.lang()">
      <header class="flex items-center gap-4 bg-brand-500 px-8 py-5 text-on-brand">
        @if (brand.logoUrl(); as logo) { <img [src]="logo" alt="" class="h-12 w-auto rounded" /> }
        <h1 class="text-3xl font-bold">{{ brand.name() }}</h1>
      </header>

      @switch (view().phase) {
        @case ('idle') {
          <section class="flex flex-1 flex-col items-center justify-center gap-6 p-10 text-center">
            @if (promo(); as p) {
              @if (p.imageUrl) { <img [src]="p.imageUrl" alt="" class="max-h-[45vh] rounded-2xl object-cover shadow" /> }
              <p class="text-lg font-semibold uppercase tracking-wide text-slate-500">{{ 'guest.promos' | t }}</p>
              <h2 class="text-5xl font-bold">{{ p.title }}</h2>
              @if (p.description) { <p class="max-w-3xl text-2xl text-slate-600">{{ p.description }}</p> }
            } @else {
              <h2 class="text-6xl font-bold">{{ 'guest.welcome' | t }}</h2>
            }
          </section>
        }
        @case ('done') {
          <section class="flex flex-1 flex-col items-center justify-center gap-4 p-10 text-center">
            <p class="text-8xl" aria-hidden="true">✓</p>
            <h2 class="text-6xl font-bold">{{ 'guest.thanks' | t }}</h2>
            @if (view().saleNo; as no) {
              <p class="text-2xl text-slate-600">{{ 'guest.number' | t }}</p>
              <p class="text-7xl font-bold tracking-widest">{{ no }}</p>
            }
            <p class="text-4xl font-semibold">{{ view().paid | currency2 }}</p>
          </section>
        }
        @default {
          <section class="flex flex-1 flex-col gap-4 p-8">
            <div class="flex items-baseline justify-between">
              <h2 class="text-3xl font-bold">{{ 'guest.yourOrder' | t }}</h2>
              <span class="text-xl text-slate-600">{{ (view().orderType === 'DineIn' ? 'cart.here' : 'cart.toGo') | t }}</span>
            </div>
            <ul class="flex-1 space-y-3 overflow-y-auto">
              @for (l of view().lines; track $index) {
                <li class="flex items-start justify-between gap-4 rounded-xl bg-slate-100 px-5 py-3 text-2xl">
                  <div>
                    <p class="font-semibold">{{ l.quantity > 1 ? l.quantity + ' × ' : '' }}{{ l.name }}@if (l.weightGrams) { · {{ kg(l.weightGrams) }} }</p>
                    @if (l.extras) { <p class="text-lg text-slate-600">{{ l.extras }}</p> }
                  </div>
                  <span class="font-semibold">{{ l.total | currency2 }}</span>
                </li>
              }
            </ul>
            <dl class="space-y-1 border-t border-slate-300 pt-4 text-2xl">
              @if (view().discount > 0) {
                <div class="flex justify-between text-slate-600"><dt>{{ 'guest.total' | t }}</dt><dd>{{ view().total | currency2 }}</dd></div>
                <div class="flex justify-between text-slate-600"><dt>{{ 'guest.discount' | t }}</dt><dd>−{{ view().discount | currency2 }}</dd></div>
              }
              @if (view().tip > 0) {
                <div class="flex justify-between text-slate-600"><dt>{{ 'guest.tip' | t }}</dt><dd>+{{ view().tip | currency2 }}</dd></div>
              }
              <div class="flex justify-between text-5xl font-bold">
                <dt>{{ (view().phase === 'pay' ? 'guest.toPay' : 'guest.total') | t }}</dt>
                <dd>{{ toPay() | currency2 }}</dd>
              </div>
            </dl>
            @if (view().phase === 'pay') {
              <div class="rounded-2xl bg-brand-500 px-6 py-5 text-center text-3xl font-bold text-on-brand" role="status">
                {{ payKey() | t }}
                @if (view().change !== null && view().method === 'Cash') {
                  <p class="mt-2 text-2xl">{{ 'guest.cashGiven' | t }}: {{ view().cashGiven! | currency2 }} · {{ 'guest.change' | t }}: {{ view().change! | currency2 }}</p>
                }
              </div>
            }
          </section>
        }
      }
    </main>
  `,
})
export class GuestComponent implements OnInit, OnDestroy {
  private display = inject(GuestDisplayService);
  private api = inject(YurtApiService);
  readonly brand = inject(BrandService);
  readonly lang = inject(PosLangService);

  readonly view = this.display.view;
  readonly toPay = computed(() => this.view().total - this.view().discount + this.view().tip);
  readonly payKey = computed(() => payPromptKey(this.view().method));

  private promos = signal<Promotion[]>(this.savedPromos());
  private index = signal(0);
  readonly promo = computed(() => { const l = this.promos(); return l.length ? l[this.index() % l.length] : null; });

  private stop: (() => void) | null = null;
  private timer: ReturnType<typeof setInterval> | null = null;

  constructor() {
    // The guest sees the till's language.
    effect(() => { const l = this.view().lang; if (l !== this.lang.lang()) this.lang.setLang(l); });
  }

  ngOnInit(): void {
    this.stop = this.display.listen(this.lang.lang());
    this.timer = setInterval(() => this.index.update((i) => i + 1), 8000);
    // Promotions come from the server when there is internet; otherwise the ones saved last time are shown.
    this.api.getActivePromotions().subscribe({
      next: (list) => {
        this.promos.set(list);
        try { localStorage.setItem(PROMO_KEY, JSON.stringify(list)); } catch { /* not saved */ }
      },
      error: () => undefined,
    });
  }

  ngOnDestroy(): void {
    this.stop?.();
    if (this.timer) clearInterval(this.timer);
  }

  kg(grams: number): string { return kgText(grams); }

  private savedPromos(): Promotion[] {
    try { return JSON.parse(localStorage.getItem(PROMO_KEY) ?? '[]') as Promotion[]; } catch { return []; }
  }
}
