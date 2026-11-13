import { checkDigitOk, findScanned, parseScaleReading, parseWeightLabel, ScanCollector, splitScaleLines, weighedPrice } from 'shared-api';

const cheese = { id: 'cheese', barcode: '000123', soldByWeight: true };
const water = { id: 'water', barcode: '4600000000008', soldByWeight: false };
const items = [cheese, water, { id: 'plain' }];

describe('scanner and scale helpers', () => {
  it('checks the last digit of an EAN code', () => {
    expect(checkDigitOk('4600000000008')).toBe(true);
    expect(checkDigitOk('4600000000009')).toBe(false);
    expect(checkDigitOk('12345')).toBe(false);
  });

  it('reads the item number and weight from a shop-scale label', () => {
    expect(parseWeightLabel('2000123003504')).toEqual({ plu: '000123', grams: 350 });
    expect(parseWeightLabel('2000123003505')).toBeNull(); // wrong last digit
    expect(parseWeightLabel('2000123000008')).toBeNull(); // no weight
    expect(parseWeightLabel('4600000000008')).toBeNull(); // not a label
  });

  it('finds a dish by its barcode, by a scale label, and says when it knows nothing', () => {
    expect(findScanned(items, ' 4600000000008 ')).toEqual({ kind: 'item', item: water });
    // The label's item number matches the dish's barcode, with or without leading zeros.
    expect(findScanned(items, '2000123003504')).toEqual({ kind: 'weighed', item: cheese, grams: 350 });
    expect(findScanned([{ ...cheese, barcode: '123' }], '2000123003504')).toEqual({ kind: 'weighed', item: { ...cheese, barcode: '123' }, grams: 350 });
    // A weighed dish scanned by its own code still needs weighing.
    expect(findScanned(items, '000123')).toEqual({ kind: 'weighed', item: cheese, grams: null });
    expect(findScanned(items, '9999999999994')).toEqual({ kind: 'unknown', code: '9999999999994' });
    expect(findScanned(items, '  ')).toEqual({ kind: 'unknown', code: '' });
    // A dish that is not sold by weight is not found through a weight label.
    expect(findScanned([{ id: 'x', barcode: '000123' }], '2000123003504').kind).toBe('unknown');
  });

  it('prices a weighing to the cent', () => {
    expect(weighedPrice(100, 350)).toBe(35);
    expect(weighedPrice(1999, 250)).toBe(499.75);
    expect(weighedPrice(333, 1)).toBe(0.33);
  });

  it('tells a scanner from a person typing', () => {
    const scan = new ScanCollector();
    let t = 0;
    let result: string | null = null;
    for (const key of '4600000000008') result = scan.feed(key, (t += 8));
    expect(result).toBeNull();
    expect(scan.feed('Enter', (t += 8))).toBe('4600000000008');

    // A person types slowly: nothing is taken as a scan.
    const slow = new ScanCollector();
    for (const key of '4600') slow.feed(key, (t += 400));
    expect(slow.feed('Enter', (t += 400))).toBeNull();

    // A short run (someone pressing Enter after "12") is not a scan either.
    const short = new ScanCollector();
    short.feed('1', (t += 5));
    short.feed('2', (t += 5));
    expect(short.feed('Enter', (t += 5))).toBeNull();
  });

  it('reads the lines scales send', () => {
    expect(parseScaleReading('ST,GS,   0.350kg')).toEqual({ grams: 350, stable: true });
    expect(parseScaleReading('US,GS,  0.120 kg')).toEqual({ grams: 120, stable: false });
    expect(parseScaleReading('+000.350 kg')).toEqual({ grams: 350, stable: true });
    expect(parseScaleReading('  350 g')).toEqual({ grams: 350, stable: true });
    expect(parseScaleReading('W 0.35')).toEqual({ grams: 350, stable: true });
    expect(parseScaleReading('1250')).toEqual({ grams: 1250, stable: true });
    expect(parseScaleReading('0,500kg ?')).toEqual({ grams: 500, stable: false });
    expect(parseScaleReading('ERR')).toBeNull();
    expect(parseScaleReading('')).toBeNull();
  });

  it('splits what a scale sent into whole lines and the unfinished rest', () => {
    expect(splitScaleLines('ST,GS, 0.1kg\r\nST,GS, 0.2kg\r\nST,G')).toEqual({ lines: ['ST,GS, 0.1kg', 'ST,GS, 0.2kg'], rest: 'ST,G' });
    expect(splitScaleLines('abc')).toEqual({ lines: [], rest: 'abc' });
  });
});
