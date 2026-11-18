import { MenuItem, MenuTopping, PaymentMethod } from 'shared-models';
import {
  addLine, addWeighedLine, kgText, branchMethods, cartCount, cartTotal, changeDue, changeQuantity, COUNTER_METHODS, defaultVariant, discountOf,
  comboSummary, missingChoice, needsOptions, offlineNumber, receiptLines, tipOf, toggleTopping, toRequest,
} from './cart';

const latte: MenuItem = {
  id: 'latte', categoryId: 'coffee', categoryName: 'Coffee', name: 'Latte', description: '', price: 1000,
  isAvailable: true, isNew: false,
  variants: [
    { id: 's', label: 'S', price: 900, sortOrder: 0, isDefault: false },
    { id: 'm', label: 'M', price: 1100, sortOrder: 1, isDefault: true },
  ],
};
const water: MenuItem = { id: 'water', categoryId: 'drinks', categoryName: 'Drinks', name: 'Water', description: '', price: 300, isAvailable: true, isNew: false };
const oat: MenuTopping = { id: 'oat', name: 'Oat milk', price: 200, isAvailable: true, categoryIds: ['coffee'], group: 'milk' };
const soy: MenuTopping = { id: 'soy', name: 'Soy milk', price: 150, isAvailable: true, categoryIds: ['coffee'], group: 'milk' };
const syrup: MenuTopping = { id: 'syrup', name: 'Syrup', price: 100, isAvailable: true, categoryIds: ['coffee'], group: null };

describe('till cart', () => {
  it('merges the same item, size and add-ons into one line, in any add-on order', () => {
    let lines = addLine([], latte, 'm', [oat, syrup]);
    lines = addLine(lines, latte, 'm', [syrup, oat]);
    lines = addLine(lines, latte, 's', [oat]);
    expect(lines.length).toBe(2);
    expect(lines[0].quantity).toBe(2);
    expect(lines[0].unitPrice).toBe(1100 + 200 + 100);
    expect(cartCount(lines)).toBe(3);
    expect(cartTotal(lines)).toBe(2 * 1400 + 1100);
  });

  it('uses the item price when there is no size', () => {
    const lines = addLine([], water, null, []);
    expect(lines[0].unitPrice).toBe(300);
    expect(needsOptions(water)).toBe(false);
    expect(needsOptions(latte)).toBe(true);
    expect(defaultVariant(latte)?.id).toBe('m');
  });

  it('drops a line that goes down to zero', () => {
    let lines = addLine([], water, null, []);
    lines = changeQuantity(lines, lines[0].key, +2);
    expect(lines[0].quantity).toBe(3);
    lines = changeQuantity(lines, lines[0].key, -3);
    expect(lines).toEqual([]);
  });

  it('lets add-ons of one group replace each other', () => {
    let chosen = toggleTopping([], oat);
    chosen = toggleTopping(chosen, syrup);
    chosen = toggleTopping(chosen, soy);
    expect(chosen.map((t) => t.id).sort()).toEqual(['soy', 'syrup']);
    chosen = toggleTopping(chosen, syrup);
    expect(chosen.map((t) => t.id)).toEqual(['soy']);
  });

  it('keeps a required group at its smallest number and stops a group at its largest', () => {
    const rules = [
      { group: 'milk', title: 'Milk', required: true, min: 1, max: 1 },
      { group: 'syrup', title: 'Syrup', required: false, min: 0, max: 2 },
    ];
    const s1: MenuTopping = { ...syrup, id: 's1', group: 'syrup' };
    const s2: MenuTopping = { ...syrup, id: 's2', group: 'syrup' };
    const s3: MenuTopping = { ...syrup, id: 's3', group: 'syrup' };
    // milk is required: tapping the chosen one again does not drop it, picking another one swaps it
    let chosen = toggleTopping([], oat, rules);
    expect(toggleTopping(chosen, oat, rules).map((t) => t.id)).toEqual(['oat']);
    expect(toggleTopping(chosen, soy, rules).map((t) => t.id)).toEqual(['soy']);
    // syrup allows two
    chosen = toggleTopping(toggleTopping(toggleTopping(chosen, s1, rules), s2, rules), s3, rules);
    expect(chosen.map((t) => t.id).sort()).toEqual(['oat', 's1', 's2']);
    expect(toggleTopping(chosen, s1, rules).map((t) => t.id).sort()).toEqual(['oat', 's2']);
  });

  it('says which required group is still open', () => {
    const latteWithMilk: MenuItem = {
      ...latte, availableToppings: [oat, soy],
      optionRules: [{ group: 'milk', title: 'Milk', required: true, min: 1, max: 1 }],
    };
    expect(missingChoice(latteWithMilk, [])?.title).toBe('Milk');
    expect(missingChoice(latteWithMilk, [oat])).toBeNull();
    expect(missingChoice(latte, [])).toBeNull();
  });

  it('works out the change for cash', () => {
    expect(changeDue(1400, 2000)).toBe(600);
    expect(changeDue(1400, 1000)).toBeNull();
    expect(changeDue(1400, null)).toBeNull();
  });

  it('offers only the counter methods the branch takes', () => {
    expect(branchMethods(null)).toEqual(COUNTER_METHODS);
    expect(branchMethods(['Card', 'Other', 'KaspiBank'])).toEqual([PaymentMethod.Card, PaymentMethod.KaspiBank]);
  });

  it('sends only ids and quantities, never prices', () => {
    const lines = addLine([], latte, 'm', [oat]);
    const req = toRequest(lines, 'branch', PaymentMethod.Cash, 'key-1', true, 5);
    expect(req).toEqual({
      locationId: 'branch', paymentMethod: PaymentMethod.Cash, idempotencyKey: 'key-1', handedOver: true, etaMinutes: null, orderType: 'TakeAway',
      items: [{ menuItemId: 'latte', quantity: 1, variantId: 'm', toppingIds: ['oat'] }],
    });
    expect(toRequest(lines, 'branch', PaymentMethod.Card, 'key-2', false, 5).etaMinutes).toBe(5);
    expect(toRequest(lines, 'branch', PaymentMethod.Card, 'key-3', false, 5, 'DineIn').orderType).toBe('DineIn');
  });
});

