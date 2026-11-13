import { PaymentMethod } from 'shared-models';
import { CartLine } from './cart';
import { guestView, GuestViewInput, isStale, parseView, payPromptKey, STALE_MS } from './guest-display';

const line: CartLine = {
  key: 'a', menuItemId: 'a', name: 'Latte', variantId: 'v', variantLabel: '0.4', toppings: [{ id: 't', name: 'Oat milk', price: 100 }],
  unitPrice: 1600, quantity: 2,
};
const base: GuestViewInput = {
  step: 'cart', lang: 'ru', lines: [line], total: 3200, discount: 200, payable: 3000, tip: 300,
  method: PaymentMethod.Card, cashGiven: null, change: null, orderType: 'DineIn', paid: 0, saleNo: null, saved: false,
};
const now = new Date('2026-11-13T10:00:00Z');

describe('guest screen view', () => {
  it('shows an empty cart as the idle screen', () => {
    const v = guestView({ ...base, lines: [] }, now);
    expect(v.phase).toBe('idle');
    expect(v.lines).toEqual([]);
  });

  it('shows items and the bill while the cashier is still adding, without tip or payment yet', () => {
    const v = guestView(base, now);
    expect(v.phase).toBe('cart');
    expect(v.lines[0]).toMatchObject({ quantity: 2, name: 'Latte', extras: '0.4, + Oat milk', total: 3200 });
    expect(v.total).toBe(3200);
    expect(v.discount).toBe(200);
    expect(v.tip).toBe(0);
    expect(v.method).toBeNull();
  });

  it('shows tip, payment method and change on the payment step', () => {
    const v = guestView({ ...base, step: 'pay', method: PaymentMethod.Cash, cashGiven: 5000, change: 1700 }, now);
    expect(v.phase).toBe('pay');
    expect(v.tip).toBe(300);
    expect(v.method).toBe('Cash');
    expect(v.change).toBe(1700);
  });

  it('shows the number on a finished sale and drops the items', () => {
    const v = guestView({ ...base, step: 'done', paid: 3300, saleNo: 'ABC123' }, now);
    expect(v.phase).toBe('done');
    expect(v.lines).toEqual([]);
    expect(v.saleNo).toBe('ABC123');
    expect(v.paid).toBe(3300);
  });

  it('works for a sale saved without internet (T-number)', () => {
    const v = guestView({ ...base, step: 'done', saleNo: 'T-1A2B3C', saved: true }, now);
    expect(v.saved).toBe(true);
    expect(v.saleNo).toBe('T-1A2B3C');
  });

  it('keeps the till language', () => {
    expect(guestView({ ...base, lang: 'kk' }, now).lang).toBe('kk');
  });
});

describe('saved guest view', () => {
  it('reads what the till saved', () => {
    const saved = JSON.stringify(guestView({ ...base, step: 'pay' }, now));
    expect(parseView(saved, 'en', now).phase).toBe('pay');
  });

  it('falls back to idle for nothing, junk or a view from long ago', () => {
    expect(parseView(null, 'en', now).phase).toBe('idle');
    expect(parseView('{not json', 'en', now).phase).toBe('idle');
    expect(parseView('{"phase":"cart"}', 'en', now).phase).toBe('idle');
    const old = JSON.stringify(guestView(base, new Date(now.getTime() - STALE_MS - 1000)));
    expect(parseView(old, 'en', now).phase).toBe('idle');
    expect(isStale(guestView(base, now), now)).toBe(false);
  });
});

describe('payment prompt', () => {
  it('tells the guest what to do for each method', () => {
    expect(payPromptKey(null)).toBe('guest.payWait');
    expect(payPromptKey(PaymentMethod.Cash)).toBe('guest.payCash');
    expect(payPromptKey(PaymentMethod.Card)).toBe('guest.payCard');
    expect(payPromptKey(PaymentMethod.KaspiBank)).toBe('guest.payQr');
  });
});
