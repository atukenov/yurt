import { Injectable, signal } from '@angular/core';

export type PosLang = 'en' | 'ru' | 'kk';

const T: Record<PosLang, Record<string, string>> = {
  en: {
    'app.name': '{cafe} POS',
    'login.title': 'Cashier sign-in', 'login.username': 'Username', 'login.password': 'Password', 'login.submit': 'Sign in',
    'login.invalid': 'Wrong username or password.', 'login.changePassword': 'Change your password in the admin app first, then sign in here.',
    'login.hint': 'Use the same login as in the admin app.',
    'branch.pick': 'Which branch is this till at?', 'branch.change': 'Change branch', 'branch.none': 'No open branches. Add one in the admin app.',
    'top.signOut': 'Sign out', 'top.sales': 'Sales on this till',
    'menu.loading': 'Loading the menu…', 'menu.empty': 'Nothing to sell here yet.', 'menu.all': 'All', 'menu.search': 'Search',
    'menu.retry': 'Try again', 'menu.failed': 'Could not load the menu.',
    'opt.size': 'Size', 'opt.addons': 'Add-ons', 'opt.add': 'Add', 'opt.cancel': 'Cancel',
    'cart.title': 'Sale', 'cart.empty': 'Tap items to add them.', 'cart.clear': 'Clear', 'cart.total': 'Total',
    'cart.items.one': '{count} item', 'cart.items.other': '{count} items',
    'cart.handedOver': 'Give it now (nothing for the kitchen)', 'cart.ready': 'Ready in', 'cart.min': 'min',
    'pay.title': 'How did the guest pay?', 'pay.Cash': 'Cash', 'pay.Card': 'Card', 'pay.KaspiBank': 'Kaspi', 'pay.HalykBank': 'Halyk',
    'pay.FreedomBank': 'Freedom', 'pay.given': 'Cash given', 'pay.change': 'Change: {amount}', 'pay.short': 'Not enough cash.',
    'pay.confirm': 'Paid, finish sale', 'pay.sending': 'Sending…', 'pay.back': 'Back',
    'done.title': 'Sale #{no} done', 'done.toKitchen': 'Sent to the kitchen.', 'done.handedOver': 'Handed over.', 'done.next': 'Next guest',
    'err.failed': 'The sale was not saved. Check the internet and press the button again; it will not be counted twice.',
    'sales.title': 'Sales on this till today', 'sales.empty': 'No sales yet today.', 'sales.close': 'Close',
  },
  ru: {
    'app.name': '{cafe} POS',
    'login.title': 'Вход для кассира', 'login.username': 'Логин', 'login.password': 'Пароль', 'login.submit': 'Войти',
    'login.invalid': 'Неверный логин или пароль.', 'login.changePassword': 'Сначала смените пароль в админ-панели, потом войдите здесь.',
    'login.hint': 'Тот же логин, что и в админ-панели.',
    'branch.pick': 'В каком филиале стоит эта касса?', 'branch.change': 'Сменить филиал', 'branch.none': 'Нет открытых филиалов. Добавьте филиал в админ-панели.',
    'top.signOut': 'Выйти', 'top.sales': 'Продажи на этой кассе',
    'menu.loading': 'Загружаем меню…', 'menu.empty': 'Здесь пока нечего продавать.', 'menu.all': 'Все', 'menu.search': 'Поиск',
    'menu.retry': 'Повторить', 'menu.failed': 'Не удалось загрузить меню.',
    'opt.size': 'Размер', 'opt.addons': 'Добавки', 'opt.add': 'Добавить', 'opt.cancel': 'Отмена',
    'cart.title': 'Продажа', 'cart.empty': 'Нажмите на позиции, чтобы добавить.', 'cart.clear': 'Очистить', 'cart.total': 'Итого',
    'cart.items.one': '{count} позиция', 'cart.items.few': '{count} позиции', 'cart.items.many': '{count} позиций', 'cart.items.other': '{count} позиции',
    'cart.handedOver': 'Отдать сразу (без кухни)', 'cart.ready': 'Готово через', 'cart.min': 'мин',
    'pay.title': 'Как оплатил гость?', 'pay.Cash': 'Наличные', 'pay.Card': 'Карта', 'pay.KaspiBank': 'Kaspi', 'pay.HalykBank': 'Halyk',
    'pay.FreedomBank': 'Freedom', 'pay.given': 'Дали наличными', 'pay.change': 'Сдача: {amount}', 'pay.short': 'Не хватает наличных.',
    'pay.confirm': 'Оплачено, завершить', 'pay.sending': 'Отправляем…', 'pay.back': 'Назад',
    'done.title': 'Продажа №{no} готова', 'done.toKitchen': 'Отправлено на кухню.', 'done.handedOver': 'Выдано гостю.', 'done.next': 'Следующий гость',
    'err.failed': 'Продажа не сохранилась. Проверьте интернет и нажмите кнопку ещё раз: дважды она не засчитается.',
    'sales.title': 'Продажи на этой кассе сегодня', 'sales.empty': 'Сегодня продаж ещё нет.', 'sales.close': 'Закрыть',
  },
  kk: {
    'app.name': '{cafe} POS',
    'login.title': 'Кассирдің кіруі', 'login.username': 'Логин', 'login.password': 'Құпиясөз', 'login.submit': 'Кіру',
    'login.invalid': 'Логин немесе құпиясөз қате.', 'login.changePassword': 'Алдымен әкімші қолданбасында құпиясөзді ауыстырыңыз, содан кейін осында кіріңіз.',
    'login.hint': 'Әкімші қолданбасындағы логинмен кіріңіз.',
    'branch.pick': 'Бұл касса қай филиалда тұр?', 'branch.change': 'Филиалды ауыстыру', 'branch.none': 'Ашық филиал жоқ. Әкімші қолданбасында филиал қосыңыз.',
    'top.signOut': 'Шығу', 'top.sales': 'Осы кассадағы сатылымдар',
    'menu.loading': 'Мәзір жүктелуде…', 'menu.empty': 'Мұнда әзірге сататын ештеңе жоқ.', 'menu.all': 'Барлығы', 'menu.search': 'Іздеу',
    'menu.retry': 'Қайталау', 'menu.failed': 'Мәзір жүктелмеді.',
    'opt.size': 'Өлшемі', 'opt.addons': 'Қоспалар', 'opt.add': 'Қосу', 'opt.cancel': 'Болдырмау',
    'cart.title': 'Сатылым', 'cart.empty': 'Қосу үшін тағамдарды басыңыз.', 'cart.clear': 'Тазалау', 'cart.total': 'Жиыны',
    'cart.items.one': '{count} позиция', 'cart.items.other': '{count} позиция',
    'cart.handedOver': 'Бірден беру (асүйсіз)', 'cart.ready': 'Дайын болады', 'cart.min': 'мин',
    'pay.title': 'Қонақ қалай төледі?', 'pay.Cash': 'Қолма-қол', 'pay.Card': 'Карта', 'pay.KaspiBank': 'Kaspi', 'pay.HalykBank': 'Halyk',
    'pay.FreedomBank': 'Freedom', 'pay.given': 'Қолма-қол берілді', 'pay.change': 'Қайтарым: {amount}', 'pay.short': 'Қолма-қол жетпейді.',
    'pay.confirm': 'Төленді, аяқтау', 'pay.sending': 'Жіберілуде…', 'pay.back': 'Артқа',
    'done.title': '№{no} сатылым дайын', 'done.toKitchen': 'Асүйге жіберілді.', 'done.handedOver': 'Қонаққа берілді.', 'done.next': 'Келесі қонақ',
    'err.failed': 'Сатылым сақталмады. Интернетті тексеріп, батырманы қайта басыңыз: екі рет есептелмейді.',
    'sales.title': 'Бүгін осы кассадағы сатылымдар', 'sales.empty': 'Бүгін әлі сатылым жоқ.', 'sales.close': 'Жабу',
  },
};

