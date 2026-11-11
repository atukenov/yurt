import { cafeNowMs, clock, secondsSince } from './staff-watch.component';

describe('staff watch live timers', () => {
  it('counts seconds from a cafe-local time', () => {
    const now = Date.UTC(2026, 10, 12, 10, 1, 35);
    expect(secondsSince('2026-11-12T10:00:00', now)).toBe(95);
    expect(secondsSince(null, now)).toBe(0);
    expect(secondsSince('2026-11-12T10:05:00', now)).toBe(0); // box clock a bit ahead: never negative
  });

  it('shows minutes and seconds', () => {
    expect(clock(0)).toBe('0:00');
    expect(clock(75)).toBe('1:15');
    expect(clock(3725)).toBe('1:02:05');
  });

  it('takes Almaty time as UTC+5', () => {
    const utc = new Date(Date.UTC(2026, 10, 12, 5, 0, 0));
    expect(new Date(cafeNowMs(utc)).getUTCHours()).toBe(10);
  });
});
