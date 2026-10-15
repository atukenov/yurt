import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { YurtApiService } from 'shared-api';
import { CustomerStats, EMPTY_CUSTOMER_STATS, LoyaltyBalance } from 'shared-models';
import { LangService } from '../../core/lang.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { ACHIEVEMENTS, Achievement, buildAchievementFlags } from '../news/achievements';

const SEEN_ACHIEVEMENTS_KEY = 'yurt_seen_achievements';

@Component({
  selector: 'app-achievements',
  standalone: true,
  imports: [RouterLink, TranslatePipe],
  templateUrl: './achievements.component.html',
  styleUrl: './achievements.component.css',
})
export class AchievementsComponent implements OnInit {
  private api = inject(YurtApiService);
  readonly lang = inject(LangService);

  readonly loading = signal(true);
  readonly stats = signal<CustomerStats>(EMPTY_CUSTOMER_STATS);
  readonly loyaltyBalance = signal<number | null>(null);
  readonly animatedCount = signal(0);
  readonly newlyUnlocked = signal<Achievement[]>([]);

  readonly total = ACHIEVEMENTS.length;
  readonly circumference = 2 * Math.PI * 40;

  readonly unlockedIds = computed(() => {
    const s = this.stats();
    const flags = buildAchievementFlags(this.loyaltyBalance());
    return new Set(ACHIEVEMENTS.filter((a) => a.condition(s, flags)).map((a) => a.id));
  });

  readonly sortedAchievements = computed(() => {
    const ids = this.unlockedIds();
    return [...ACHIEVEMENTS].sort((a, b) => {
      const au = ids.has(a.id) ? 0 : 1;
      const bu = ids.has(b.id) ? 0 : 1;
      return au - bu;
    });
  });

  readonly ringOffset = computed(() =>
    this.circumference * (1 - this.animatedCount() / this.total)
  );

  ngOnInit(): void {
    forkJoin({
      stats: this.api.getCustomerStats(),
      loyalty: this.api.getLoyaltyBalance().pipe(
        catchError(() => of<LoyaltyBalance>({ enabled: false, available: false, linked: false, balance: null, earnPercent: 0 }))
      ),
    }).subscribe({
      next: ({ stats, loyalty }) => {
        // Defends against a backend still on the old /me/stats shape (pre-deploy,
        // rolling deploy) — missing fields fall back to 0 instead of surfacing
        // "undefined" in a badge's progress label.
        this.stats.set({ ...EMPTY_CUSTOMER_STATS, ...stats });
        this.loyaltyBalance.set(loyalty.balance);
        this.loading.set(false);
        const unlocked = this.unlockedIds().size;
        setTimeout(() => this.animateCount(0, unlocked, 700), 250);
        this.detectNewUnlocks();
      },
      error: () => this.loading.set(false),
    });
  }

  private detectNewUnlocks(): void {
    const seen = new Set(JSON.parse(localStorage.getItem(SEEN_ACHIEVEMENTS_KEY) ?? '[]'));
    const unlocked = this.unlockedIds();
    const fresh = ACHIEVEMENTS.filter((a) => unlocked.has(a.id) && !seen.has(a.id));
    localStorage.setItem(SEEN_ACHIEVEMENTS_KEY, JSON.stringify([...unlocked]));
    if (fresh.length && seen.size > 0) {
      // Only celebrate once there's a prior baseline — a first-ever visit already
      // shows every earned badge unlocked, so nothing here reads as "new".
      this.newlyUnlocked.set(fresh);
      setTimeout(() => this.newlyUnlocked.set([]), 3200);
    }
  }

  dismissCelebration(): void {
    this.newlyUnlocked.set([]);
  }

  private animateCount(from: number, to: number, duration: number): void {
    const start = performance.now();
    const step = (now: number) => {
      const t = Math.min((now - start) / duration, 1);
      const eased = 1 - Math.pow(1 - t, 3);
      this.animatedCount.set(Math.round(from + (to - from) * eased));
      if (t < 1) requestAnimationFrame(step);
    };
    requestAnimationFrame(step);
  }

