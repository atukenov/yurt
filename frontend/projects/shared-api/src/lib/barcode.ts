/**
 * Barcode scanners and scales for the till and the admin app. No Angular in this file, so it can be tested.
 *
 * - A **scanner** that plugs in by USB or Bluetooth types the code like a very fast keyboard and ends with Enter.
 *   `ScanCollector` tells that apart from a person typing.
 * - A **weight label** is a barcode a shop scale prints: `2 IIIIII WWWWW C` (EAN-13). `IIIIII` is the scale's item number
 *   (PLU) and `WWWWW` the weight in grams. Enter the PLU as the dish's barcode and the till reads the weight from the label.
 * - A **scale** plugged into the till sends its reading as a line of text, for example `ST,GS,   0.350kg`.
 */

/** Trims a typed or scanned code. Empty means none. */
export function cleanBarcode(raw: string | null | undefined): string {
  return (raw ?? '').trim();
}

/** Whether the last digit of an EAN-13 or EAN-8 is right (so a half-read code is not taken for a real one). */
export function checkDigitOk(code: string): boolean {
  if ((code.length !== 13 && code.length !== 8) || !/^\d+$/.test(code)) return false;
  const digits = [...code].map(Number);
  const check = digits.pop()!;
  // Counting from the right (the digit before the check digit first), odd places weigh 3 and even places weigh 1.
  const sum = digits.reverse().reduce((s, d, i) => s + d * (i % 2 === 0 ? 3 : 1), 0);
  return (10 - (sum % 10)) % 10 === check;
}

/** A weight label read from a barcode: the scale's item number and the weight in grams. */
export interface WeightLabel {
  plu: string;
  grams: number;
}

/** Reads a shop-scale label (`2IIIIIIWWWWWC`), or null when the code is not one. */
export function parseWeightLabel(code: string): WeightLabel | null {
  if (!/^2\d{12}$/.test(code) || !checkDigitOk(code)) return null;
  const grams = Number(code.slice(7, 12));
  return grams > 0 ? { plu: code.slice(1, 7), grams } : null;
}

/** The dishes a scan can mean. */
export interface ScannableItem {
  id: string;
  barcode?: string | null;
  soldByWeight?: boolean;
}

/** What a scan found. */
export type ScanResult<T extends ScannableItem> =
  | { kind: 'item'; item: T }
  | { kind: 'weighed'; item: T; grams: number | null }
  | { kind: 'unknown'; code: string };

function samePlu(a: string, b: string): boolean {
  return a.replace(/^0+/, '') === b.replace(/^0+/, '');
}

/**
 * Finds the dish for a scanned code. A normal barcode matches a dish exactly. A weight label matches the dish whose
 * barcode is the label's item number; the grams come from the label. A weighed dish scanned by its plain code has no
 * weight yet (`grams` null): the till then asks the scale or the cashier.
 */
export function findScanned<T extends ScannableItem>(items: readonly T[], rawCode: string): ScanResult<T> {
  const code = cleanBarcode(rawCode);
  if (!code) return { kind: 'unknown', code };
  const exact = items.find((i) => cleanBarcode(i.barcode) === code);
  if (exact) return exact.soldByWeight ? { kind: 'weighed', item: exact, grams: null } : { kind: 'item', item: exact };
  const label = parseWeightLabel(code);
  if (label) {
    const byPlu = items.find((i) => i.soldByWeight && cleanBarcode(i.barcode) && samePlu(cleanBarcode(i.barcode), label.plu));
    if (byPlu) return { kind: 'weighed', item: byPlu, grams: label.grams };
  }
  return { kind: 'unknown', code };
}

/** The price of a weighed line: the price of 1 kg for this many grams, to the cent (the server does the same sum). */
export function weighedPrice(pricePerKg: number, grams: number): number {
  return Math.round((pricePerKg * grams) / 10) / 100;
}

/**
 * Tells a scanner from a person typing. Feed every key press; when Enter comes after a quick run of characters the code
 * is returned. A scanner types a whole code in a few milliseconds per key, a person needs far longer.
 */
export class ScanCollector {
  private buffer = '';
  private lastAt = 0;

  constructor(
    /** The longest pause between two keys of one scan (milliseconds). */
    private readonly maxGapMs = 60,
    /** The shortest code taken as a scan. */
    private readonly minLength = 4,
  ) {}

  /** Returns the code when this key completes a scan, else null. `now` is in milliseconds. */
  feed(key: string, now: number): string | null {
    if (this.buffer && now - this.lastAt > this.maxGapMs) this.buffer = '';
    this.lastAt = now;
    if (key === 'Enter') {
      const code = this.buffer;
      this.buffer = '';
      return code.length >= this.minLength ? code : null;
    }
    if (key.length === 1) this.buffer += key;
    return null;
  }

  /** True while a run of fast keys is building up, so the page can keep them out of a text field. */
  get collecting(): boolean {
    return this.buffer.length >= 2;
  }
}

/** One reading from a scale. */
export interface ScaleReading {
  grams: number;
  /** False while the scale says the weight is still moving. */
  stable: boolean;
}

/**
 * Reads one line a scale sent, such as `ST,GS,   0.350kg`, `US,GS,  0.120 kg`, `+000.350 kg`, `  350 g` or `W 0.35`.
 * A number with a unit is exact; without a unit a decimal point means kg and a whole number means grams. A line with
 * "US" (unstable) or "?" is not stable. Returns null for a line with no weight in it.
 */
export function parseScaleReading(line: string): ScaleReading | null {
  const text = line.replace(/\0/g, '').trim();
  if (!text) return null;
  const m = /([+-]?\d+(?:[.,]\d+)?)\s*(kg|g|lb)?/i.exec(text);
  if (!m) return null;
  const value = Number(m[1].replace(',', '.'));
  if (!Number.isFinite(value) || value < 0) return null;
  const unit = m[2]?.toLowerCase();
  let grams: number;
  if (unit === 'kg') grams = value * 1000;
  else if (unit === 'g') grams = value;
  else if (unit === 'lb') grams = value * 453.592;
  else grams = m[1].includes('.') || m[1].includes(',') ? value * 1000 : value;
  const stable = !/(^|[^A-Za-z])US([^A-Za-z]|$)/.test(text) && !text.includes('?');
  return { grams: Math.round(grams), stable };
}

/** Splits what a scale has sent so far into whole lines and the unfinished rest. */
export function splitScaleLines(pending: string): { lines: string[]; rest: string } {
  const parts = pending.split(/\r\n|\r|\n/);
  const rest = parts.pop() ?? '';
  return { lines: parts.filter((l) => l.trim()), rest };
}