describe('discount', () => {
  it('takes a percent or a fixed amount off, never more than the bill', () => {
    expect(discountOf(2000, { kind: 'percent', value: 10 })).toBe(200);
    expect(discountOf(1999, { kind: 'percent', value: 15 })).toBe(299.85);
    expect(discountOf(2000, { kind: 'amount', value: 500 })).toBe(500);
    expect(discountOf(2000, { kind: 'amount', value: 9000 })).toBe(2000);
    expect(discountOf(2000, { kind: 'percent', value: 250 })).toBe(2000);
    expect(discountOf(2000, null)).toBe(0);
    expect(discountOf(2000, { kind: 'amount', value: 0 })).toBe(0);
  });

  it('goes into the sale request with the manager PIN and reason, and is left out without one', () => {
    const lines = addLine([], latte, 'm', []);
    const req = toRequest(lines, 'branch', PaymentMethod.Cash, 'k', false, 5, 'TakeAway', { kind: 'percent', value: 10, pin: '4821', reason: 'Regular' });
    expect(req.discount).toEqual({ percent: 10, amount: null, pin: '4821', reason: 'Regular' });
    expect('discount' in toRequest(lines, 'branch', PaymentMethod.Cash, 'k', false, 5)).toBe(false);
  });
});

describe('receipt lines', () => {
  it('puts size and add-ons after the name and gives the line total', () => {
    const lines = receiptLines([{
      key: 'k', menuItemId: 'm', name: 'Latte', variantId: 'v', variantLabel: 'L',
      toppings: [{ id: 't', name: 'Oat milk', price: 200 }], unitPrice: 1400, quantity: 2,
    }]);
    expect(lines).toEqual([{ quantity: 2, name: 'Latte', extras: 'L, + Oat milk', total: 2800 }]);
    expect(offlineNumber('3f2a9c10-aaaa-bbbb')).toBe('T-3F2A9C');
  });
});