  isUnlocked(a: Achievement): boolean {
    return this.unlockedIds().has(a.id);
  }

  name(a: Achievement): string {
    const l = this.lang.lang();
    return l === 'kk' ? a.nameKk : l === 'ru' ? a.nameRu : a.nameEn;
  }

  desc(a: Achievement): string {
    const l = this.lang.lang();
    return l === 'kk' ? a.descKk : l === 'ru' ? a.descRu : a.descEn;
  }

  progressInfo(a: Achievement): { pct: number; label: string } | null {
    const s = this.stats();
    const l: 'en' | 'ru' | 'kk' = this.lang.lang() === 'ru' ? 'ru' : this.lang.lang() === 'kk' ? 'kk' : 'en';

    type Unit = 'orders' | 'drinks' | 'spent' | 'weeks' | 'weekends' | 'days' | 'locations' | 'points';
    const unitLabels: Record<Unit, Record<'en' | 'ru' | 'kk', string>> = {
      orders:    { en: 'orders',    ru: 'заказов',  kk: 'тапсырыс' },
      drinks:    { en: 'drinks',    ru: 'напитков', kk: 'сусын' },
      spent:     { en: '',          ru: '',         kk: '' }, // formatted separately, as K ₸
      weeks:     { en: 'weeks',     ru: 'недель',   kk: 'апта' },
      weekends:  { en: 'weekends',  ru: 'выходных', kk: 'демалыс' },
      days:      { en: 'days',      ru: 'дней',     kk: 'күн' },
      locations: { en: 'locations', ru: 'точек',    kk: 'нүкте' },
      points:    { en: 'pts',       ru: 'баллов',   kk: 'ұпай' },
    };

    type Entry = { current: number; target: number; unit: Unit };
    const map: Record<string, Entry> = {
      first_sip:       { current: s.totalOrders,             target: 1,      unit: 'orders' },
      coffee_rookie:   { current: s.totalOrders,             target: 3,      unit: 'orders' },
      golden_cup:      { current: s.totalOrders,             target: 10,     unit: 'orders' },
      altyn_regular:   { current: s.maxWeeklyStreak,         target: 8,      unit: 'weeks' },
      altyncup_star:   { current: s.totalOrders,             target: 50,     unit: 'orders' },
      century_sipper:  { current: s.totalDrinks,             target: 100,    unit: 'drinks' },
      big_spender:     { current: s.totalSpent,              target: 500000, unit: 'spent' },
      altyn_champion:  { current: s.totalSpent,              target: 750000, unit: 'spent' },
      globetrotter:    { current: s.distinctLocations,       target: 5,      unit: 'locations' },
      early_riser:     { current: s.earlyOrders,             target: 10,     unit: 'orders' },
      midnight_brew:   { current: s.lateOrders,              target: 5,      unit: 'orders' },
      weekend_warrior: { current: s.maxWeekendStreak,        target: 4,      unit: 'weekends' },
      streak_master:   { current: s.maxDailyStreak,          target: 7,      unit: 'days' },
      caffeine_shield: { current: s.maxDailyDrinks,          target: 3,      unit: 'drinks' },
      perfect_brew:    { current: s.maxRepeatItemCount,      target: 5,      unit: 'orders' },
      bean_collector:  { current: this.loyaltyBalance() ?? 0, target: 5000,  unit: 'points' },
      bean_counter:    { current: s.redeemedOrders,          target: 5,      unit: 'orders' },
    };
    const p = map[a.id];
    if (!p) return null;
    const pct = Math.min(1, p.current / p.target);
    const label = p.unit === 'spent'
      ? `${Math.round(p.current / 1000)}K / ${p.target / 1000}K ₸`
      : `${p.current} / ${p.target} ${unitLabels[p.unit][l]}`;
    return { pct, label };
  }
}
