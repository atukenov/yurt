import { ForecastConfidence } from 'shared-models';

/** A date as `yyyy-MM-dd` in the browser's local time, `days` from today. */
export function dateInDays(days: number, from: Date = new Date()): string {
  const d = new Date(from.getFullYear(), from.getMonth(), from.getDate() + days);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** The hours the cafe sold in, from the first to the last with an expected item (never empty). */
export function openHours(hourly: number[]): number[] {
  const busy = hourly.map((n, h) => (n > 0 ? h : -1)).filter((h) => h >= 0);
  if (busy.length === 0) return [];
  const first = busy[0];
  const last = busy[busy.length - 1];
  return Array.from({ length: last - first + 1 }, (_, i) => first + i);
}

/** Bar height in percent of the busiest hour; an hour with something expected never shrinks below a sliver. */
export function barPercent(value: number, max: number): number {
  if (!(value > 0) || !(max > 0)) return 0;
  return Math.max(4, Math.round((value / max) * 100));
}

/** "09:00" for hour 9. */
export function hourLabel(hour: number): string {
  return `${String(hour).padStart(2, '0')}:00`;
}

/** Translation key that says how far to trust the forecast. */
export function confidenceKey(confidence: ForecastConfidence): string {
  return `fc.conf.${confidence}`;
}

/** A plain number for amounts: up to 2 decimals, no trailing zeros. */
export function plainAmount(n: number): string {
  return String(Math.round(n * 100) / 100);
}
