import { CustomerStats } from 'shared-models';

export interface Achievement {
  id: string;
  icon: string;            // emoji fallback if the image fails or none is assigned
  badge?: string;          // path under /badges — omitted falls back to icon
  nameEn: string;
  nameRu: string;
  nameKk: string;
  descEn: string;
  descRu: string;
  descKk: string;
  condition: (stats: CustomerStats, flags: Record<string, string | null>) => boolean;
}

/** Extra signals conditions need that don't live on CustomerStats — kept in one
 * place so the News summary and the Achievements page always agree on unlocks. */
export function buildAchievementFlags(loyaltyBalance?: number | null): Record<string, string | null> {
  return {
    wolt: localStorage.getItem('yurt_wolt_clicked'),
    loyaltyBalance: loyaltyBalance == null ? null : String(loyaltyBalance),
  };
}

export const ACHIEVEMENTS: Achievement[] = [
  {
    id: 'first_sip',
    icon: '☕',
    badge: '/badges/first-drink.png',
    nameEn: 'First Sip',
    nameRu: 'Первый Глоток',
    nameKk: 'Бірінші Кесе',
    descEn: 'Placed your first order',
    descRu: 'Сделали первый заказ',
    descKk: 'Алғашқы тапсырысыңызды бердіңіз',
    condition: (s) => s.totalOrders >= 1,
  },
  {
    id: 'coffee_rookie',
    icon: '🥉',
    badge: '/badges/coffee-beginner.png',
    nameEn: 'Coffee Rookie',
    nameRu: 'Кофе Новичок',
    nameKk: 'Кофе Жаңадан',
    descEn: '3 orders completed',
    descRu: '3 заказа выполнено',
    descKk: '3 тапсырыс орындалды',
    condition: (s) => s.totalOrders >= 3,
  },
  {
    id: 'golden_cup',
    icon: '🥇',
    badge: '/badges/gold-cup.png',
    nameEn: 'Golden Cup',
    nameRu: 'Золотая Чаша',
    nameKk: 'Алтын Кесе',
    descEn: '10 orders — a loyal regular',
    descRu: '10 заказов — верный постоянный',
    descKk: '10 тапсырыс — адал тұрақты',
    condition: (s) => s.totalOrders >= 10,
  },
  {
    id: 'altyn_regular',
    icon: '💛',
    badge: '/badges/constant.png',
    nameEn: 'Altyn Regular',
    nameRu: 'Постоянный',
    nameKk: 'Тұрақты',
    descEn: 'Ordered at least once a week for 8 weeks straight',
    descRu: 'Заказывали хотя бы раз в неделю 8 недель подряд',
    descKk: '8 апта қатарынан аптасына кемінде бір рет тапсырыс',
    condition: (s) => s.maxWeeklyStreak >= 8,
  },
  {
    id: 'altyncup_star',
    // Doc proposes tying this to "reach the highest loyalty tier, hold 3 months" —
    // no tier concept exists in the iiko integration yet, so it stays order-count-based
    // until that data is tracked.
    icon: '🌟',
    badge: '/badges/star-of-altyncup.png',
    nameEn: 'Altyncup Star',
    nameRu: 'Звезда Алтынкап',
    nameKk: 'Алтынкап Жұлдызы',
    descEn: '50 orders — a shining star',
    descRu: '50 заказов — сияющая звезда',
    descKk: '50 тапсырыс — жарқын жұлдыз',
    condition: (s) => s.totalOrders >= 50,
  },
  {
    id: 'century_sipper',
    icon: '💯',
    badge: '/badges/hundred-cups.png',
    nameEn: 'Century Sipper',
    nameRu: '100 Чашек',
    nameKk: '100 Кесе',
    descEn: '100 drinks — legendary',
    descRu: '100 напитков — легендарный',
    descKk: '100 сусын — аңызға айналған',
    condition: (s) => s.totalDrinks >= 100,
  },
  {
    id: 'big_spender',
    icon: '💰',
    badge: '/badges/coffee-baron.png',
    nameEn: 'Coffee Baron',
    nameRu: 'Кофейный Барон',
    nameKk: 'Кофе Барон',
    descEn: 'Spent over 500,000 ₸',
    descRu: 'Потрачено более 500 000 ₸',
    descKk: '500 000 ₸-тен артық жұмсалды',
    condition: (s) => s.totalSpent >= 500000,
  },
  {
    id: 'altyn_champion',
    // Doc proposes "finish first in a monthly ALTYNCUP challenge" — no contest/leaderboard
    // feature exists to evaluate that, so this stays spend-based. Threshold raised above
    // Coffee Baron's so Champion still reads as the more prestigious of the two.
    icon: '👑',
    badge: '/badges/champion.png',
    nameEn: 'Altyn Champion',
    nameRu: 'Чемпион',
    nameKk: 'Алтынкап Чемпион',
    descEn: 'Spent over 750,000 ₸',
    descRu: 'Потрачено более 750 000 ₸',
    descKk: '750 000 ₸-тен артық жұмсалды',
    condition: (s) => s.totalSpent >= 750000,
  },
  {
    id: 'wolt_rider',
    // No dedicated art — globetrotter.png now belongs to the `globetrotter` achievement
    // below, since location data is available to evaluate it properly.
    icon: '🛵',
    nameEn: 'Wolt Rider',
    nameRu: 'Курьер Wolt',
    nameKk: 'Wolt Жеткізуші',
    descEn: 'Used Wolt delivery',
    descRu: 'Воспользовались доставкой Wolt',
    descKk: 'Wolt жеткізуін пайдаландыңыз',
    condition: (_s, flags) => flags['wolt'] === 'true',
  },
  {
    id: 'globetrotter',
    icon: '🌍',
    badge: '/badges/globetrotter.png',
    nameEn: 'Globetrotter',
    nameRu: 'Путешественник',
    nameKk: 'Саяхатшы',
    descEn: 'Ordered from 5 different locations',
    descRu: 'Заказывали из 5 разных точек',
    descKk: '5 түрлі нүктеден тапсырыс бердіңіз',
    condition: (s) => s.distinctLocations >= 5,
  },
  {
    id: 'early_riser',
    icon: '🌅',
    badge: '/badges/early-riser.png',
    nameEn: 'Early Riser',
    nameRu: 'Ранняя Пташка',
    nameKk: 'Ерте Тұратын',
    descEn: '10 orders placed before 9 AM',
    descRu: '10 заказов до 9 утра',
    descKk: 'Таңғы 9-ға дейін 10 тапсырыс',
    condition: (s) => s.earlyOrders >= 10,
  },
  {
    id: 'midnight_brew',
    icon: '🌙',
    badge: '/badges/midnight-brew.png',
    nameEn: 'Midnight Brew',
    nameRu: 'Полуночный Кофе',
    nameKk: 'Түн Ортасындағы Кофе',
    descEn: '5 orders placed after 9 PM',
    descRu: '5 заказов после 21:00',
    descKk: '21:00-ден кейін 5 тапсырыс',
    condition: (s) => s.lateOrders >= 5,
  },
  {
    id: 'weekend_warrior',
    icon: '🛡️',
    badge: '/badges/weekend-warrior.png',
    nameEn: 'Weekend Warrior',
    nameRu: 'Воин Выходных',
    nameKk: 'Демалыс Жауынгері',
    descEn: 'Ordered on 4 weekends in a row',
    descRu: 'Заказы 4 выходных подряд',
    descKk: 'Қатарынан 4 демалыс күнінде тапсырыс',
    condition: (s) => s.maxWeekendStreak >= 4,
  },
  {
    id: 'streak_master',
    icon: '🔥',
    badge: '/badges/streak-master.png',
    nameEn: 'Streak Master',
    nameRu: 'Мастер Серии',
    nameKk: 'Серия Шебері',
    descEn: 'Ordered 7 days in a row',
    descRu: '7 дней подряд с заказом',
    descKk: 'Қатарынан 7 күн тапсырыс',
    condition: (s) => s.maxDailyStreak >= 7,
  },
  {
    id: 'caffeine_shield',
    icon: '⚡',
    badge: '/badges/caffeine-shield.png',
    nameEn: 'Caffeine Shield',
    nameRu: 'Кофеиновый Щит',
    nameKk: 'Кофеин Қалқаны',
    descEn: '3 or more drinks in a single day',
    descRu: '3 и более напитка за один день',
    descKk: 'Бір күнде 3 немесе одан да көп сусын',
    condition: (s) => s.maxDailyDrinks >= 3,
  },
  {
    id: 'perfect_brew',
    icon: '🎯',
    badge: '/badges/perfect-brew.png',
    nameEn: 'Perfect Brew',
    nameRu: 'Идеальный Напиток',
    nameKk: 'Тамаша Сусын',
    descEn: 'Ordered the same drink 5 times',
    descRu: 'Один и тот же напиток заказан 5 раз',
    descKk: 'Бір сусынды 5 рет тапсырыс бердіңіз',
    condition: (s) => s.maxRepeatItemCount >= 5,
  },
  {
    id: 'bean_collector',
    // Reads the live loyalty balance passed in via flags (not part of CustomerStats,
    // since fetching it lives on a separate iiko-backed endpoint) — locked whenever
    // that balance hasn't been loaded, rather than treating "unknown" as "unlocked".
    icon: '💎',
    badge: '/badges/bean-collector.png',
    nameEn: 'Bean Collector',
    nameRu: 'Коллекционер Бонусов',
    nameKk: 'Бонус Жинаушы',
    descEn: 'Held 5,000 bonus points at once',
    descRu: 'Накопили 5 000 бонусных баллов',
    descKk: 'Бір мезгілде 5 000 бонус ұпай жинадыңыз',
    condition: (_s, flags) => Number(flags['loyaltyBalance'] ?? 0) >= 5000,
  },
  {
    id: 'bean_counter',
    icon: '🧮',
    badge: '/badges/bean-counter.png',
    nameEn: 'Bean Counter',
    nameRu: 'Бонус Мастер',
    nameKk: 'Бонус Есепшісі',
    descEn: 'Redeemed bonus points on 5 separate orders',
    descRu: 'Списали бонусы в 5 разных заказах',
    descKk: '5 бөлек тапсырыста бонус жұмсадыңыз',
    condition: (s) => s.redeemedOrders >= 5,
  },
];
