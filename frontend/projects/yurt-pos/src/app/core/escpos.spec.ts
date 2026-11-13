import { clean, cp866, receiptBytes, TillPrinterSettings, TillReceipt, wrap } from './escpos';

const printer: TillPrinterSettings = { lineWidth: 32, codeTable: 17, language: 'ru', cashDrawer: true };
const sale: TillReceipt = {
  brand: 'Yurt', branch: 'Downtown', at: new Date(2026, 10, 12, 9, 5), number: 'T-ABC123',
  lines: [
    { quantity: 2, name: 'Капучино', extras: 'L, + Овсяное молоко', total: 2400 },
    { quantity: 1, name: 'Круассан', extras: '', total: 900 },
  ],
  total: 3300, method: 'Cash', offline: true,
};

/** The printable text of the bytes (code page 866 back to letters, printer commands skipped), for reading in the tests. */
function text(bytes: Uint8Array): string {
  const table = new Map<number, string>();
  for (let c = 0x410; c <= 0x44f; c++) table.set(cp866(String.fromCharCode(c)), String.fromCharCode(c));
  table.set(0xf0, 'Ё').set(0xf1, 'ё');
  const args: Record<string, number> = { '@': 0, t: 1, a: 1, E: 1, d: 1, p: 3, '!': 1, V: 2 };
  let out = '';
  for (let i = 0; i < bytes.length; i++) {
    const b = bytes[i];
    if (b === 0x1b || b === 0x1d) {
      i += 1 + (args[String.fromCharCode(bytes[i + 1])] ?? 0);
    } else out += b === 0x0a ? '\n' : b >= 0x20 && b < 0x7f ? String.fromCharCode(b) : table.get(b) ?? '';
  }
  return out;
}

function has(bytes: Uint8Array, seq: number[]): boolean {
  outer: for (let i = 0; i + seq.length <= bytes.length; i++) {
    for (let j = 0; j < seq.length; j++) if (bytes[i + j] !== seq[j]) continue outer;
    return true;
  }
  return false;
}

describe('till printing (ESC/POS)', () => {
  it('writes Russian in code page 866 and swaps Kazakh letters like the server', () => {
    expect([..."Аая"].map(cp866)).toEqual([0x80, 0xa0, 0xef]);
    expect(cp866('ё')).toBe(0xf1);
    expect(clean('Қазақ «шай» ₸')).toBe('Казак "шай" тг');
  });

  it('wraps at spaces and cuts very long words', () => {
    expect(wrap('one two three', 7)).toEqual(['one two', 'three']);
    expect(wrap('abcdefghij', 4)).toEqual(['abcd', 'efgh', 'ij']);
    expect(wrap('', 5)).toEqual(['']);
  });

  it('prints the receipt with prices, total, method and the offline tax note', () => {
    const bytes = receiptBytes(printer, sale, true);
    const t = text(bytes);
    expect(t).toContain('#T-ABC123');
    expect(t).toContain('12.11.2026 09:05');
    expect(t).toMatch(/2 x Капучино +2 400 тг/);
    expect(t).toContain('L, + Овсяное молоко');
    expect(t).toMatch(/Итого +3 300 тг/);
    expect(t).toContain('Оплата: Наличные');
    expect(t).toContain('Фискальный чек');
    // Every line fits the paper.
    for (const line of t.split('\n')) expect(line.length).toBeLessThanOrEqual(32);
    // Drawer pulse, then the cut.
    expect(has(bytes, [0x1b, 0x70, 0, 25, 250])).toBe(true);
    expect(has(bytes, [0x1d, 0x56, 66, 0])).toBe(true);
  });

  it('prints the tip under the total, only when there is one', () => {
    expect(text(receiptBytes(printer, { ...sale, tip: 330 }))).toMatch(/Чаевые +330 тг/);
    expect(text(receiptBytes(printer, sale))).not.toContain('Чаевые');
  });

  it('opens the drawer only for cash on a printer with a drawer, and says nothing about tax when online', () => {
    expect(has(receiptBytes(printer, sale, false), [0x1b, 0x70])).toBe(false);
    expect(has(receiptBytes({ ...printer, cashDrawer: false }, sale, true), [0x1b, 0x70])).toBe(false);
    expect(text(receiptBytes(printer, { ...sale, offline: false }))).not.toContain('Фискальный');
  });
});