describe('tip', () => {
  it('is a percent of the bill or a typed amount, never negative', () => {
    expect(tipOf(2000, { kind: 'none' })).toBe(0);
    expect(tipOf(2000, { kind: 'percent', value: 10 })).toBe(200);
    expect(tipOf(1999, { kind: 'percent', value: 15 })).toBe(299.85);
    expect(tipOf(2000, { kind: 'amount', value: 350 })).toBe(350);
    expect(tipOf(2000, { kind: 'amount', value: -5 })).toBe(0);
    expect(tipOf(2000, { kind: 'percent', value: 250 })).toBe(2000);
  });

  it('goes into the sale request, with its own method only when it differs from the bill, and is left out without one', () => {
    const lines = addLine([], latte, 'm', []);
    const card = toRequest(lines, 'branch', PaymentMethod.Card, 'k', false, 5, 'TakeAway', null, 100, PaymentMethod.Cash);
    expect(card.tip).toBe(100);
    expect(card.tipMethod).toBe(PaymentMethod.Cash);
    const same = toRequest(lines, 'branch', PaymentMethod.Card, 'k', false, 5, 'TakeAway', null, 100, PaymentMethod.Card);
    expect('tipMethod' in same).toBe(false);
    expect('tip' in toRequest(lines, 'branch', PaymentMethod.Cash, 'k', false, 5)).toBe(false);
  });
});

describe('combos on the till', () => {
  const combo: MenuItem = {
    id: 'combo', categoryId: 'coffee', categoryName: 'Coffee', name: 'Coffee & bun', description: '', price: 1200,
    isAvailable: true, isNew: false, isCombo: true,
    comboItems: [
      { itemId: 'latte', name: 'Latte', quantity: 1 },
      { itemId: 'bun', name: 'Bun', quantity: 2 },
    ],
  };

  it('says what is inside on one line', () => {
    expect(comboSummary(combo)).toBe('Latte + 2 × Bun');
    expect(comboSummary(water)).toBe('');
  });

  it('is added straight away at its set price, with no size or add-ons', () => {
    expect(needsOptions(combo)).toBe(false);
    const lines = addLine([], combo, null, [], 2);
    expect(cartTotal(lines)).toBe(2400);
  });
});

describe('weighed items', () => {
  const cheese: MenuItem = { id: 'cheese', categoryId: 'deli', categoryName: 'Deli', name: 'Cheese', description: '', price: 100, isAvailable: true, isNew: false, soldByWeight: true, barcode: '000123' };

  it('makes one line per weighing, priced for the weight, and cannot add more of the same line', () => {
    let lines = addWeighedLine([], cheese, 350, 'a');
    lines = addWeighedLine(lines, cheese, 1250, 'b');
    expect(lines.length).toBe(2);
    expect(lines.map((l) => l.unitPrice)).toEqual([35, 125]);
    expect(cartTotal(lines)).toBe(160);
    expect(changeQuantity(lines, lines[0].key, +1)).toEqual(lines);
    expect(changeQuantity(lines, lines[0].key, -1).length).toBe(1);
    expect(kgText(350)).toBe('0.350 kg');
  });

  it('sends the weight with the sale and prints it on the receipt', () => {
    const lines = addWeighedLine([], cheese, 350, 'a');
    const req = toRequest(lines, 'branch', PaymentMethod.Cash, 'k', true, null);
    expect(req.items).toEqual([{ menuItemId: 'cheese', quantity: 1, variantId: null, toppingIds: [], weightGrams: 350 }]);
    expect(receiptLines(lines)).toEqual([{ quantity: 1, weightGrams: 350, name: 'Cheese', extras: '', total: 35 }]);
    // A plain item carries no weight.
    expect('weightGrams' in toRequest(addLine([], water, null, []), 'branch', PaymentMethod.Cash, 'k', true, null).items[0]).toBe(false);
  });
});
