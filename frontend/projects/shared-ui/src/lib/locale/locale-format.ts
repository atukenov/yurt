import { InjectionToken, Pipe, PipeTransform, inject } from '@angular/core';
import { formatDate, formatNumber, registerLocaleData } from '@angular/common';
import localeRu from '@angular/common/locales/ru';
import localeKk from '@angular/common/locales/kk';

// Angular only ships English month/day names. Registering ru and kk here lets
// dates and numbers follow whichever language the person picked in the app.
registerLocaleData(localeRu);
registerLocaleData(localeKk);

export type UiLang = 'en' | 'ru' | 'kk';

/**
 * Reads the app's current language. Each app provides it from its own
 * language service, so the pipes below re-format as soon as the language
 * changes, without a reload.
 */
export const UI_LANG = new InjectionToken<() => string>('UI_LANG', {
  factory: () => () => 'en',
});

/**
 * Named date styles. Word order and clock style differ by language
 * ("Jan 5 · 3:15 PM" vs "5 янв. · 15:15"), so templates pick a style name and
 * each language supplies its own pattern. Anything that is not a style name is
 * used as a plain Angular date pattern.
 */
export const DATE_STYLES: Record<string, Record<UiLang, string>> = {
  // "January 2026" — standalone month (LLLL) so ru reads "январь", not "января".
  monthYear: { en: 'LLLL y', ru: 'LLLL y', kk: 'LLLL y' },
  // "4 May 1997"
  longDate: { en: 'd MMMM y', ru: 'd MMMM y', kk: 'd MMMM y' },
  // "May 4, 2026"
  shortDate: { en: 'MMM d, y', ru: 'd MMM y', kk: 'd MMM y' },
  // "May 4 · 3:15 PM"
  shortDateTime: { en: 'MMM d · h:mm a', ru: 'd MMM · HH:mm', kk: 'd MMM · HH:mm' },
  // "May 4, 2026 · 3:15 PM"
  longDateTime: { en: 'MMMM d, y · h:mm a', ru: 'd MMMM y · HH:mm', kk: 'd MMMM y · HH:mm' },
  // "May 4, 15:15" — staff screens use a 24-hour clock in every language.
  shortDateTime24: { en: 'MMM d, HH:mm', ru: 'd MMM, HH:mm', kk: 'd MMM, HH:mm' },
  // "May 4, 2026 15:15"
  dateTime24: { en: 'MMM d, y HH:mm', ru: 'd MMM y, HH:mm', kk: 'd MMM y, HH:mm' },
};

export function toUiLang(lang: string | null | undefined): UiLang {
  return lang === 'ru' || lang === 'kk' ? lang : 'en';
}

/** Formats a date in the given language. Empty or invalid input gives ''. */
export function formatLocalDate(
  value: string | number | Date | null | undefined,
  style: string,
  lang: string,
): string {
  if (value === null || value === undefined || value === '') return '';
  const date = value instanceof Date ? value : new Date(value);
  if (isNaN(date.getTime())) return '';
  const locale = toUiLang(lang);
  const pattern = DATE_STYLES[style]?.[locale] ?? style;
  return formatDate(date, pattern, locale);
}

/** Formats a number in the given language ("1,234.5" in en, "1 234,5" in ru/kk). */
export function formatLocalNumber(value: number | string | null | undefined, digits: string, lang: string): string {
  if (value === null || value === undefined || value === '') return '';
  const n = typeof value === 'number' ? value : Number(value);
  if (!isFinite(n)) return '';
  return formatNumber(n, toUiLang(lang), digits);
}

/** `{{ order.createdAt | ldate: 'shortDateTime' }}` — like `date`, but follows the app language. */
@Pipe({ name: 'ldate', standalone: true, pure: false })
export class LocalDatePipe implements PipeTransform {
  private lang = inject(UI_LANG);
  private last?: { value: unknown; style: string; lang: string; out: string };

  transform(value: string | number | Date | null | undefined, style = 'shortDate'): string {
    const lang = this.lang();
    const l = this.last;
    if (l && l.value === value && l.style === style && l.lang === lang) return l.out;
    const out = formatLocalDate(value, style, lang);
    this.last = { value, style, lang, out };
    return out;
  }
}

/** `{{ amount | lnumber: '1.0-2' }}` — like `number`, but follows the app language. */
@Pipe({ name: 'lnumber', standalone: true, pure: false })
export class LocalNumberPipe implements PipeTransform {
  private lang = inject(UI_LANG);
  private last?: { value: unknown; digits: string; lang: string; out: string };

  transform(value: number | string | null | undefined, digits = '1.0-3'): string {
    const lang = this.lang();
    const l = this.last;
    if (l && l.value === value && l.digits === digits && l.lang === lang) return l.out;
    const out = formatLocalNumber(value, digits, lang);
    this.last = { value, digits, lang, out };
    return out;
  }
}
