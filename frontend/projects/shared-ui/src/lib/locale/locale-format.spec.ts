import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { LocalDatePipe, LocalNumberPipe, UI_LANG, formatLocalDate, formatLocalNumber } from './locale-format';

// Built from parts so the test does not depend on the machine's time zone.
const may4 = new Date(1997, 4, 4, 15, 7);
const jan2026 = new Date(2026, 0, 15, 9, 30);

describe('formatLocalDate', () => {
  it('writes month names in Russian', () => {
    expect(formatLocalDate(may4, 'longDate', 'ru')).toBe('4 мая 1997');
    expect(formatLocalDate(jan2026, 'monthYear', 'ru')).toBe('январь 2026');
  });

  it('writes month names in Kazakh', () => {
    expect(formatLocalDate(may4, 'longDate', 'kk')).toBe('4 мамыр 1997');
    expect(formatLocalDate(jan2026, 'monthYear', 'kk').toLowerCase()).toBe('қаңтар 2026');
  });

  it('keeps English as before', () => {
    expect(formatLocalDate(may4, 'longDate', 'en')).toBe('4 May 1997');
    expect(formatLocalDate(jan2026, 'monthYear', 'en')).toBe('January 2026');
    expect(formatLocalDate(may4, 'shortDateTime', 'en')).toBe('May 4 · 3:07 PM');
  });

  it('uses day-first order and a 24-hour clock in ru/kk', () => {
    expect(formatLocalDate(may4, 'shortDateTime', 'ru')).toBe('4 мая · 15:07');
    expect(formatLocalDate(may4, 'shortDateTime', 'kk')).toBe('4 мам. · 15:07');
  });

  it('treats an unknown style as a plain pattern', () => {
    expect(formatLocalDate(may4, 'dd.MM HH:mm', 'ru')).toBe('04.05 15:07');
  });

  it('falls back to English for an unknown language', () => {
    expect(formatLocalDate(may4, 'longDate', 'de')).toBe('4 May 1997');
  });

  it('returns empty text for missing or broken dates', () => {
    expect(formatLocalDate(null, 'longDate', 'ru')).toBe('');
    expect(formatLocalDate('', 'longDate', 'ru')).toBe('');
    expect(formatLocalDate('not a date', 'longDate', 'ru')).toBe('');
  });
});

describe('formatLocalNumber', () => {
  it('uses a comma for decimals in ru/kk', () => {
    expect(formatLocalNumber(1234.5, '1.2-2', 'ru').replace(/\s/g, ' ')).toBe('1 234,50');
    expect(formatLocalNumber(1234.5, '1.2-2', 'kk').replace(/\s/g, ' ')).toBe('1 234,50');
    expect(formatLocalNumber(1234.5, '1.2-2', 'en')).toBe('1,234.50');
  });

  it('returns empty text for missing values', () => {
    expect(formatLocalNumber(null, '1.0-2', 'ru')).toBe('');
  });
});

@Component({
  imports: [LocalDatePipe, LocalNumberPipe],
  template: `<span id="d">{{ when | ldate: 'monthYear' }}</span><span id="n">{{ 2.5 | lnumber: '1.1-1' }}</span>`,
})
class Host {
  when = jan2026;
}

describe('ldate / lnumber pipes', () => {
  it('switch language live, without a reload', async () => {
    const lang = signal('en');
    TestBed.configureTestingModule({ providers: [{ provide: UI_LANG, useValue: () => lang() }] });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('#d')!.textContent).toBe('January 2026');
    expect(el.querySelector('#n')!.textContent).toBe('2.5');

    lang.set('ru');
    fixture.detectChanges();
    expect(el.querySelector('#d')!.textContent).toBe('январь 2026');
    expect(el.querySelector('#n')!.textContent).toBe('2,5');
  });
});
