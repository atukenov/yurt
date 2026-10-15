import { Component } from '@angular/core';

@Component({
  selector: 'yurt-skeleton',
  standalone: true,
  template: `
    <div class="animate-pulse space-y-3">
      <div class="h-4 bg-stone-200 rounded-full w-3/4"></div>
      <div class="h-4 bg-stone-200 rounded-full"></div>
      <div class="h-4 bg-stone-200 rounded-full w-5/6"></div>
    </div>
  `,
})
export class SkeletonComponent {}

@Component({
  selector: 'yurt-skeleton-card',
  standalone: true,
  template: `
    <div class="bg-white rounded-2xl border border-stone-100 p-4 animate-pulse">
      <div class="h-32 bg-stone-200 rounded-xl mb-3"></div>
      <div class="h-4 bg-stone-200 rounded w-2/3 mb-2"></div>
      <div class="h-3 bg-stone-200 rounded w-full mb-2"></div>
      <div class="h-5 bg-amber-100 rounded w-1/4"></div>
    </div>
  `,
})
export class SkeletonCardComponent {}

/**
 * Matches the full-width order card layout:
 * [order # title]  [status badge]
 * [date line]
 * [location line]
 * [items summary]
 * ─────────────────────
 * [Total]    [amount]
 */
@Component({
  selector: 'yurt-skeleton-order-card',
  standalone: true,
  template: `
    <div class="bg-white rounded-2xl border border-stone-100 p-4 animate-pulse">
      <div class="flex items-start justify-between mb-2">
        <div class="flex-1 min-w-0">
          <div class="h-4 bg-stone-200 rounded w-28 mb-1.5"></div>
          <div class="h-3 bg-stone-200 rounded w-20 mb-1"></div>
          <div class="h-3 bg-stone-200 rounded w-32"></div>
        </div>
        <div class="h-6 bg-stone-200 rounded-full w-20 ml-3 shrink-0"></div>
      </div>
      <div class="h-3 bg-stone-200 rounded w-3/4 mb-3"></div>
      <div class="flex justify-between items-center pt-2 border-t border-stone-100">
        <div class="h-3 bg-stone-200 rounded w-10"></div>
        <div class="h-5 bg-amber-100 rounded w-16"></div>
      </div>
    </div>
  `,
})
export class SkeletonOrderCardComponent {}

/**
 * Mirrors the home/news screen structure:
 * – amber stats banner (3 cols)
 * – achievements horizontal scroll
 * – promo card
 * – new items horizontal scroll
 * – recommended items horizontal scroll
 */
@Component({
  selector: 'yurt-skeleton-news',
  standalone: true,
  template: `
    <div class="flex flex-col gap-0 pb-6 animate-pulse">

      <!-- Stats banner -->
      <div class="mx-4 mt-4 rounded-3xl bg-amber-100 h-28"></div>

      <!-- Achievements row -->
      <div class="mt-5 px-4">
        <div class="flex items-center justify-between mb-3">
          <div class="h-4 bg-stone-200 rounded w-36"></div>
          <div class="h-3 bg-stone-200 rounded w-12"></div>
        </div>
        <div class="flex gap-3 overflow-hidden">
          <div class="shrink-0 w-20 h-24 bg-stone-100 rounded-2xl"></div>
          <div class="shrink-0 w-20 h-24 bg-stone-100 rounded-2xl"></div>
          <div class="shrink-0 w-20 h-24 bg-stone-100 rounded-2xl"></div>
          <div class="shrink-0 w-20 h-24 bg-stone-100 rounded-2xl"></div>
          <div class="shrink-0 w-20 h-24 bg-stone-100 rounded-2xl"></div>
        </div>
      </div>

      <!-- Promo card -->
      <div class="mt-5 px-4">
        <div class="h-4 bg-stone-200 rounded w-32 mb-3"></div>
        <div class="h-36 bg-stone-200 rounded-2xl"></div>
      </div>

      <!-- New items horizontal scroll -->
      <div class="mt-5">
        <div class="h-4 bg-stone-200 rounded w-36 mb-3 mx-4"></div>
        <div class="flex gap-3 pl-4 overflow-hidden">
          <div class="shrink-0 w-36 rounded-2xl overflow-hidden bg-white border border-stone-100">
            <div class="h-24 bg-stone-200"></div>
            <div class="p-2.5 space-y-1.5">
              <div class="h-3 bg-stone-200 rounded"></div>
              <div class="h-4 bg-amber-100 rounded w-16"></div>
              <div class="h-7 bg-stone-100 rounded-xl"></div>
            </div>
          </div>
          <div class="shrink-0 w-36 rounded-2xl overflow-hidden bg-white border border-stone-100">
            <div class="h-24 bg-stone-200"></div>
            <div class="p-2.5 space-y-1.5">
              <div class="h-3 bg-stone-200 rounded"></div>
              <div class="h-4 bg-amber-100 rounded w-16"></div>
              <div class="h-7 bg-stone-100 rounded-xl"></div>
            </div>
          </div>
          <div class="shrink-0 w-36 rounded-2xl overflow-hidden bg-white border border-stone-100">
            <div class="h-24 bg-stone-200"></div>
            <div class="p-2.5 space-y-1.5">
              <div class="h-3 bg-stone-200 rounded"></div>
              <div class="h-4 bg-amber-100 rounded w-16"></div>
              <div class="h-7 bg-stone-100 rounded-xl"></div>
            </div>
          </div>
        </div>
      </div>

      <!-- Recommended items horizontal scroll -->
      <div class="mt-5">
        <div class="h-4 bg-stone-200 rounded w-44 mb-3 mx-4"></div>
        <div class="flex gap-3 pl-4 overflow-hidden">
          <div class="shrink-0 w-36 rounded-2xl overflow-hidden bg-white border border-stone-100">
            <div class="h-24 bg-stone-200"></div>
            <div class="p-2.5 space-y-1.5">
              <div class="h-3 bg-stone-200 rounded"></div>
              <div class="h-4 bg-amber-100 rounded w-16"></div>
              <div class="h-7 bg-stone-100 rounded-xl"></div>
            </div>
          </div>
          <div class="shrink-0 w-36 rounded-2xl overflow-hidden bg-white border border-stone-100">
            <div class="h-24 bg-stone-200"></div>
            <div class="p-2.5 space-y-1.5">
              <div class="h-3 bg-stone-200 rounded"></div>
              <div class="h-4 bg-amber-100 rounded w-16"></div>
              <div class="h-7 bg-stone-100 rounded-xl"></div>
            </div>
          </div>
          <div class="shrink-0 w-36 rounded-2xl overflow-hidden bg-white border border-stone-100">
            <div class="h-24 bg-stone-200"></div>
            <div class="p-2.5 space-y-1.5">
              <div class="h-3 bg-stone-200 rounded"></div>
              <div class="h-4 bg-amber-100 rounded w-16"></div>
              <div class="h-7 bg-stone-100 rounded-xl"></div>
            </div>
          </div>
        </div>
      </div>

    </div>
  `,
})
export class SkeletonNewsComponent {}
