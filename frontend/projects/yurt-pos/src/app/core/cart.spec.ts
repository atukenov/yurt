import { MenuItem, MenuTopping, PaymentMethod } from 'shared-models';
import {
  addLine, branchMethods, cartCount, cartTotal, changeDue, changeQuantity, COUNTER_METHODS, defaultVariant,
  needsOptions, toggleTopping, toRequest,
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
      locationId: 'branch', paymentMethod: PaymentMethod.Cash, idempotencyKey: 'key-1', handedOver: true, etaMinutes: null,
      items: [{ menuItemId: 'latte', quantity: 1, variantId: 'm', toppingIds: ['oat'] }],
    });
    expect(toRequest(lines, 'branch', PaymentMethod.Card, 'key-2', false, 5).etaMinutes).toBe(5);
  });
});
