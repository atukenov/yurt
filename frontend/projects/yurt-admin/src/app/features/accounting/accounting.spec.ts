import { daysBetween, presetRange } from './accounting.component';

describe('accounting export helpers', () => {
  const now = new Date(2026, 10, 12); // 12 Nov 2026

  it('works out the ready-made periods', () => {
    expect(presetRange('lastMonth', now)).toEqual({ from: '2026-10-01', to: '2026-10-31' });
    expect(presetRange('thisMonth', now)).toEqual({ from: '2026-11-01', to: '2026-11-12' });
    expect(presetRange('lastQuarter', now)).toEqual({ from: '2026-07-01', to: '2026-09-30' });
    expect(presetRange('lastYear', now)).toEqual({ from: '2025-01-01', to: '2025-12-31' });
    // In January, last month and last quarter are in the year before.
    const jan = new Date(2027, 0, 5);
    expect(presetRange('lastMonth', jan)).toEqual({ from: '2026-12-01', to: '2026-12-31' });
    expect(presetRange('lastQuarter', jan)).toEqual({ from: '2026-10-01', to: '2026-12-31' });
  });

  it('counts the days with both ends included', () => {
    expect(daysBetween('2026-11-01', '2026-11-30')).toBe(30);
    expect(daysBetween('2026-11-01', '2026-11-01')).toBe(1);
    expect(daysBetween('2026-11-02', '2026-11-01')).toBe(0);
    expect(daysBetween('', '2026-11-01')).toBe(0);
  });
});
