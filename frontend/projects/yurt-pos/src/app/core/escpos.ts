/**
 * Receipts and kitchen tickets printed by the till itself (a Bluetooth printer next to it), for sales made while the
 * internet is down. The same layout and the same printer commands as the server's (backend PrintLayouts / EscPos), so
 * a receipt looks the same whether the server or the till printed it. No Angular here, so it is tested on its own.
 */

/** What the till needs to know about its printer (copied from the printer's settings in the admin app). */
export interface TillPrinterSettings {
  /** Characters per line: 32 for 58 mm paper, 48 for 80 mm. */
  lineWidth: number;
  /** The printer's character table for code page 866 (17 on most printers). */
  codeTable: number;
  language: 'ru' | 'kk' | 'en';
  /** A cash drawer is plugged into the printer. */
  cashDrawer: boolean;
}

export interface TillReceiptLine {
  quantity: number;
  /** A weighed item: the weight in grams (printed as kg instead of the quantity). */
  weightGrams?: number;
  name: string;
  /** Size and add-ons, such as "L, + Oat milk". */
  extras: string;
  total: number;
}

export interface TillReceipt {
  brand: string;
  branch: string;
  at: Date;
  /** The number on the receipt and the kitchen ticket. */
  number: string;
  lines: TillReceiptLine[];
  total: number;
  /** A tip on top of the total; printed under it. */
  tip?: number;
  method: string;
  /** Made without internet: says that the tax receipt comes later. */
  offline: boolean;
}

const WORDS: Record<TillPrinterSettings['language'], Record<string, string>> = {
  ru: {
    total: 'Итого', tip: 'Чаевые', method: 'Оплата', thanks: 'Спасибо!', currency: 'тг', test: 'Тестовая печать', testOk: 'Принтер работает',
    Cash: 'Наличные', Card: 'Карта', KaspiBank: 'Kaspi', HalykBank: 'Halyk', FreedomBank: 'Freedom',
    offline: 'Продажа без интернета.', offlineTax: 'Фискальный чек оформится, когда касса выйдет в сеть.',
  },
  kk: {
    total: 'Барлығы', tip: 'Шайлық', method: 'Төлем', thanks: 'Рахмет!', currency: 'тг', test: 'Сынақ басып шығару', testOk: 'Принтер жұмыс істейді',
    Cash: 'Қолма-қол', Card: 'Карта', KaspiBank: 'Kaspi', HalykBank: 'Halyk', FreedomBank: 'Freedom',
    offline: 'Интернетсіз сатылым.', offlineTax: 'Фискалдық чек касса желіге қосылғанда рәсімделеді.',
  },
  en: {
    total: 'Total', tip: 'Tip', method: 'Payment', thanks: 'Thank you!', currency: 'KZT', test: 'Test print', testOk: 'The printer works',
    Cash: 'Cash', Card: 'Card', KaspiBank: 'Kaspi', HalykBank: 'Halyk', FreedomBank: 'Freedom',
    offline: 'Sold without internet.', offlineTax: 'The tax receipt is issued once the till is back online.',
  },
};

const ESC = 0x1b;
const GS = 0x1d;
const LF = 0x0a;

/** Letters code page 866 does not have, swapped for close ones (Kazakh letters print as the nearest Russian one). */
const REPLACE: Record<string, string> = {
  'ә': 'а', 'Ә': 'А', 'ғ': 'г', 'Ғ': 'Г', 'қ': 'к', 'Қ': 'К', 'ң': 'н', 'Ң': 'Н', 'ө': 'о', 'Ө': 'О', 'ұ': 'у', 'Ұ': 'У',
  'ү': 'у', 'Ү': 'У', 'һ': 'х', 'Һ': 'Х', 'і': 'i', 'І': 'I', '₸': 'тг', '×': 'x', '−': '-', '–': '-', '—': '-', '«': '"',
  '»': '"', '“': '"', '”': '"', '’': "'", '…': '...', '№': 'N', ' ': ' ', ' ': ' ',
};

/** One character in code page 866; '?' for anything it does not hold. */
export function cp866(ch: string): number {
  const c = ch.charCodeAt(0);
  if (c < 0x80) return c;
  if (c >= 0x410 && c <= 0x42f) return 0x80 + (c - 0x410); // А-Я
  if (c >= 0x430 && c <= 0x43f) return 0xa0 + (c - 0x430); // а-п
  if (c >= 0x440 && c <= 0x44f) return 0xe0 + (c - 0x440); // р-я
  if (c === 0x401) return 0xf0; // Ё
  if (c === 0x451) return 0xf1; // ё
  return 0x3f;
}