@Injectable({ providedIn: 'root' })
export class PosLangService {
  private readonly KEY = 'yurt_pos_lang';
  readonly lang = signal<PosLang>(PosLangService.saved());

  constructor() {
    document.documentElement.lang = this.lang();
  }

  private static saved(): PosLang {
    try {
      const l = localStorage.getItem('yurt_pos_lang');
      return l === 'en' || l === 'kk' || l === 'ru' ? l : 'ru';
    } catch {
      return 'ru';
    }
  }

  setLang(lang: PosLang): void {
    this.lang.set(lang);
    try { localStorage.setItem(this.KEY, lang); } catch { /* private mode: the choice lasts this visit */ }
    document.documentElement.lang = lang;
  }

  /** The text for `key` with {slots} filled; a number `count` picks the plural form (key.one, key.few...). */
  t(key: string, params?: Record<string, string | number>): string {
    const dict = T[this.lang()];
    let text = dict[key];
    if (text === undefined && typeof params?.['count'] === 'number') {
      const form = new Intl.PluralRules(this.lang()).select(params['count']);
      text = dict[`${key}.${form}`] ?? dict[`${key}.other`];
    }
    text ??= key;
    return params ? text.replace(/\{(\w+)\}/g, (slot, name) => (name in params ? String(params[name]) : slot)) : text;
  }
}
