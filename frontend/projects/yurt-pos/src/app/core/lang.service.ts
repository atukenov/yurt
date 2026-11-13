import { Injectable, signal } from '@angular/core';

export type PosLang = 'en' | 'ru' | 'kk';

const T: Record<PosLang, Record<string, string>> = {
  en: {
    'cart.orderType': 'Here or to go', 'cart.here': 'Here', 'cart.toGo': 'To go',
    'app.name': '{cafe} POS',
    'login.title': 'Cashier sign-in', 'login.username': 'Username', 'login.password': 'Password', 'login.submit': 'Sign in',
    'login.invalid': 'Wrong username or password.', 'login.changePassword': 'Change your password in the admin app first, then sign in here.',
    'login.hint': 'Use the same login as in the admin app.',
    'branch.pick': 'Which branch is this till at?', 'branch.change': 'Change branch', 'branch.none': 'No open branches. Add one in the admin app.',
    'top.signOut': 'Sign out', 'top.sales': 'Sales on this till',
    'menu.loading': 'Loading the menu…', 'menu.empty': 'Nothing to sell here yet.', 'menu.all': 'All', 'menu.search': 'Search',
    'menu.retry': 'Try again', 'menu.failed': 'Could not load the menu.',
    'opt.choose': 'Choose: {title}', 'opt.required': 'Required', 'opt.range': 'Choose {min}–{max}', 'opt.atLeast': 'At least {min}', 'opt.upTo': 'Up to {max}', 'opt.size': 'Size', 'opt.addons': 'Add-ons', 'opt.add': 'Add', 'opt.cancel': 'Cancel',
    'cart.title': 'Sale', 'cart.empty': 'Tap items to add them.', 'cart.clear': 'Clear', 'cart.total': 'Total',
    'cart.items.one': '{count} item', 'cart.items.other': '{count} items',
    'cart.handedOver': 'Give it now (nothing for the kitchen)', 'cart.ready': 'Ready in', 'cart.min': 'min',
    'pay.title': 'How did the guest pay?', 'pay.Cash': 'Cash', 'pay.Card': 'Card', 'pay.KaspiBank': 'Kaspi', 'pay.HalykBank': 'Halyk',
    'pay.FreedomBank': 'Freedom', 'pay.given': 'Cash given', 'pay.change': 'Change: {amount}', 'pay.short': 'Not enough cash.',
    'pay.confirm': 'Paid, finish sale', 'pay.sending': 'Sending…', 'pay.back': 'Back',
    'done.title': 'Sale #{no} done', 'done.toKitchen': 'Sent to the kitchen.', 'done.handedOver': 'Handed over.', 'done.next': 'Next guest',
     'menu.combo': 'Combo',
    'err.failed': 'The server did not accept this sale. An item may have left the menu: check the sale and try again.',
    'err.signIn': 'Your sign-in has run out. Sign out and sign in again; the sale is still here.',
    'queue.offline': 'No internet. Keep selling: sales are saved on this tablet and go up when the internet is back.',
    'queue.waiting.one': '{count} sale waiting to upload.', 'queue.waiting.other': '{count} sales waiting to upload.',
    'queue.sendNow': 'Send now', 'queue.sending': 'Sending…',
    'queue.signIn': 'Sign in again to upload the waiting sales. They stay on this tablet.',
    'queue.refused': 'The server did not accept the sale of {time} for {amount}.',
    'queue.tooOld': 'Older than 7 days: enter it by hand in the admin app.',
    'queue.retry': 'Try again', 'queue.remove': 'Remove',
    'queue.removeConfirm': 'Remove this sale from the tablet? It will not be uploaded.',
    'done.savedTitle': 'Sale saved on this tablet', 'done.savedText': 'No internet. It goes up to the server by itself when the internet is back.',
    'menu.saved': 'No internet: menu saved {time}.',
    'sales.pending': 'waiting',
    'printer.title': 'Receipt printer', 'printer.on': 'Printer ✓',
    'printer.help': 'Connect the Bluetooth printer next to this till. Receipts of online sales come from the server; a sale made without internet is printed by the till itself. Add the printer first in the admin app (Printers, "Bluetooth, through a tablet").',
    'printer.unsupported': 'This browser cannot use Bluetooth. Use Chrome on an Android tablet, Windows or Mac.',
    'printer.none': 'No Bluetooth printers for this branch yet.', 'printer.connect': 'Connect', 'printer.disconnect': 'Disconnect',
    'printer.connectedTo': 'Connected to {name}', 'printer.notConnected': 'Not connected to this tablet',
    'printer.printed': 'The receipt was printed.', 'printer.failed': 'The receipt did not print: {reason}',
    'sales.title': 'Sales on this till today', 'sales.empty': 'No sales yet today.', 'sales.close': 'Close',
    'tip.title': 'Tip', 'tip.none': 'No tip', 'tip.custom': 'Other amount', 'tip.inCash': 'Guest gives the tip in cash', 'tip.line': 'Tip',
    'disc.button': 'Discount', 'disc.title': 'Give a discount',
    'disc.hint': 'A manager types their PIN and the reason. The server checks the PIN when the sale is sent.', 'disc.reason': 'Reason',
    'disc.pin': 'Manager PIN', 'disc.confirm': 'Apply discount',
    'disc.kind': 'Discount type', 'disc.amount': 'Amount',
    'disc.value': 'Discount', 'disc.applied': 'Discount',
    'disc.toPay': 'To pay', 'disc.remove': 'Remove the discount',
    'disc.offline': 'A discount needs the internet and a manager PIN. Remove the discount or wait for the internet.', 'cancel.button': 'Cancel',
    'cancel.title': 'Cancel this sale', 'cancel.hint': 'Hand the money back to the guest. A manager types their PIN and the reason.',
    'cancel.confirm': 'Cancel sale', 'sales.cancelled': 'cancelled',
    'staff.button': 'Staff PIN', 'staff.title': 'Switch cashier or clock in',
    'staff.hint': 'Type your own PIN. "Take over" makes the next sales yours; "Clock in / out" starts or ends your working hours.',
    'staff.pin': 'Your PIN', 'staff.switch': 'Take over the till', 'staff.clock': 'Clock in / out',
    'staff.onClock': 'On the clock now', 'staff.nobody': 'Nobody is clocked in.', 'staff.since': 'since',
    'staff.clockedIn': '{name} clocked in at {time}.', 'staff.clockedOut': '{name} clocked out at {time} ({hours} h).',
    'staff.forgot': 'The last shift had no clock-out: ask a manager to fix it.',
    'staff.offline': 'No internet. The PIN is checked by the server: try again when it is back.', 'staff.failed': 'That did not work.',
  },
  ru: {
    'cart.orderType': 'Здесь или с собой', 'cart.here': 'Здесь', 'cart.toGo': 'С собой',
    'app.name': '{cafe} POS',
    'login.title': 'Вход для кассира', 'login.username': 'Логин', 'login.password': 'Пароль', 'login.submit': 'Войти',
    'login.invalid': 'Неверный логин или пароль.', 'login.changePassword': 'Сначала смените пароль в админ-панели, потом войдите здесь.',
    'login.hint': 'Тот же логин, что и в админ-панели.',
    'branch.pick': 'В каком филиале стоит эта касса?', 'branch.change': 'Сменить филиал', 'branch.none': 'Нет открытых филиалов. Добавьте филиал в админ-панели.',
    'top.signOut': 'Выйти', 'top.sales': 'Продажи на этой кассе',
    'menu.loading': 'Загружаем меню…', 'menu.empty': 'Здесь пока нечего продавать.', 'menu.all': 'Все', 'menu.search': 'Поиск',
    'menu.retry': 'Повторить', 'menu.failed': 'Не удалось загрузить меню.',
    'opt.choose': 'Выберите: {title}', 'opt.required': 'Обязательно', 'opt.range': 'Выберите {min}–{max}', 'opt.atLeast': 'Минимум {min}', 'opt.upTo': 'Не более {max}', 'opt.size': 'Размер', 'opt.addons': 'Добавки', 'opt.add': 'Добавить', 'opt.cancel': 'Отмена',
    'cart.title': 'Продажа', 'cart.empty': 'Нажмите на позиции, чтобы добавить.', 'cart.clear': 'Очистить', 'cart.total': 'Итого',
    'cart.items.one': '{count} позиция', 'cart.items.few': '{count} позиции', 'cart.items.many': '{count} позиций', 'cart.items.other': '{count} позиции',
    'cart.handedOver': 'Отдать сразу (без кухни)', 'cart.ready': 'Готово через', 'cart.min': 'мин',
    'pay.title': 'Как оплатил гость?', 'pay.Cash': 'Наличные', 'pay.Card': 'Карта', 'pay.KaspiBank': 'Kaspi', 'pay.HalykBank': 'Halyk',
    'pay.FreedomBank': 'Freedom', 'pay.given': 'Дали наличными', 'pay.change': 'Сдача: {amount}', 'pay.short': 'Не хватает наличных.',
    'pay.confirm': 'Оплачено, завершить', 'pay.sending': 'Отправляем…', 'pay.back': 'Назад',
    'done.title': 'Продажа №{no} готова', 'done.toKitchen': 'Отправлено на кухню.', 'done.handedOver': 'Выдано гостю.', 'done.next': 'Следующий гость',
     'menu.combo': 'Комбо',
    'err.failed': 'Сервер не принял эту продажу. Возможно, позицию убрали из меню: проверьте продажу и попробуйте ещё раз.',
    'err.signIn': 'Вход устарел. Выйдите и войдите снова; продажа никуда не пропадёт.',
    'queue.offline': 'Нет интернета. Продавайте дальше: продажи сохраняются на планшете и уйдут на сервер, когда интернет вернётся.',
    'queue.waiting.one': '{count} продажа ждёт отправки.', 'queue.waiting.few': '{count} продажи ждут отправки.',
    'queue.waiting.many': '{count} продаж ждут отправки.', 'queue.waiting.other': '{count} продажи ждут отправки.',
    'queue.sendNow': 'Отправить сейчас', 'queue.sending': 'Отправляем…',
    'queue.signIn': 'Войдите снова, чтобы отправить ожидающие продажи. Они остаются на планшете.',
    'queue.refused': 'Сервер не принял продажу от {time} на {amount}.',
    'queue.tooOld': 'Старше 7 дней: внесите её вручную в админке.',
    'queue.retry': 'Повторить', 'queue.remove': 'Удалить',
    'queue.removeConfirm': 'Удалить эту продажу с планшета? Она не будет отправлена.',
    'done.savedTitle': 'Продажа сохранена на планшете', 'done.savedText': 'Нет интернета. Она сама уйдёт на сервер, когда интернет вернётся.',
    'menu.saved': 'Нет интернета: меню сохранено {time}.',
    'sales.pending': 'ждёт',
    'printer.title': 'Чековый принтер', 'printer.on': 'Принтер ✓',
    'printer.help': 'Подключите Bluetooth-принтер рядом с кассой. Чеки онлайн-продаж приходят с сервера; продажу без интернета касса печатает сама. Сначала добавьте принтер в админке (Принтеры, «Bluetooth, через планшет»).',
    'printer.unsupported': 'Этот браузер не умеет Bluetooth. Откройте кассу в Chrome на Android-планшете, Windows или Mac.',
    'printer.none': 'Для этого филиала пока нет Bluetooth-принтеров.', 'printer.connect': 'Подключить', 'printer.disconnect': 'Отключить',
    'printer.connectedTo': 'Подключён: {name}', 'printer.notConnected': 'Не подключён к этому планшету',
    'printer.printed': 'Чек напечатан.', 'printer.failed': 'Чек не напечатался: {reason}',
    'sales.title': 'Продажи на этой кассе сегодня', 'sales.empty': 'Сегодня продаж ещё нет.', 'sales.close': 'Закрыть',
    'tip.title': 'Чаевые', 'tip.none': 'Без чаевых', 'tip.custom': 'Другая сумма', 'tip.inCash': 'Гость дал чаевые наличными', 'tip.line': 'Чаевые',
    'disc.button': 'Скидка', 'disc.title': 'Дать скидку',
    'disc.hint': 'Менеджер вводит PIN и причину. Сервер проверит PIN при отправке продажи.', 'disc.reason': 'Причина',
    'disc.pin': 'PIN менеджера', 'disc.confirm': 'Применить скидку',
    'disc.kind': 'Тип скидки', 'disc.amount': 'Сумма',
    'disc.value': 'Скидка', 'disc.applied': 'Скидка',
    'disc.toPay': 'К оплате', 'disc.remove': 'Убрать скидку',
    'disc.offline': 'Для скидки нужен интернет и PIN менеджера. Уберите скидку или дождитесь интернета.', 'cancel.button': 'Отменить',
    'cancel.title': 'Отменить продажу', 'cancel.hint': 'Верните деньги гостю. Менеджер вводит PIN и причину.',
    'cancel.confirm': 'Отменить продажу', 'sales.cancelled': 'отменена',
    'staff.button': 'PIN сотрудника', 'staff.title': 'Сменить кассира или отметить приход',
    'staff.hint': 'Введите свой PIN. «Сесть за кассу» — следующие продажи будут на вас; «Приход / уход» начинает или заканчивает рабочие часы.',
    'staff.pin': 'Ваш PIN', 'staff.switch': 'Сесть за кассу', 'staff.clock': 'Приход / уход',
    'staff.onClock': 'Сейчас на смене', 'staff.nobody': 'Никто не отметился.', 'staff.since': 'с',
    'staff.clockedIn': '{name}: приход в {time}.', 'staff.clockedOut': '{name}: уход в {time} ({hours} ч).',
    'staff.forgot': 'Прошлая смена осталась без ухода: попросите менеджера исправить.',
    'staff.offline': 'Нет интернета. PIN проверяет сервер: повторите, когда связь вернётся.', 'staff.failed': 'Не получилось.',
  },
  kk: {
    'cart.orderType': 'Осында немесе алып кету', 'cart.here': 'Осында', 'cart.toGo': 'Алып кету',
    'app.name': '{cafe} POS',
    'login.title': 'Кассирдің кіруі', 'login.username': 'Логин', 'login.password': 'Құпиясөз', 'login.submit': 'Кіру',
    'login.invalid': 'Логин немесе құпиясөз қате.', 'login.changePassword': 'Алдымен әкімші қолданбасында құпиясөзді ауыстырыңыз, содан кейін осында кіріңіз.',
    'login.hint': 'Әкімші қолданбасындағы логинмен кіріңіз.',
    'branch.pick': 'Бұл касса қай филиалда тұр?', 'branch.change': 'Филиалды ауыстыру', 'branch.none': 'Ашық филиал жоқ. Әкімші қолданбасында филиал қосыңыз.',
    'top.signOut': 'Шығу', 'top.sales': 'Осы кассадағы сатылымдар',
    'menu.loading': 'Мәзір жүктелуде…', 'menu.empty': 'Мұнда әзірге сататын ештеңе жоқ.', 'menu.all': 'Барлығы', 'menu.search': 'Іздеу',
    'menu.retry': 'Қайталау', 'menu.failed': 'Мәзір жүктелмеді.',
    'opt.choose': 'Таңдаңыз: {title}', 'opt.required': 'Міндетті', 'opt.range': '{min}–{max} таңдаңыз', 'opt.atLeast': 'Кемінде {min}', 'opt.upTo': 'Көбі {max}', 'opt.size': 'Өлшемі', 'opt.addons': 'Қоспалар', 'opt.add': 'Қосу', 'opt.cancel': 'Болдырмау',
    'cart.title': 'Сатылым', 'cart.empty': 'Қосу үшін тағамдарды басыңыз.', 'cart.clear': 'Тазалау', 'cart.total': 'Жиыны',
    'cart.items.one': '{count} позиция', 'cart.items.other': '{count} позиция',
    'cart.handedOver': 'Бірден беру (асүйсіз)', 'cart.ready': 'Дайын болады', 'cart.min': 'мин',
    'pay.title': 'Қонақ қалай төледі?', 'pay.Cash': 'Қолма-қол', 'pay.Card': 'Карта', 'pay.KaspiBank': 'Kaspi', 'pay.HalykBank': 'Halyk',
    'pay.FreedomBank': 'Freedom', 'pay.given': 'Қолма-қол берілді', 'pay.change': 'Қайтарым: {amount}', 'pay.short': 'Қолма-қол жетпейді.',
    'pay.confirm': 'Төленді, аяқтау', 'pay.sending': 'Жіберілуде…', 'pay.back': 'Артқа',
    'done.title': '№{no} сатылым дайын', 'done.toKitchen': 'Асүйге жіберілді.', 'done.handedOver': 'Қонаққа берілді.', 'done.next': 'Келесі қонақ',
     'menu.combo': 'Комбо',
    'err.failed': 'Сервер бұл сатылымды қабылдамады. Мүмкін, позиция мәзірден алынған: сатылымды тексеріп, қайта көріңіз.',
    'err.signIn': 'Кіру мерзімі бітті. Шығып, қайта кіріңіз; сатылым жоғалмайды.',
    'queue.offline': 'Интернет жоқ. Сатуды жалғастырыңыз: сатылымдар планшетте сақталып, интернет қайтқанда серверге кетеді.',
    'queue.waiting.one': '{count} сатылым жіберуді күтуде.', 'queue.waiting.other': '{count} сатылым жіберуді күтуде.',
    'queue.sendNow': 'Қазір жіберу', 'queue.sending': 'Жіберілуде…',
    'queue.signIn': 'Күтудегі сатылымдарды жіберу үшін қайта кіріңіз. Олар планшетте қалады.',
    'queue.refused': 'Сервер {time} уақыттағы {amount} сатылымын қабылдамады.',
    'queue.tooOld': '7 күннен ескі: оны админкада қолмен енгізіңіз.',
    'queue.retry': 'Қайталау', 'queue.remove': 'Өшіру',
    'queue.removeConfirm': 'Бұл сатылымды планшеттен өшіру керек пе? Ол жіберілмейді.',
    'done.savedTitle': 'Сатылым планшетте сақталды', 'done.savedText': 'Интернет жоқ. Интернет қайтқанда ол серверге өзі кетеді.',
    'menu.saved': 'Интернет жоқ: мәзір {time} сақталған.',
    'sales.pending': 'күтуде',
    'printer.title': 'Чек принтері', 'printer.on': 'Принтер ✓',
    'printer.help': 'Касса жанындағы Bluetooth-принтерді қосыңыз. Онлайн сатылымдардың чектері серверден келеді; интернетсіз сатылымды касса өзі басады. Алдымен принтерді админкада қосыңыз (Принтерлер, «Bluetooth, планшет арқылы»).',
    'printer.unsupported': 'Бұл браузер Bluetooth қолданбайды. Кассаны Android-планшеттегі, Windows немесе Mac-тағы Chrome-да ашыңыз.',
    'printer.none': 'Бұл филиалда әзірге Bluetooth-принтер жоқ.', 'printer.connect': 'Қосу', 'printer.disconnect': 'Ажырату',
    'printer.connectedTo': 'Қосылған: {name}', 'printer.notConnected': 'Бұл планшетке қосылмаған',
    'printer.printed': 'Чек басылды.', 'printer.failed': 'Чек басылмады: {reason}',
    'sales.title': 'Бүгін осы кассадағы сатылымдар', 'sales.empty': 'Бүгін әлі сатылым жоқ.', 'sales.close': 'Жабу',
    'tip.title': 'Шайлық', 'tip.none': 'Шайлықсыз', 'tip.custom': 'Басқа сома', 'tip.inCash': 'Қонақ шайлықты қолма-қол берді', 'tip.line': 'Шайлық',
    'disc.button': 'Жеңілдік', 'disc.title': 'Жеңілдік беру',
    'disc.hint': 'Менеджер PIN мен себебін енгізеді. Сервер PIN-ді сатуды жібергенде тексереді.', 'disc.reason': 'Себебі',
    'disc.pin': 'Менеджердің PIN-коды', 'disc.confirm': 'Жеңілдікті қолдану',
    'disc.kind': 'Жеңілдік түрі', 'disc.amount': 'Сома',
    'disc.value': 'Жеңілдік', 'disc.applied': 'Жеңілдік',
    'disc.toPay': 'Төлеуге', 'disc.remove': 'Жеңілдікті алып тастау',
    'disc.offline': 'Жеңілдік үшін интернет пен менеджердің PIN-коды қажет. Жеңілдікті алып тастаңыз немесе интернетті күтіңіз.', 'cancel.button': 'Болдырмау',
    'cancel.title': 'Сатуды болдырмау', 'cancel.hint': 'Ақшаны қонаққа қайтарыңыз. Менеджер PIN мен себебін енгізеді.',
    'cancel.confirm': 'Сатуды болдырмау', 'sales.cancelled': 'болдырылмады',
    'staff.button': 'Қызметкер PIN-і', 'staff.title': 'Кассирді ауыстыру немесе келуді белгілеу',
    'staff.hint': 'Өз PIN-кодыңызды енгізіңіз. «Кассаға отыру» — келесі сатулар сізге жазылады; «Келу / кету» жұмыс сағатын бастайды немесе аяқтайды.',
    'staff.pin': 'PIN-кодыңыз', 'staff.switch': 'Кассаға отыру', 'staff.clock': 'Келу / кету',
    'staff.onClock': 'Қазір ауысымда', 'staff.nobody': 'Ешкім белгіленбеген.', 'staff.since': 'бастап',
    'staff.clockedIn': '{name}: {time} келді.', 'staff.clockedOut': '{name}: {time} кетті ({hours} сағ).',
    'staff.forgot': 'Өткен ауысымда кету белгіленбеген: менеджерден түзетуін сұраңыз.',
    'staff.offline': 'Интернет жоқ. PIN-ді сервер тексереді: байланыс қайта келгенде қайталаңыз.', 'staff.failed': 'Болмады.',
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
