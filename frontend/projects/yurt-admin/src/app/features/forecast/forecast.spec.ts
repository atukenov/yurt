import { barPercent, confidenceKey, dateInDays, hourLabel, openHours, plainAmount } from './forecast-helpers';

describe('forecast helpers', () => {
  it('finds dates relative to a day, across month ends', () => {
    expect(dateInDays(0, new Date(2026, 10, 13))).toBe('2026-11-13');
    expect(dateInDays(1, new Date(2026, 10, 30))).toBe('2026-12-01');
    expect(dateInDays(-1, new Date(2026, 0, 1))).toBe('2025-12-31');
  });

  it('keeps only the hours between the first and last busy one', () => {
    const hourly = Array(24).fill(0);
    hourly[8] = 3;
    hourly[11] = 7;
    expect(openHours(hourly)).toEqual([8, 9, 10, 11]);
    expect(openHours(Array(24).fill(0))).toEqual([]);
  });

  it('draws bars against the busiest hour', () => {
    expect(barPercent(10, 10)).toBe(100);
    expect(barPercent(5, 10)).toBe(50);
    expect(barPercent(0, 10)).toBe(0);
    expect(barPercent(1, 1000)).toBe(4);
    expect(barPercent(3, 0)).toBe(0);
  });

  it('labels hours, confidence and amounts', () => {
    expect(hourLabel(9)).toBe('09:00');
    expect(confidenceKey('low')).toBe('fc.conf.low');
    expect(plainAmount(74.4)).toBe('74.4');
    expect(plainAmount(122.7600001)).toBe('122.76');
    expect(plainAmount(3)).toBe('3');
  });
});