/** Swaps characters code page 866 does not have and drops control characters, like the server does. */
export function clean(text: string): string {
  let out = '';
  for (const ch of text) {
    if (REPLACE[ch] !== undefined) out += REPLACE[ch];
    else if (ch === '\n' || ch === '\r' || ch === '\t') out += ' ';
    else if (ch.charCodeAt(0) >= 0x20 && ch.charCodeAt(0) !== 0x7f) out += ch;
  }
  return out.trim();
}

/** Splits text into lines of at most `width` characters, breaking at spaces (a very long word is cut). */
export function wrap(text: string, width: number): string[] {
  const lines: string[] = [];
  let current = '';
  for (let word of text.split(' ').filter((w) => w.length > 0)) {
    while (word.length > width) {
      if (current) { lines.push(current); current = ''; }
      lines.push(word.slice(0, width));
      word = word.slice(width);
    }
    if (current && current.length + 1 + word.length > width) { lines.push(current); current = ''; }
    current = current ? `${current} ${word}` : word;
  }
  if (current || lines.length === 0) lines.push(current);
  return lines;
}

/** Builds the bytes for an ESC/POS thermal printer. */
export class EscPos {
  private bytes: number[] = [];

  constructor(readonly width: number, codeTable: number) {
    this.raw(ESC, 0x40); // reset
    this.raw(ESC, 0x74, codeTable); // character table
  }

  raw(...b: number[]): this {
    this.bytes.push(...b);
    return this;
  }

  center(): this { return this.raw(ESC, 0x61, 1); }
  left(): this { return this.raw(ESC, 0x61, 0); }
  bold(on: boolean): this { return this.raw(ESC, 0x45, on ? 1 : 0); }
  /** Double width and height; each line then holds half as many characters. */
  big(on: boolean): this { return this.raw(GS, 0x21, on ? 0x11 : 0); }

  line(text = '', width = this.width): this {
    for (const l of wrap(clean(text), width)) this.text(l).raw(LF);
    return this;
  }

  /** Text on the left and a value on the right of one line; a long left part wraps above the value. */
  columns(left: string, right: string): this {
    left = clean(left);
    right = clean(right);
    const lines = wrap(left, Math.max(1, this.width - right.length - 1));
    for (let i = 0; i < lines.length - 1; i++) this.text(lines[i]).raw(LF);
    const last = lines[lines.length - 1];
    return this.text(last + ' '.repeat(Math.max(1, this.width - last.length - right.length)) + right).raw(LF);
  }

  rule(ch = '-'): this { return this.text(ch.repeat(this.width)).raw(LF); }
  /** Feeds a little paper and cuts (a printer with no cutter just feeds). */
  cut(): this { return this.raw(ESC, 0x64, 4).raw(GS, 0x56, 66, 0); }
  /** A pulse on both drawer pins; only the one with a drawer does anything. */
  openDrawer(): this { return this.raw(ESC, 0x70, 0, 25, 250).raw(ESC, 0x70, 1, 25, 250); }

  toBytes(): Uint8Array {
    return Uint8Array.from(this.bytes);
  }

  private text(t: string): this {
    for (const ch of t) this.bytes.push(cp866(ch));
    return this;
  }
}

function words(p: TillPrinterSettings): Record<string, string> {
  return WORDS[p.language] ?? WORDS.ru;
}

function money(n: number, w: Record<string, string>): string {
  return `${Math.round(n).toLocaleString('en-US').replace(/,/g, ' ')} ${w['currency']}`;
}

function pad(n: number): string {
  return String(n).padStart(2, '0');
}

function dateTime(d: Date): string {
  return `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function methodName(method: string, p: TillPrinterSettings): string {
  return words(p)[method] ?? method;
}

/** The guest's receipt, like the server's; opens the drawer first for a cash sale when the printer has one. */
export function receiptBytes(p: TillPrinterSettings, r: TillReceipt, openDrawer = false): Uint8Array {
  const w = words(p);
  const e = new EscPos(p.lineWidth, p.codeTable);
  if (openDrawer && p.cashDrawer) e.openDrawer();
  e.center().bold(true).big(true).line(r.brand, Math.floor(e.width / 2)).big(false).bold(false);
  e.line(r.branch).line(dateTime(r.at)).line(`#${r.number}`);
  e.left().rule();
  for (const l of r.lines) {
    e.columns(l.weightGrams ? `${(l.weightGrams / 1000).toFixed(3)} kg ${l.name}` : `${l.quantity} x ${l.name}`, money(l.total, w));
    if (l.extras) e.line(`  ${l.extras}`);
  }
  e.rule();
  e.bold(true).columns(w['total'], money(r.total, w)).bold(false);
  if (r.tip && r.tip > 0) e.columns(w['tip'], money(r.tip, w));
  e.rule();
  e.center().line(`${w['method']}: ${methodName(r.method, p)}`).line(w['thanks']);
  if (r.offline) e.rule().line(w['offline']).line(w['offlineTax']);
  return e.cut().toBytes();
}
